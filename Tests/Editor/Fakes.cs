using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Zaphkiel.DiscordRichPresence.Tests
{
    sealed class FakeTransport : IIpcTransport
    {
        public readonly List<IpcFrame> Written = new List<IpcFrame>();
        public readonly Queue<IpcFrame> Incoming = new Queue<IpcFrame>();
        public bool Broken;
        public bool Disposed;

        public IEnumerable<IpcFrame> Commands => Written.Where(f => f.Opcode == Opcode.Frame);

        public void Write(IpcFrame frame)
        {
            if (Broken) throw new IOException("The pipe is broken.");
            Written.Add(frame);
        }

        public bool TryRead(out IpcFrame frame)
        {
            if (Broken) throw new IOException("The pipe is broken.");
            if (Incoming.Count == 0)
            {
                frame = default;
                return false;
            }
            frame = Incoming.Dequeue();
            return true;
        }

        public void Dispose() => Disposed = true;

        public void Receive(Opcode opcode, string json) => Incoming.Enqueue(new IpcFrame(opcode, json));

        public void ReceiveReady(string globalName = "Tester", string userName = "tester") =>
            Receive(Opcode.Frame, "{\"cmd\":\"DISPATCH\",\"data\":{\"v\":1,\"config\":{\"api_endpoint\":\"//discord.com/api\"},\"user\":{\"id\":\"1\",\"username\":\"" + userName + "\",\"global_name\":\"" + globalName + "\"}},\"evt\":\"READY\",\"nonce\":null}");
    }

    sealed class FakeDiscord
    {
        public const string ApplicationId = "123456";
        public const int ProcessId = 4242;

        public readonly List<FakeTransport> Connections = new List<FakeTransport>();
        public bool Running = true;
        public int Attempts;

        public FakeTransport Last => Connections[Connections.Count - 1];

        public IIpcTransport Open()
        {
            Attempts++;
            if (!Running) return null;
            var transport = new FakeTransport();
            Connections.Add(transport);
            return transport;
        }

        public DiscordIpcClient CreateClient() => new DiscordIpcClient(ApplicationId, ProcessId, Open);
    }
}
