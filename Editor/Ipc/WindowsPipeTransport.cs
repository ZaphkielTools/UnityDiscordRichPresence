using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Zaphkiel.DiscordRichPresence
{
    internal sealed class WindowsPipeTransport : IIpcTransport
    {
        public const string DiscordPipePrefix = "discord-ipc-";
        public const int DiscordPipeCount = 10;

        readonly NamedPipeClientStream stream;
        readonly FrameReader reader = new FrameReader();

        WindowsPipeTransport(NamedPipeClientStream stream)
        {
            this.stream = stream;
            ServerProcessId = GetNamedPipeServerProcessId(stream.SafePipeHandle, out uint id) ? id : 0;
        }

        public uint ServerProcessId { get; }

        public static WindowsPipeTransport OpenDiscord()
        {
            for (int i = 0; i < DiscordPipeCount; i++)
            {
                var transport = Open(DiscordPipePrefix + i);
                if (transport != null) return transport;
            }
            return null;
        }

        public static WindowsPipeTransport Open(string pipeName)
        {
            var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.None);
            try
            {
                stream.Connect(0);
                return new WindowsPipeTransport(stream);
            }
            catch (Exception e) when (e is TimeoutException || e is IOException || e is UnauthorizedAccessException || e is Win32Exception)
            {
                stream.Dispose();
                return null;
            }
        }

        public static WindowsPipeTransport Attach(long handle, uint serverProcessId)
        {
            if (handle == 0 || serverProcessId == 0) return null;
            var probe = new SafePipeHandle(new IntPtr(handle), false);
            if (!GetNamedPipeServerProcessId(probe, out uint actual) || actual != serverProcessId) return null;
            WindowsPipeTransport transport = null;
            try
            {
                transport = new WindowsPipeTransport(new NamedPipeClientStream(PipeDirection.InOut, false, true, new SafePipeHandle(new IntPtr(handle), true)));
                transport.Available();
                return transport;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                transport?.Dispose();
                return null;
            }
        }

        public bool TryDetach(out long handle)
        {
            handle = 0;
            if (reader.HasPartialFrame || !stream.IsConnected) return false;
            var pipe = stream.SafePipeHandle;
            handle = pipe.DangerousGetHandle().ToInt64();
            pipe.SetHandleAsInvalid();
            return true;
        }

        public void Write(IpcFrame frame)
        {
            byte[] bytes = frame.Encode();
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        public bool TryRead(out IpcFrame frame)
        {
            int available = Available();
            return reader.TryRead(stream, ref available, out frame);
        }

        public void Dispose() => stream.Dispose();

        int Available()
        {
            if (!PeekNamedPipe(stream.SafePipeHandle, IntPtr.Zero, 0, IntPtr.Zero, out uint available, IntPtr.Zero))
                throw new IOException("Discord closed the connection.", new Win32Exception(Marshal.GetLastWin32Error()));
            return (int)Math.Min(available, int.MaxValue);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool PeekNamedPipe(SafePipeHandle pipe, IntPtr buffer, uint bufferSize, IntPtr bytesRead, out uint totalBytesAvailable, IntPtr bytesLeftThisMessage);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);
    }
}
