using System.Text;

namespace OogL2Client.Networking;

public sealed record CharacterSelectionEntry(int Slot, string Name, int ObjectId, int ClassId, int Level, bool IsActive);

internal static class PacketParsing
{
    public static IReadOnlyList<int> ParseServerListIds(byte[] payload)
    {
        // Minimal parser for login ServerList packet.
        // Layout starts with: opcode(0x04), serverCount(byte), lastServer(byte), then server entries.
        if (payload.Length < 3 || payload[0] != 0x04)
        {
            return Array.Empty<int>();
        }

        var ids = new List<int>();
        var serverCount = payload[1];
        var pos = 3;

        for (var i = 0; i < serverCount; i++)
        {
            // This fixed width matches Mobius Interlude ServerList entry layout.
            if (pos + 21 > payload.Length)
            {
                break;
            }

            var id = payload[pos];
            ids.Add(id);
            pos += 21;
        }

        return ids;
    }

    public static bool TryParseLoginInit(byte[] payload, out int sessionId, out byte[] scrambledModulus, out byte[] blowfishKey)
    {
        sessionId = 0;
        scrambledModulus = Array.Empty<byte>();
        blowfishKey = Array.Empty<byte>();

        // Init layout: opcode(1) + sessionId(4) + protocol(4) + rsa(128) + 4 ints + blowfish(16) + 1 byte.
        if (payload.Length < 170 || payload[0] != 0x00)
        {
            return false;
        }

        sessionId = BitConverter.ToInt32(payload, 1);
        scrambledModulus = payload.AsSpan(9, 128).ToArray();
        blowfishKey = payload.AsSpan(153, 16).ToArray();
        return true;
    }

    public static IReadOnlyList<CharacterSelectionEntry> ParseCharSelectionInfo(byte[] payload)
    {
        var reader = new PacketReader(payload);
        var opcode = reader.ReadByte();
        if (opcode != L2MobiusPacketBuilder.CharSelectInfoOpcode)
        {
            return Array.Empty<CharacterSelectionEntry>();
        }

        var count = reader.ReadInt();
        var results = new List<CharacterSelectionEntry>(Math.Max(0, count));

        for (var slot = 0; slot < count; slot++)
        {
            var name = reader.ReadNullTerminatedL2String();
            var objectId = reader.ReadInt();
            _ = reader.ReadNullTerminatedL2String(); // account/login name
            _ = reader.ReadInt(); // session id echoed by server

            // Explicit field order from Mobius CharSelectionInfo.writeImpl.
            _ = reader.ReadInt(); // clan id
            _ = reader.ReadInt(); // unknown
            _ = reader.ReadInt(); // sex
            _ = reader.ReadInt(); // race
            var baseClassId = reader.ReadInt();
            _ = reader.ReadInt(); // active class marker
            _ = reader.ReadInt(); // unknown
            _ = reader.ReadInt(); // unknown
            _ = reader.ReadInt(); // unknown

            _ = reader.ReadDouble(); // current hp
            _ = reader.ReadDouble(); // current mp
            _ = reader.ReadInt(); // sp
            _ = reader.ReadLong(); // exp
            var level = reader.ReadInt();

            _ = reader.ReadInt(); // karma
            reader.Skip(9 * 4); // reserved ints
            reader.Skip(68); // paperdoll object ids
            reader.Skip(68); // paperdoll item ids
            reader.Skip(12); // hair style/color/face
            reader.Skip(16); // max hp/mp doubles
            reader.Skip(4); // delete timer
            var classId = reader.ReadInt();
            var isActive = reader.ReadInt() != 0;
            reader.Skip(1); // enchant effect
            reader.Skip(4); // augmentation id

            if (classId == 0)
            {
                classId = baseClassId;
            }

            results.Add(new CharacterSelectionEntry(slot, name, objectId, classId, level, isActive));
        }

        return results;
    }

    private sealed class PacketReader
    {
        private readonly byte[] _buffer;
        private int _position;

        public PacketReader(byte[] buffer)
        {
            _buffer = buffer;
        }

        public byte ReadByte()
        {
            Ensure(1);
            return _buffer[_position++];
        }

        public short ReadShort()
        {
            Ensure(2);
            var value = BitConverter.ToInt16(_buffer, _position);
            _position += 2;
            return value;
        }

        public int ReadInt()
        {
            Ensure(4);
            var value = BitConverter.ToInt32(_buffer, _position);
            _position += 4;
            return value;
        }

        public long ReadLong()
        {
            Ensure(8);
            var value = BitConverter.ToInt64(_buffer, _position);
            _position += 8;
            return value;
        }

        public double ReadDouble()
        {
            Ensure(8);
            var value = BitConverter.ToDouble(_buffer, _position);
            _position += 8;
            return value;
        }

        public string ReadNullTerminatedL2String()
        {
            var chars = new List<char>();
            while (true)
            {
                var ch = ReadShort();
                if (ch == 0)
                {
                    break;
                }

                chars.Add((char)ch);
            }

            return chars.Count == 0 ? string.Empty : new string(chars.ToArray());
        }

        public void Skip(int byteCount)
        {
            Ensure(byteCount);
            _position += byteCount;
        }

        private void Ensure(int byteCount)
        {
            if (_position + byteCount > _buffer.Length)
            {
                throw new InvalidOperationException("Packet parsing attempted to read beyond packet boundaries.");
            }
        }
    }
}