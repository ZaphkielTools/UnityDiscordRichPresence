using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Zaphkiel.DiscordRichPresence.Tests
{
    sealed class IpcFrameTests
    {
        static List<IpcFrame> ReadInChunks(byte[] bytes, int chunkSize)
        {
            var stream = new MemoryStream(bytes);
            var reader = new FrameReader();
            var frames = new List<IpcFrame>();
            int delivered = 0;
            while (stream.Position < bytes.Length)
            {
                delivered = Math.Min(bytes.Length, delivered + chunkSize);
                int available = delivered - (int)stream.Position;
                while (reader.TryRead(stream, ref available, out var frame)) frames.Add(frame);
            }
            return frames;
        }

        [Test]
        public void Encode_WritesALittleEndianHeaderAndUtf8Payload() =>
            CollectionAssert.AreEqual(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 0xC3, 0xA9 }, new IpcFrame(Opcode.Frame, "é").Encode());

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(1000)]
        public void Reader_RebuildsFramesFromAnyChunkSize(int chunkSize)
        {
            var sent = new[] { new IpcFrame(Opcode.Frame, "{\"evt\":\"READY\"}"), new IpcFrame(Opcode.Ping, ""), new IpcFrame(Opcode.Close, "{\"code\":4000,\"message\":\"Scène\"}") };
            var frames = ReadInChunks(sent.SelectMany(f => f.Encode()).ToArray(), chunkSize);
            CollectionAssert.AreEqual(sent.Select(f => f.Opcode), frames.Select(f => f.Opcode));
            CollectionAssert.AreEqual(sent.Select(f => f.Json), frames.Select(f => f.Json));
        }

        [Test]
        public void Reader_ReportsAPartialFrame()
        {
            byte[] bytes = new IpcFrame(Opcode.Frame, "{}").Encode();
            var reader = new FrameReader();
            int available = 5;
            Assert.IsFalse(reader.TryRead(new MemoryStream(bytes), ref available, out _));
            Assert.IsTrue(reader.HasPartialFrame);
        }

        [TestCase(9, 0)]
        [TestCase(1, -1)]
        [TestCase(1, IpcFrame.MaxPayloadSize + 1)]
        public void Reader_RejectsInvalidHeaders(int opcode, int length)
        {
            var header = new byte[IpcFrame.HeaderSize];
            BitConverter.GetBytes(opcode).CopyTo(header, 0);
            BitConverter.GetBytes(length).CopyTo(header, 4);
            int available = header.Length;
            Assert.Throws<InvalidDataException>(() => new FrameReader().TryRead(new MemoryStream(header), ref available, out _));
        }

        [Test]
        public void Reader_ThrowsWhenTheStreamEndsEarly()
        {
            int available = IpcFrame.HeaderSize;
            Assert.Throws<EndOfStreamException>(() => new FrameReader().TryRead(new MemoryStream(new byte[4]), ref available, out _));
        }
    }
}
