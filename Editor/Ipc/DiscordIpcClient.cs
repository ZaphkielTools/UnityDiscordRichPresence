using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Zaphkiel.DiscordRichPresence
{
    internal enum ConnectionState
    {
        Disconnected,
        Handshaking,
        Ready,
    }

    internal sealed class DiscordIpcClient : IDisposable
    {
        readonly string applicationId;
        readonly int processId;
        readonly Func<IIpcTransport> openTransport;
        int nonce;

        public DiscordIpcClient(string applicationId, int processId, Func<IIpcTransport> openTransport)
        {
            this.applicationId = applicationId;
            this.processId = processId;
            this.openTransport = openTransport;
        }

        public ConnectionState State { get; private set; }
        public IIpcTransport Transport { get; private set; }
        public string UserName { get; private set; }
        public string LastError { get; private set; }
        public bool WasRejected { get; private set; }

        public bool Open()
        {
            Close();
            Transport = openTransport();
            if (Transport == null) return false;
            State = ConnectionState.Handshaking;
            Log.Info("Pipe opened, sending the handshake.");
            return Send(Opcode.Handshake, IpcCommands.Handshake(applicationId));
        }

        public void Resume(IIpcTransport transport, string userName)
        {
            Close();
            Transport = transport;
            UserName = userName;
            State = ConnectionState.Ready;
        }

        public void Release()
        {
            Transport = null;
            State = ConnectionState.Disconnected;
        }

        public void Poll()
        {
            try
            {
                while (Transport != null && Transport.TryRead(out var frame)) Handle(frame);
            }
            catch (Exception e) when (IsConnectionError(e))
            {
                Drop(e.Message);
            }
        }

        public bool SetActivity(string activityJson) =>
            State == ConnectionState.Ready &&
            Send(Opcode.Frame, IpcCommands.SetActivity(processId, activityJson, (++nonce).ToString(CultureInfo.InvariantCulture)));

        public void Drop(string reason)
        {
            LastError = reason;
            Log.Info("Disconnected: " + reason);
            Close();
        }

        public void Close()
        {
            Transport?.Dispose();
            Transport = null;
            State = ConnectionState.Disconnected;
        }

        public void Dispose() => Close();

        bool Send(Opcode opcode, string json)
        {
            try
            {
                Transport.Write(new IpcFrame(opcode, json));
                return true;
            }
            catch (Exception e) when (IsConnectionError(e))
            {
                Drop(e.Message);
                return false;
            }
        }

        void Handle(IpcFrame frame)
        {
            switch (frame.Opcode)
            {
                case Opcode.Ping:
                    Send(Opcode.Pong, frame.Json);
                    break;
                case Opcode.Close:
                    var close = Parse<IpcMessageData>(frame.Json);
                    WasRejected = true;
                    Drop($"Discord closed the connection: {close?.message} ({close?.code})");
                    Log.WarningOnce(LastError);
                    break;
                case Opcode.Frame:
                    HandleMessage(Parse<IpcMessage>(frame.Json));
                    break;
            }
        }

        void HandleMessage(IpcMessage message)
        {
            if (message == null) return;
            if (message.evt == "READY")
            {
                UserName = message.data?.user?.DisplayName;
                WasRejected = false;
                LastError = null;
                State = ConnectionState.Ready;
                Log.Info("Connected as " + UserName + ".");
            }
            else if (message.evt == "ERROR")
            {
                LastError = $"{message.data?.message} ({message.data?.code})";
                Log.WarningOnce($"Discord rejected {message.cmd}: {LastError}");
            }
        }

        static T Parse<T>(string json) where T : class
        {
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        static bool IsConnectionError(Exception e) =>
            e is IOException || e is InvalidDataException || e is ObjectDisposedException || e is InvalidOperationException;
    }
}
