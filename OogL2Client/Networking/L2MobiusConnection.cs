using System.Net.Sockets;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using OogL2Client.Models;

namespace OogL2Client.Networking;

public enum ConnectionStage
{
    Disconnected,
    LoginConnected,
    GameConnected
}

public sealed record SessionStatus(ConnectionStage Stage, bool LoginAuthenticated, bool GameAuthenticated, bool InWorld);

public sealed class L2MobiusConnection : IDisposable
{
    private static readonly byte[] GameKeyTail = { 0xC8, 0x27, 0x93, 0x01, 0xA1, 0x6C, 0x31, 0x97 };

    private readonly AccountProfile _account;
    private TcpClient _client = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly List<byte> _receiveBuffer = new();
    private readonly LoginCrypt _loginCrypt = new();
    private readonly GameCrypt _gameCrypt = new();
    private NetworkStream? _stream;
    private Task? _readerTask;
    private int _sessionId;
    private int _loginKey1;
    private int _loginKey2;
    private int _playKey1;
    private int _playKey2;
    private List<int> _serverIds = new();
    private byte[] _rsaModulus = Array.Empty<byte>();
    private bool _loginInitReceived;
    private bool _gamePacketEncryptionEnabled;
    private bool _pendingGameAuth;
    private bool _enterWorldSent;
    private bool _worldEnteredConfirmed;
    private bool _pendingEnterWorldAfterCharSelected;
    private bool _loginAuthenticated;
    private bool _gameAuthenticated;

    public event Action<string>? MessageReceived;
    public event Action<IReadOnlyList<string>>? CharactersReceived;
    public event Action<IReadOnlyList<CharacterSelectionEntry>>? CharacterListReceived;
    public event Action<SessionStatus>? StatusChanged;

    public L2MobiusConnection(AccountProfile account)
    {
        _account = account;
    }

    public bool IsConnected => _client.Connected && _stream is not null;
    public ConnectionStage Stage { get; private set; } = ConnectionStage.Disconnected;

    public Task ConnectAsync()
    {
        return ConnectLoginAsync();
    }

    public async Task ConnectLoginAsync()
    {
        if (IsConnected && Stage == ConnectionStage.LoginConnected)
        {
            return;
        }

        await DisconnectInternalAsync();
        ResetAuthState();
        _client = new TcpClient();

        await _client.ConnectAsync(_account.ServerHost, _account.LoginPort);
        _stream = _client.GetStream();
        Stage = ConnectionStage.LoginConnected;
        PublishStatus();

        MessageReceived?.Invoke($"Connected to {_account.ServerHost}:{_account.LoginPort}.");
        StartReader();
    }

    public async Task ConnectGameAsync()
    {
        if (IsConnected && Stage == ConnectionStage.GameConnected)
        {
            return;
        }

        await DisconnectInternalAsync(resetAuthState: false);
        _client = new TcpClient();

        await _client.ConnectAsync(_account.ServerHost, _account.GamePort);
        _stream = _client.GetStream();
        Stage = ConnectionStage.GameConnected;
        _pendingGameAuth = false;
        PublishStatus();

        MessageReceived?.Invoke($"Connected to game server {_account.ServerHost}:{_account.GamePort}.");
        StartReader();
    }

    public async Task SendLoginRequestAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        if (_sessionId == 0)
        {
            throw new InvalidOperationException("Session id not received yet (Init packet missing).");
        }

        if (_rsaModulus.Length != 128)
        {
            throw new InvalidOperationException("RSA modulus from Init packet is not available.");
        }

