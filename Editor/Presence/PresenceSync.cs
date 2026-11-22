using System;

namespace Zaphkiel.DiscordRichPresence
{
    internal sealed class PresenceSync : IDisposable
    {
        public const double SendInterval = 4;
        public const double RetryInterval = 5;
        public const double RejectedRetryInterval = 60;
        public const double HandshakeTimeout = 10;

        string desired;
        bool enabled = true;
        bool connected;
        bool upToDate;
        double nextSend;
        double nextConnect;
        double handshakeDeadline;

        public PresenceSync(DiscordIpcClient client) => Client = client;

        public DiscordIpcClient Client { get; }
        public string Sent { get; private set; }

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                connected = false;
                nextConnect = 0;
                if (!value) Client.Close();
            }
        }

        public void Publish(string activityJson) => desired = activityJson;

        public void Resume(string sent)
        {
            Sent = sent;
            connected = true;
            upToDate = true;
        }

        public void Tick(double now)
        {
            if (!enabled) return;
            Client.Poll();
            switch (Client.State)
            {
                case ConnectionState.Disconnected:
                    if (connected)
                    {
                        connected = false;
                        upToDate = false;
                        nextConnect = now + (Client.WasRejected ? RejectedRetryInterval : RetryInterval);
                    }
                    if (now < nextConnect) return;
                    nextConnect = now + RetryInterval;
                    if (!Client.Open()) return;
                    connected = true;
                    handshakeDeadline = now + HandshakeTimeout;
                    return;
                case ConnectionState.Handshaking:
                    if (now >= handshakeDeadline) Client.Drop("Discord did not answer the handshake.");
                    return;
                case ConnectionState.Ready:
                    if (upToDate && desired == Sent) return;
                    if (now < nextSend || !Client.SetActivity(desired)) return;
                    Sent = desired;
                    upToDate = true;
                    nextSend = now + SendInterval;
                    return;
            }
        }

        public void Dispose() => Client.Dispose();
    }
}
