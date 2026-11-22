using System;
using System.IO;
using System.Text;

namespace Zaphkiel.DiscordRichPresence
{
    internal enum Opcode
    {
        Handshake = 0,
        Frame = 1,
        Close = 2,
        Ping = 3,
        Pong = 4,
    }

    internal readonly struct IpcFrame
    {
        public const int HeaderSize = 8;
        public const int MaxPayloadSize = 1 << 20;

        public readonly Opcode Opcode;
        public readonly string Json;

        public IpcFrame(Opcode opcode, string json)
        {
            Opcode = opcode;
            Json = json ?? "";
        }

        public byte[] Encode()
        {
            byte[] payload = Encoding.UTF8.GetBytes(Json);
            var bytes = new byte[HeaderSize + payload.Length];
            WriteInt32(bytes, 0, (int)Opcode);
            WriteInt32(bytes, 4, payload.Length);
            Buffer.BlockCopy(payload, 0, bytes, HeaderSize, payload.Length);
            return bytes;
        }

        public static void ParseHeader(byte[] header, out Opcode opcode, out int length)
        {
            int code = ReadInt32(header, 0);
            length = ReadInt32(header, 4);
            if (code < (int)Opcode.Handshake || code > (int)Opcode.Pong) throw new InvalidDataException($"Unknown IPC opcode {code}.");
            if (length < 0 || length > MaxPayloadSize) throw new InvalidDataException($"Invalid IPC frame length {length}.");
            opcode = (Opcode)code;
        }

        static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        static int ReadInt32(byte[] buffer, int offset) =>
            buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16 | buffer[offset + 3] << 24;
    }

    internal sealed class FrameReader
    {
        readonly byte[] header = new byte[IpcFrame.HeaderSize];
        int headerRead;
        byte[] payload;
        int payloadRead;
        Opcode opcode;

        public bool HasPartialFrame => headerRead > 0;

        public bool TryRead(Stream stream, ref int available, out IpcFrame frame)
        {
            frame = default;
            if (headerRead < IpcFrame.HeaderSize)
            {
                headerRead += Fill(stream, header, headerRead, IpcFrame.HeaderSize - headerRead, ref available);
                if (headerRead < IpcFrame.HeaderSize) return false;
                IpcFrame.ParseHeader(header, out opcode, out int length);
                payload = new byte[length];
                payloadRead = 0;
            }
            payloadRead += Fill(stream, payload, payloadRead, payload.Length - payloadRead, ref available);
            if (payloadRead < payload.Length) return false;
            frame = new IpcFrame(opcode, Encoding.UTF8.GetString(payload));
            headerRead = 0;
            payload = null;
            return true;
        }

        static int Fill(Stream stream, byte[] buffer, int offset, int count, ref int available)
        {
            count = Math.Min(count, available);
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, offset + total, count - total);
                if (read <= 0) throw new EndOfStreamException("Discord closed the connection.");
                total += read;
            }
            available -= total;
            return total;
        }
    }
}