        var clearBlock = L2MobiusPacketBuilder.BuildAuthLoginCredentialBlock(_account.Username, _account.Password);
        var encryptedBlock = EncryptRsaNoPadding(clearBlock, _rsaModulus);
        var payload = L2MobiusPacketBuilder.BuildAuthLoginPayload(encryptedBlock);
        await SendPayloadAsync(payload, encryptLogin: true, encryptGame: false);
    }

    public async Task SendAuthGameGuardAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        if (_sessionId == 0)
        {
            throw new InvalidOperationException("Session id not available.");
        }

        var payload = L2MobiusPacketBuilder.BuildAuthGameGuardPayload(_sessionId);
        await SendPayloadAsync(payload, encryptLogin: true, encryptGame: false);
    }

    public async Task SendRequestServerListAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildRequestServerList(_loginKey1, _loginKey2));
        await SendPayloadAsync(payload, encryptLogin: true, encryptGame: false);
    }

    public async Task SendSelectServerAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildRequestPlay(_account.ServerId, _loginKey1, _loginKey2));
        await SendPayloadAsync(payload, encryptLogin: true, encryptGame: false);
    }

    public async Task SendGameAuthAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        if (_playKey1 == 0 && _playKey2 == 0)
        {
            throw new InvalidOperationException("Play keys not received yet. Complete login server flow first.");
        }

        _pendingGameAuth = true;
        var protocolPayload = ExtractPayload(L2MobiusPacketBuilder.BuildGameProtocolVersion(_account.ProtocolVersion));
        await SendPayloadAsync(protocolPayload, encryptLogin: false, encryptGame: false);
    }

    public async Task SendSelectCharacterAsync(int slot)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildGameCharacterSelect(slot));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true);
        _pendingEnterWorldAfterCharSelected = true;
        MessageReceived?.Invoke($"CharacterSelect sent for slot {slot}. Waiting for CharSelected before EnterWorld...");
    }

    public async Task SendEnterWorldAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildGameEnterWorld());
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true);
        _enterWorldSent = true;
        MessageReceived?.Invoke("EnterWorld sent. Waiting for in-game packet stream confirmation...");
    }

    public async Task SendPingAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildPingPacket());
        await SendPayloadAsync(payload, encryptLogin: Stage == ConnectionStage.LoginConnected, encryptGame: Stage == ConnectionStage.GameConnected);
    }

    public async Task SendAsync(byte[] packet)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Stream not initialized.");
        }

        await _stream.WriteAsync(packet.AsMemory(0, packet.Length), _cts.Token);
        MessageReceived?.Invoke($"Sent ({packet.Length} bytes): {Convert.ToHexString(packet)}");
    }

    private async Task SendPayloadAsync(byte[] payload, bool encryptLogin, bool encryptGame)
    {
        byte[] wirePayload = payload;

        if (encryptLogin && Stage == ConnectionStage.LoginConnected)
        {
            wirePayload = _loginCrypt.EncryptSessionPacket(payload);
        }
        else if (encryptGame && Stage == ConnectionStage.GameConnected && _gamePacketEncryptionEnabled)
        {
            wirePayload = _gameCrypt.Encrypt(payload);
        }

        await SendAsync(L2MobiusPacketBuilder.BuildPacket(wirePayload));
    }

    private void StartReader()
    {
        _readerTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        var buffer = new byte[4096];

        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = await _stream!.ReadAsync(buffer.AsMemory(0, buffer.Length), token);

                if (read == 0)
                {
                    break;
                }

                _receiveBuffer.AddRange(buffer.AsSpan(0, read).ToArray());
                ProcessReceiveBuffer();
            }
        }
        catch (OperationCanceledException)
        {
            // expected during shutdown
        }
        catch (Exception ex)
        {
            MessageReceived?.Invoke($"Socket read error: {ex.Message}");
        }
        finally
        {
            Stage = ConnectionStage.Disconnected;
            PublishStatus();
            MessageReceived?.Invoke("Connection closed.");
        }
    }

    private void ProcessReceiveBuffer()
    {
        while (_receiveBuffer.Count >= 2)
        {
            var expectedLength = BitConverter.ToUInt16(_receiveBuffer.ToArray(), 0);
            if (expectedLength < 2)
            {
                MessageReceived?.Invoke("Received malformed packet length.");
                _receiveBuffer.Clear();
                return;
            }

            if (_receiveBuffer.Count < expectedLength)
            {
                return;
            }

            var packet = _receiveBuffer.GetRange(0, expectedLength).ToArray();
            _receiveBuffer.RemoveRange(0, expectedLength);
            HandlePacket(packet);
        }
    }

    private void HandlePacket(byte[] packet)
    {
        if (packet.Length < 3)
        {
            MessageReceived?.Invoke($"Received short packet ({packet.Length} bytes). Hex: {Convert.ToHexString(packet)}");
            return;
        }

        var encryptedPayload = packet.AsSpan(2).ToArray();
        var payload = encryptedPayload;

        try
        {
            if (Stage == ConnectionStage.LoginConnected)
            {
                payload = DecodeLoginPayload(encryptedPayload);
            }
            else if (Stage == ConnectionStage.GameConnected && _gamePacketEncryptionEnabled)
            {
                payload = _gameCrypt.Decrypt(encryptedPayload);
            }
        }
        catch (Exception ex)
        {
            MessageReceived?.Invoke($"Packet decode error: {ex.Message}");
            return;
        }

        if (payload.Length == 0)
        {
            MessageReceived?.Invoke("Received empty payload packet.");
            return;
        }

        var opcode = payload[0];
        MessageReceived?.Invoke($"Received opcode 0x{opcode:X2} ({payload.Length} bytes payload): {Convert.ToHexString(payload)}");

        if (Stage == ConnectionStage.LoginConnected)
        {
            HandleLoginPacket(opcode, payload);
        }
        else if (Stage == ConnectionStage.GameConnected)
        {
            HandleGamePacket(opcode, payload);
        }
    }

    private byte[] DecodeLoginPayload(byte[] encryptedPayload)
    {
        if (!_loginInitReceived)
        {
            return _loginCrypt.DecryptInitialPacket(encryptedPayload);
        }

        var payload = _loginCrypt.DecryptSessionPacket(encryptedPayload, out var checksumOk);
        if (!checksumOk)
        {
            MessageReceived?.Invoke("Warning: invalid login packet checksum.");
        }

        return payload;
    }

    private void HandleLoginPacket(byte opcode, byte[] payload)
    {
        switch (opcode)
        {
            case 0x00: // INIT
                if (PacketParsing.TryParseLoginInit(payload, out var sessionId, out var scrambledModulus, out var blowfishKey))
                {
                    _sessionId = sessionId;
                    _rsaModulus = LoginCrypt.UnscrambleRsaModulus(scrambledModulus);
                    _loginCrypt.SetSessionKey(blowfishKey);
                    _loginInitReceived = true;
                    MessageReceived?.Invoke($"Login Init received. SessionId={_sessionId}. Sending AuthGameGuard...");
                    QueueSend(SendAuthGameGuardAsync, "AuthGameGuard");
                }
                else
                {
                    MessageReceived?.Invoke("Init packet could not be parsed after decryption; login handshake cannot continue.");
                }
                break;
            case 0x0B: // GG_AUTH
                MessageReceived?.Invoke("GG_AUTH received. Sending RequestAuthLogin...");
                QueueSend(SendLoginRequestAsync, "AuthLogin");
                break;
            case 0x03: // LOGIN_OK
                if (payload.Length >= 9)
                {
                    _loginKey1 = BitConverter.ToInt32(payload, 1);
                    _loginKey2 = BitConverter.ToInt32(payload, 5);
                    _loginAuthenticated = true;
                    PublishStatus();
                    MessageReceived?.Invoke($"Login OK. LoginKeys=({_loginKey1}, {_loginKey2}). Requesting server list...");
                    QueueSend(SendRequestServerListAsync, "RequestServerList");
                }
                break;
            case 0x04: // SERVER_LIST
                _serverIds = PacketParsing.ParseServerListIds(payload).ToList();
                if (_serverIds.Count == 0)
                {
                    MessageReceived?.Invoke("ServerList received, but no server ids could be parsed.");
                    break;
                }

                MessageReceived?.Invoke($"ServerList ids: {string.Join(", ", _serverIds)}");
                if (!_serverIds.Contains(_account.ServerId))
                {
                    var oldId = _account.ServerId;
                    _account.ServerId = _serverIds[0];
                    MessageReceived?.Invoke($"Configured ServerId {oldId} not present. Using ServerId {_account.ServerId} from ServerList.");
                }

                QueueSend(SendSelectServerAsync, "RequestServerLogin");
                break;
            case 0x07: // PLAY_OK
                if (payload.Length >= 9)
                {
                    _playKey1 = BitConverter.ToInt32(payload, 1);
                    _playKey2 = BitConverter.ToInt32(payload, 5);
                    MessageReceived?.Invoke($"Play OK. PlayKeys=({_playKey1}, {_playKey2}). Ready for game server auth.");
                }
                break;
            case 0x01:
                MessageReceived?.Invoke("LoginFail received.");
                break;
            case 0x06:
                var reason = payload.Length > 1 ? payload[1] : (byte)0;
                MessageReceived?.Invoke($"PlayFail received. Reason=0x{reason:X2} ({DescribePlayFailReason(reason)}).");
                break;
        }
    }

    private void HandleGamePacket(byte opcode, byte[] payload)
    {
        if (opcode == L2MobiusPacketBuilder.KeyPacketOpcode)
        {
            HandleGameKeyPacket(payload);
            return;
        }

        if (opcode == L2MobiusPacketBuilder.CharSelectInfoOpcode)
        {
            try
            {
                var characters = PacketParsing.ParseCharSelectionInfo(payload);
                if (characters.Count == 0)
                {
                    MessageReceived?.Invoke("Character selection info received, but no characters were parsed.");
                    return;
                }

                _gameAuthenticated = true;
                PublishStatus();
                CharacterListReceived?.Invoke(characters);
                var labels = characters.Select(c => $"{c.Slot}:{c.Name} (Lv {c.Level}, Class {c.ClassId})").ToList();
                CharactersReceived?.Invoke(labels);
                MessageReceived?.Invoke($"Parsed {characters.Count} character(s) from CharSelectInfo.");
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke($"CharSelectInfo parse error: {ex.Message}");
            }
        }

        if (opcode == 0x15 && _pendingEnterWorldAfterCharSelected)
        {
            _pendingEnterWorldAfterCharSelected = false;
            QueueSend(SendEnterWorldAsync, "EnterWorld");
            MessageReceived?.Invoke("CharSelected received. Sending EnterWorld now.");
        }

        if (_enterWorldSent && !_worldEnteredConfirmed && IsWorldStatePacket(opcode))
        {
            _worldEnteredConfirmed = true;
            PublishStatus();
            MessageReceived?.Invoke($"World entry confirmed by server packet 0x{opcode:X2}. Character is in-game server-side.");
        }
    }

    private static bool IsWorldStatePacket(byte opcode)
    {
        // Ignore pre-world handshake packets and confirm on first gameplay-state packet.
        return opcode is not 0x00 and not 0x13;
    }

    private void HandleGameKeyPacket(byte[] payload)
    {
        if (payload.Length < 23)
        {
            MessageReceived?.Invoke("KeyPacket payload too short.");
            return;
        }

        var result = payload[1];
        var keyPrefix = payload.AsSpan(2, 8).ToArray();
        _gamePacketEncryptionEnabled = BitConverter.ToInt32(payload, 10) != 0;
        var serverId = BitConverter.ToInt32(payload, 14);

        var key = new byte[16];
        Array.Copy(keyPrefix, 0, key, 0, 8);
        Array.Copy(GameKeyTail, 0, key, 8, 8);
        _gameCrypt.SetKey(key);

        if (_gamePacketEncryptionEnabled)
        {
            _gameCrypt.Activate();
        }

        MessageReceived?.Invoke($"KeyPacket received. Result={result}, ServerId={serverId}, PacketEncryption={_gamePacketEncryptionEnabled}.");

        if (_pendingGameAuth)
        {
            _pendingGameAuth = false;
            QueueSend(async () =>
            {
                var authPayload = ExtractPayload(L2MobiusPacketBuilder.BuildGameAuthLogin(_account.Username, _playKey1, _playKey2, _loginKey1, _loginKey2));
                await SendPayloadAsync(authPayload, encryptLogin: false, encryptGame: true);
            }, "GameAuthLogin");
        }
    }

    private static byte[] EncryptRsaNoPadding(byte[] plainBlock, byte[] modulus)
    {
        if (plainBlock.Length != 128)
        {
            throw new ArgumentException("RSA payload must be exactly 128 bytes.", nameof(plainBlock));
        }

        var rsa = new RsaEngine();
        var key = new RsaKeyParameters(false, new BigInteger(1, modulus), BigInteger.ValueOf(65537));
        rsa.Init(true, key);

        var encrypted = rsa.ProcessBlock(plainBlock, 0, plainBlock.Length);
        if (encrypted.Length == 128)
        {
            return encrypted;
        }

        // Ensure fixed key size by left-padding if needed.
        var padded = new byte[128];
        var copyOffset = padded.Length - encrypted.Length;
        Array.Copy(encrypted, 0, padded, copyOffset, encrypted.Length);
        return padded;
    }

    private void QueueSend(Func<Task> action, string name)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke($"{name} send failed: {ex.Message}");
            }
        });
    }

    private static byte[] ExtractPayload(byte[] framedPacket)
    {
        return framedPacket.AsSpan(2).ToArray();
    }

    private static string DescribePlayFailReason(byte reason)
    {
        return reason switch
        {
            0x0F => "Server overloaded or requested server id is not available",
            0x10 => "Server under maintenance",
            0x15 => "Access failed",
            _ => "See Mobius PlayFailReason enum"
        };
    }

    private void ResetAuthState()
    {
        _sessionId = 0;
        _loginKey1 = 0;
        _loginKey2 = 0;
        _playKey1 = 0;
        _playKey2 = 0;
        _serverIds.Clear();
        _rsaModulus = Array.Empty<byte>();
        _loginInitReceived = false;
        _gamePacketEncryptionEnabled = false;
        _pendingGameAuth = false;
        _enterWorldSent = false;
        _worldEnteredConfirmed = false;
        _pendingEnterWorldAfterCharSelected = false;
        _loginAuthenticated = false;
        _gameAuthenticated = false;
    }

    private void PublishStatus()
    {
        StatusChanged?.Invoke(new SessionStatus(Stage, _loginAuthenticated, _gameAuthenticated, _worldEnteredConfirmed));
    }

    private async Task DisconnectInternalAsync(bool resetAuthState = true)
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }

        if (_client.Connected)
        {
            _client.Close();
        }

        _client.Dispose();

        _receiveBuffer.Clear();
        if (resetAuthState)
        {
            ResetAuthState();
        }
        Stage = ConnectionStage.Disconnected;
        PublishStatus();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _stream?.Dispose();
        _client.Dispose();
        ResetAuthState();
        Stage = ConnectionStage.Disconnected;
        PublishStatus();
    }
}
