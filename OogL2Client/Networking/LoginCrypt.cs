using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace OogL2Client.Networking;

internal sealed class LoginCrypt
{
    private static readonly byte[] StaticKey =
    {
        0x6B, 0x60, 0xCB, 0x5B, 0x82, 0xCE, 0x90, 0xB1,
        0xCC, 0x2B, 0x6C, 0x55, 0x6C, 0x6C, 0x6C, 0x6C
    };

    private readonly BlowfishEngine _staticDecrypt = new();
    private BlowfishEngine? _sessionDecrypt;
    private BlowfishEngine? _sessionEncrypt;
    private BlowfishCompatMode _mode = BlowfishCompatMode.Auto;

    public LoginCrypt()
    {
        _staticDecrypt.Init(false, new KeyParameter(StaticKey));
    }

    public void SetSessionKey(byte[] blowfishKey)
    {
        if (blowfishKey.Length < 16)
        {
            throw new ArgumentException("Blowfish key must have at least 16 bytes.", nameof(blowfishKey));
        }

        var key = blowfishKey.Take(16).ToArray();
        _sessionDecrypt = new BlowfishEngine();
        _sessionDecrypt.Init(false, new KeyParameter(key));

        _sessionEncrypt = new BlowfishEngine();
        _sessionEncrypt.Init(true, new KeyParameter(key));
    }

    public byte[] DecryptInitialPacket(byte[] encryptedPayload)
    {
        var standard = encryptedPayload.ToArray();
        ProcessBlocks(_staticDecrypt, standard, BlowfishCompatMode.Standard);
        ReverseXorPass(standard);
        if (LooksLikeInitPacket(standard))
        {
            _mode = BlowfishCompatMode.Standard;
            return standard;
        }

        var swapped = encryptedPayload.ToArray();
        ProcessBlocks(_staticDecrypt, swapped, BlowfishCompatMode.WordSwap32);
        ReverseXorPass(swapped);
        if (LooksLikeInitPacket(swapped))
        {
            _mode = BlowfishCompatMode.WordSwap32;
            return swapped;
        }

        // Fallback to standard output so logs can show bytes for debugging.
        _mode = BlowfishCompatMode.Standard;
        return standard;
    }

    public byte[] DecryptSessionPacket(byte[] encryptedPayload, out bool checksumOk)
    {
        if (_sessionDecrypt is null)
        {
            throw new InvalidOperationException("Session key not initialized.");
        }

        var data = encryptedPayload.ToArray();
        ProcessBlocks(_sessionDecrypt, data, _mode == BlowfishCompatMode.Auto ? BlowfishCompatMode.Standard : _mode);
        checksumOk = VerifyChecksum(data);
        return data;
    }

    public byte[] EncryptSessionPacket(byte[] payload)
    {
        if (_sessionEncrypt is null)
        {
            throw new InvalidOperationException("Session key not initialized.");
        }

        var totalLength = AlignTo8(payload.Length + 4);
        var data = new byte[totalLength];
        Array.Copy(payload, 0, data, 0, payload.Length);

        AppendChecksum(data);
        ProcessBlocks(_sessionEncrypt, data, _mode == BlowfishCompatMode.Auto ? BlowfishCompatMode.Standard : _mode);
        return data;
    }

    public static byte[] UnscrambleRsaModulus(byte[] scrambled)
    {
        var modulus = scrambled.ToArray();

        for (var i = 0; i < 64; i++)
        {
            modulus[64 + i] ^= modulus[i];
        }

        for (var i = 0; i < 4; i++)
        {
            modulus[13 + i] ^= modulus[52 + i];
        }

        for (var i = 0; i < 64; i++)
        {
            modulus[i] ^= modulus[64 + i];
        }

        for (var i = 0; i < 4; i++)
        {
            (modulus[i], modulus[77 + i]) = (modulus[77 + i], modulus[i]);
        }

        return modulus;
    }

    private static void ProcessBlocks(BlowfishEngine engine, byte[] data, BlowfishCompatMode mode)
    {
        var blocks = data.Length / 8;
        for (var i = 0; i < blocks; i++)
        {
            var offset = i * 8;
            if (mode == BlowfishCompatMode.WordSwap32)
            {
                var temp = new byte[8];
                Array.Copy(data, offset, temp, 0, 8);

                SwapWordEndian(temp, 0);
                SwapWordEndian(temp, 4);

                engine.ProcessBlock(temp, 0, temp, 0);

                SwapWordEndian(temp, 0);
                SwapWordEndian(temp, 4);
                Array.Copy(temp, 0, data, offset, 8);
            }
            else
            {
                engine.ProcessBlock(data, offset, data, offset);
            }
        }
    }

    private static bool LooksLikeInitPacket(byte[] payload)
    {
        // INIT opcode + expected constants from Mobius Init packet.
        return payload.Length >= 170
               && payload[0] == 0x00
               && BitConverter.ToInt32(payload, 5) == 50721;
    }

    private static void SwapWordEndian(byte[] buffer, int wordOffset)
    {
        (buffer[wordOffset], buffer[wordOffset + 3]) = (buffer[wordOffset + 3], buffer[wordOffset]);
        (buffer[wordOffset + 1], buffer[wordOffset + 2]) = (buffer[wordOffset + 2], buffer[wordOffset + 1]);
    }

    private static void ReverseXorPass(byte[] data)
    {
        if (data.Length < 12)
        {
            return;
        }

        var stop = data.Length - 8;
        if (stop < 4)
        {
            return;
        }

        var key = BitConverter.ToInt32(data, stop);
        for (var pos = stop - 4; pos >= 4; pos -= 4)
        {
            var encrypted = BitConverter.ToInt32(data, pos);
            var plain = encrypted ^ key;
            key -= plain;
            WriteInt(data, pos, plain);
        }
    }

    private static bool VerifyChecksum(byte[] data)
    {
        if (data.Length <= 4 || (data.Length & 0x03) != 0)
        {
            return false;
        }

        var checksumOffset = data.Length - 4;
        var xor = 0;
        for (var i = 0; i < checksumOffset; i += 4)
        {
            xor ^= BitConverter.ToInt32(data, i);
        }

        return xor == BitConverter.ToInt32(data, checksumOffset);
    }

    private static void AppendChecksum(byte[] data)
    {
        var checksumOffset = data.Length - 4;
        var xor = 0;
        for (var i = 0; i < checksumOffset; i += 4)
        {
            xor ^= BitConverter.ToInt32(data, i);
        }

        WriteInt(data, checksumOffset, xor);
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        var intBytes = BitConverter.GetBytes(value);
        Array.Copy(intBytes, 0, buffer, offset, 4);
    }

    private static int AlignTo8(int value)
    {
        var mod = value & 0x07;
        return mod == 0 ? value : value + (8 - mod);
    }

    private enum BlowfishCompatMode
    {
        Auto,
        Standard,
        WordSwap32
    }
}