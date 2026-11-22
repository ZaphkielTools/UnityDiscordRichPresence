using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Zaphkiel.DiscordRichPresence.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor)]
    sealed class WindowsPipeTransportTests
    {
        const int TimeoutMilliseconds = 2000;
        const int BufferSize = 4096;

        readonly List<IDisposable> cleanup = new List<IDisposable>();
        string pipeName;
        NamedPipeServerStream server;

        [SetUp]
        public void SetUp()
        {
            pipeName = "zaphkiel-drp-tests-" + Guid.NewGuid().ToString("N");
            server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, BufferSize, BufferSize);
            cleanup.Add(server);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in cleanup) item.Dispose();
            cleanup.Clear();
        }

        static Task<T> OnWorker<T>(Func<T> work) => Task.Factory.StartNew(work, TaskCreationOptions.LongRunning);

        static T Await<T>(Task<T> task, string failure)
        {
            Assert.IsTrue(task.Wait(TimeoutMilliseconds), failure);
            return task.Result;
        }

        WindowsPipeTransport Connect()
        {
            var waiting = OnWorker(() =>
            {
                server.WaitForConnection();
                return true;
            });
            var transport = WindowsPipeTransport.Open(pipeName);
            Assert.IsNotNull(transport);
            cleanup.Add(transport);
            Await(waiting, "the server saw no client");
            return transport;
        }

        Task<IpcFrame> ReceiveOnServer() => OnWorker(() =>
        {
            var header = ReadExactly(server, IpcFrame.HeaderSize);
            IpcFrame.ParseHeader(header, out var opcode, out int length);
            return new IpcFrame(opcode, Encoding.UTF8.GetString(ReadExactly(server, length)));
        });

        Task<bool> SendFromServer(IpcFrame frame) => OnWorker(() =>
        {
            byte[] bytes = frame.Encode();
            server.Write(bytes, 0, bytes.Length);
            return true;
        });

        static byte[] ReadExactly(Stream stream, int count)
        {
            var buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int chunk = stream.Read(buffer, read, count - read);
                if (chunk <= 0) throw new EndOfStreamException();
                read += chunk;
            }
            return buffer;
        }

        static IpcFrame ReadOnClient(WindowsPipeTransport transport)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < TimeoutMilliseconds)
            {
                if (transport.TryRead(out var frame)) return frame;
                Thread.Sleep(5);
            }
            Assert.Fail("the client got no frame");
            return default;
        }

        [Test]
        public void Open_MissingPipe_ReturnsNull() =>
            Assert.IsNull(WindowsPipeTransport.Open(pipeName + "-missing"));

        [Test]
        public void ServerProcessId_IsTheProcessThatCreatedThePipe()
        {
            using var process = Process.GetCurrentProcess();
            Assert.AreEqual((uint)process.Id, Connect().ServerProcessId);
        }

        [Test]
        public void Frames_TravelBothWays()
        {
            var transport = Connect();
            var receiving = ReceiveOnServer();
            transport.Write(new IpcFrame(Opcode.Handshake, "{\"v\":1,\"client_id\":\"é\"}"));
            var received = Await(receiving, "the server got no frame");
            Assert.AreEqual(Opcode.Handshake, received.Opcode);
            Assert.AreEqual("{\"v\":1,\"client_id\":\"é\"}", received.Json);

            Assert.IsFalse(transport.TryRead(out _));
            var sending = SendFromServer(new IpcFrame(Opcode.Frame, "{\"evt\":\"READY\"}"));
            var frame = ReadOnClient(transport);
            Await(sending, "the server could not write");
            Assert.AreEqual(Opcode.Frame, frame.Opcode);
            Assert.AreEqual("{\"evt\":\"READY\"}", frame.Json);
        }

        [Test]
        public void DetachThenAttach_KeepsTheConnection()
        {
            var transport = Connect();
            Assert.IsTrue(transport.TryDetach(out long handle));
            var resumed = WindowsPipeTransport.Attach(handle, transport.ServerProcessId);
            Assert.IsNotNull(resumed);
            cleanup.Add(resumed);
            var receiving = ReceiveOnServer();
            resumed.Write(new IpcFrame(Opcode.Frame, "{\"after\":\"reload\"}"));
            Assert.AreEqual("{\"after\":\"reload\"}", Await(receiving, "the server got no frame").Json);
        }

        [Test]
        public void Attach_RefusesAPipeFromAnotherServer()
        {
            var transport = Connect();
            uint serverId = transport.ServerProcessId;
            Assert.IsTrue(transport.TryDetach(out long handle));
            Assert.IsNull(WindowsPipeTransport.Attach(handle, serverId + 1));
            var owner = WindowsPipeTransport.Attach(handle, serverId);
            Assert.IsNotNull(owner);
            cleanup.Add(owner);
        }

        [Test]
        public void ClosedServer_ThrowsOnRead()
        {
            var transport = Connect();
            server.Dispose();
            Assert.Throws<IOException>(() => transport.TryRead(out _));
        }
    }
}
