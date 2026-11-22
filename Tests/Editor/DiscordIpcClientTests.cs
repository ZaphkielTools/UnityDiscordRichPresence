using System.Linq;
using NUnit.Framework;

namespace Zaphkiel.DiscordRichPresence.Tests
{
    sealed class DiscordIpcClientTests
    {
        FakeDiscord discord;
        DiscordIpcClient client;

        [SetUp]
        public void SetUp()
        {
            discord = new FakeDiscord();
            client = discord.CreateClient();
        }

        void Connect()
        {
            Assert.IsTrue(client.Open());
            discord.Last.ReceiveReady();
            client.Poll();
        }

        [Test]
        public void Open_SendsTheHandshake()
        {
            Assert.IsTrue(client.Open());
            Assert.AreEqual(ConnectionState.Handshaking, client.State);
            var handshake = discord.Last.Written.Single();
            Assert.AreEqual(Opcode.Handshake, handshake.Opcode);
            Assert.AreEqual(IpcCommands.Handshake(FakeDiscord.ApplicationId), handshake.Json);
        }

        [Test]
        public void Open_WithoutDiscord_StaysDisconnected()
        {
            discord.Running = false;
            Assert.IsFalse(client.Open());
            Assert.AreEqual(ConnectionState.Disconnected, client.State);
        }

        [Test]
        public void Ready_StoresTheDisplayName()
        {
            Connect();
            Assert.AreEqual(ConnectionState.Ready, client.State);
            Assert.AreEqual("Tester", client.UserName);
        }

        [Test]
        public void Ready_FallsBackToTheUsername()
        {
            client.Open();
            discord.Last.ReceiveReady("", "tester");
            client.Poll();
            Assert.AreEqual("tester", client.UserName);
        }

        [Test]
        public void Ping_IsAnsweredWithTheSamePayload()
        {
            Connect();
            discord.Last.Receive(Opcode.Ping, "{\"n\":5}");
            client.Poll();
            var pong = discord.Last.Written.Last();
            Assert.AreEqual(Opcode.Pong, pong.Opcode);
            Assert.AreEqual("{\"n\":5}", pong.Json);
        }

        [Test]
        public void CloseFrame_DisconnectsAndMarksTheRejection()
        {
            client.Open();
            var transport = discord.Last;
            transport.Receive(Opcode.Close, "{\"code\":4000,\"message\":\"Invalid Client ID\"}");
            client.Poll();
            Assert.AreEqual(ConnectionState.Disconnected, client.State);
            Assert.IsTrue(client.WasRejected);
            StringAssert.Contains("Invalid Client ID", client.LastError);
            Assert.IsTrue(transport.Disposed);
        }

        [Test]
        public void BrokenPipe_Disconnects()
        {
            Connect();
            discord.Last.Broken = true;
            client.Poll();
            Assert.AreEqual(ConnectionState.Disconnected, client.State);
            Assert.IsFalse(client.WasRejected);
            Assert.IsNotNull(client.LastError);
        }

        [Test]
        public void SetActivity_BeforeReady_SendsNothing()
        {
            client.Open();
            Assert.IsFalse(client.SetActivity("{}"));
            Assert.IsEmpty(discord.Last.Commands);
        }

        [Test]
        public void SetActivity_SendsTheCommandWithAFreshNonce()
        {
            Connect();
            Assert.IsTrue(client.SetActivity("{\"state\":\"a\"}"));
            Assert.IsTrue(client.SetActivity(null));
            var commands = discord.Last.Commands.Select(f => f.Json).ToArray();
            Assert.AreEqual(IpcCommands.SetActivity(FakeDiscord.ProcessId, "{\"state\":\"a\"}", "1"), commands[0]);
            Assert.AreEqual(IpcCommands.SetActivity(FakeDiscord.ProcessId, null, "2"), commands[1]);
        }

        [Test]
        public void ErrorEvent_KeepsTheConnection()
        {
            Connect();
            discord.Last.Receive(Opcode.Frame, "{\"cmd\":\"SET_ACTIVITY\",\"evt\":\"ERROR\",\"data\":{\"code\":4000,\"message\":\"child \\\"activity\\\" fails\"},\"nonce\":\"1\"}");
            client.Poll();
            Assert.AreEqual(ConnectionState.Ready, client.State);
            StringAssert.Contains("child \"activity\" fails", client.LastError);
        }

        [Test]
        public void InvalidJson_IsIgnored()
        {
            Connect();
            discord.Last.Receive(Opcode.Frame, "not json");
            client.Poll();
            Assert.AreEqual(ConnectionState.Ready, client.State);
        }

        [Test]
        public void Release_ForgetsTheTransportWithoutClosingIt()
        {
            Connect();
            var transport = discord.Last;
            client.Release();
            Assert.AreEqual(ConnectionState.Disconnected, client.State);
            Assert.IsFalse(transport.Disposed);
        }

        [Test]
        public void Resume_IsReadyAtOnce()
        {
            var transport = new FakeTransport();
            client.Resume(transport, "Tester");
            Assert.AreEqual(ConnectionState.Ready, client.State);
            Assert.IsTrue(client.SetActivity("{}"));
            Assert.AreEqual(1, transport.Commands.Count());
        }
    }
}
