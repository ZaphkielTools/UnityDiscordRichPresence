using System.Linq;
using NUnit.Framework;

namespace Zaphkiel.DiscordRichPresence.Tests
{
    sealed class PresenceSyncTests
    {
        const string A = "{\"state\":\"A\"}";
        const string B = "{\"state\":\"B\"}";
        const string C = "{\"state\":\"C\"}";

        FakeDiscord discord;
        PresenceSync sync;

        [SetUp]
        public void SetUp()
        {
            discord = new FakeDiscord();
            sync = new PresenceSync(discord.CreateClient());
        }

        string[] Sent(FakeTransport transport) =>
            transport.Commands.Select(f => f.Json).ToArray();

        static string Command(string activity, int nonce) =>
            IpcCommands.SetActivity(FakeDiscord.ProcessId, activity, nonce.ToString());

        void ConnectAt(double now)
        {
            sync.Tick(now);
            discord.Last.ReceiveReady();
            sync.Tick(now);
        }

        [Test]
        public void Tick_ConnectsThenSendsOnceReady()
        {
            sync.Publish(A);
            sync.Tick(0);
            Assert.AreEqual(1, discord.Connections.Count);
            Assert.IsEmpty(Sent(discord.Last));
            discord.Last.ReceiveReady();
            sync.Tick(0.25);
            CollectionAssert.AreEqual(new[] { Command(A, 1) }, Sent(discord.Last));
            Assert.AreEqual(A, sync.Sent);
        }

        [Test]
        public void SameActivity_IsSentOnce()
        {
            sync.Publish(A);
            ConnectAt(0);
            sync.Tick(10);
            sync.Tick(20);
            Assert.AreEqual(1, Sent(discord.Last).Length);
        }

        [Test]
        public void Changes_AreThrottledAndOnlyTheLatestIsSent()
        {
            sync.Publish(A);
            ConnectAt(0);
            sync.Publish(B);
            sync.Tick(1);
            sync.Publish(C);
            sync.Tick(PresenceSync.SendInterval - 0.1);
            Assert.AreEqual(1, Sent(discord.Last).Length);
            sync.Tick(PresenceSync.SendInterval);
            CollectionAssert.AreEqual(new[] { Command(A, 1), Command(C, 2) }, Sent(discord.Last));
        }

        [Test]
        public void LostConnection_ReconnectsAndSendsAgain()
        {
            sync.Publish(A);
            ConnectAt(0);
            var first = discord.Last;
            first.Broken = true;
            sync.Tick(1);
            Assert.AreEqual(ConnectionState.Disconnected, sync.Client.State);
            sync.Tick(1 + PresenceSync.RetryInterval - 0.1);
            Assert.AreEqual(1, discord.Connections.Count);
            ConnectAt(1 + PresenceSync.RetryInterval);
            Assert.AreNotSame(first, discord.Last);
            CollectionAssert.AreEqual(new[] { Command(A, 2) }, Sent(discord.Last));
        }

        [Test]
        public void MissingDiscord_IsRetriedOnASchedule()
        {
            discord.Running = false;
            sync.Publish(A);
            sync.Tick(0);
            sync.Tick(PresenceSync.RetryInterval - 0.1);
            Assert.AreEqual(1, discord.Attempts);
            discord.Running = true;
            sync.Tick(PresenceSync.RetryInterval);
            Assert.AreEqual(2, discord.Attempts);
            Assert.AreEqual(ConnectionState.Handshaking, sync.Client.State);
        }

        [Test]
        public void Rejection_WaitsLongerBeforeRetrying()
        {
            sync.Publish(A);
            sync.Tick(0);
            discord.Last.Receive(Opcode.Close, "{\"code\":4000,\"message\":\"Invalid Client ID\"}");
            sync.Tick(1);
            sync.Tick(1 + PresenceSync.RetryInterval);
            Assert.AreEqual(1, discord.Attempts);
            sync.Tick(1 + PresenceSync.RejectedRetryInterval);
            Assert.AreEqual(2, discord.Attempts);
        }

        [Test]
        public void SilentDiscord_TimesOutTheHandshake()
        {
            sync.Publish(A);
            sync.Tick(0);
            var transport = discord.Last;
            sync.Tick(PresenceSync.HandshakeTimeout);
            Assert.AreEqual(ConnectionState.Disconnected, sync.Client.State);
            Assert.IsTrue(transport.Disposed);
        }

        [Test]
        public void Disabling_ClosesTheConnectionAndStaysOff()
        {
            sync.Publish(A);
            ConnectAt(0);
            var transport = discord.Last;
            sync.Enabled = false;
            Assert.IsTrue(transport.Disposed);
            sync.Tick(100);
            Assert.AreEqual(1, discord.Attempts);
        }

        [Test]
        public void Reenabling_ConnectsAtOnce()
        {
            sync.Publish(A);
            ConnectAt(0);
            sync.Enabled = false;
            sync.Enabled = true;
            sync.Tick(0.5);
            Assert.AreEqual(2, discord.Attempts);
        }

        [Test]
        public void Resume_DoesNotResendTheSameActivity()
        {
            var transport = new FakeTransport();
            sync.Client.Resume(transport, "Tester");
            sync.Resume(A);
            sync.Publish(A);
            sync.Tick(0);
            Assert.IsEmpty(Sent(transport));
            sync.Publish(B);
            sync.Tick(0.25);
            CollectionAssert.AreEqual(new[] { Command(B, 1) }, Sent(transport));
        }
    }
}
