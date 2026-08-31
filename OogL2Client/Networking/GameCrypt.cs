namespace OogL2Client.Networking;

internal sealed class GameCrypt
{
    private readonly byte[] _inKey = new byte[16];
    private readonly byte[] _outKey = new byte[16];
    private bool _enabled;

    public bool IsEnabled => _enabled;

    public void SetKey(byte[] key)
    {
        if (key.Length < 16)
        {
            throw new ArgumentException("Encryption key must contain at least 16 bytes.", nameof(key));
        }

        Array.Copy(key, 0, _inKey, 0, 16);
        Array.Copy(key, 0, _outKey, 0, 16);
        _enabled = false;
    }

    public void Activate()
    {
        _enabled = true;
    }

    public byte[] Encrypt(byte[] payload)
    {
        if (!_enabled || payload.Length == 0)
        {
            return payload;
        }

        var data = payload.ToArray();
        var previous = 0;
        for (var i = 0; i < data.Length; i++)
        {
            var current = data[i] & 0xFF;
            previous = current ^ (_outKey[i & 0x0F] & 0xFF) ^ previous;
            data[i] = (byte)previous;
        }

        AdvanceOffset(_outKey, data.Length);
        return data;
    }

    public byte[] Decrypt(byte[] payload)
    {
        if (!_enabled || payload.Length == 0)
        {
            return payload;
        }

        var data = payload.ToArray();
        var previous = 0;
        for (var i = 0; i < data.Length; i++)
        {
            var current = data[i] & 0xFF;
            data[i] = (byte)(current ^ (_inKey[i & 0x0F] & 0xFF) ^ previous);
            previous = current;
        }

        AdvanceOffset(_inKey, data.Length);
        return data;
    }

    private static void AdvanceOffset(byte[] key, int delta)
    {
        var offset = (key[8] & 0xFF) |
                     ((key[9] & 0xFF) << 8) |
                     ((key[10] & 0xFF) << 16) |
                     ((key[11] & 0xFF) << 24);
        offset += delta;
        key[8] = (byte)(offset & 0xFF);
        key[9] = (byte)((offset >> 8) & 0xFF);
        key[10] = (byte)((offset >> 16) & 0xFF);
        key[11] = (byte)((offset >> 24) & 0xFF);
    }
}