using System.Net.Sockets;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using OogL2Client.Models;
using OogL2Client.World;

namespace OogL2Client.Networking;

public enum ConnectionStage
{
    Disconnected,
    LoginConnected,
    GameConnected
}

public sealed record SessionStatus(ConnectionStage Stage, bool LoginAuthenticated, bool GameAuthenticated, bool InWorld);
public sealed record PlayerLocationUpdate(int ObjectId, string Name, int X, int Y, int Z, int Heading, byte Opcode, string SourceSummary);
public sealed record TargetUpdate(int ObjectId, string Name, int? Hp, int? MaxHp, byte Opcode, string SourceSummary);

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
    private IReadOnlyList<CharacterSelectionEntry> _lastCharacterEntries = Array.Empty<CharacterSelectionEntry>();
    private int _selectedCharacterObjectId;
    private string _selectedCharacterName = string.Empty;
    private readonly WorldState _worldState = new();
    private readonly Dictionary<int, LearnedSkillEntry> _learnedSkills = new();
    private int _currentTargetObjectId;
    private int? _currentTargetHp;
    private int? _currentTargetMaxHp;
    private int? _lastSelfX;
    private int? _lastSelfY;
    private int? _lastSelfZ;
    private int? _lastSelfHeading;

    public event Action<string>? MessageReceived;
    public event Action<IReadOnlyList<string>>? CharactersReceived;
    public event Action<IReadOnlyList<CharacterSelectionEntry>>? CharacterListReceived;
    public event Action<SessionStatus>? StatusChanged;
    public event Action<WorldPacketApplyResult>? WorldStateUpdated;
    public event Action<PlayerLocationUpdate>? PlayerLocationUpdated;
    public event Action<IReadOnlyList<LearnedSkillEntry>>? LearnedSkillsUpdated;
    public event Action<TargetUpdate>? TargetUpdated;

    public L2MobiusConnection(AccountProfile account)
    {
        _account = account;
    }

    public bool IsConnected => _client.Connected && _stream is not null;
    public ConnectionStage Stage { get; private set; } = ConnectionStage.Disconnected;
    public WorldState WorldState => _worldState;
    public bool IsLoginAuthenticated => _loginAuthenticated;
    public bool IsGameAuthenticated => _gameAuthenticated;
    public bool IsInWorld => _worldEnteredConfirmed;
    public bool HasPlayKeys => _playKey1 != 0 || _playKey2 != 0;
    public IReadOnlyList<LearnedSkillEntry> LearnedSkills => _learnedSkills.Values.OrderBy(s => s.SkillId).ToList();
    public int CurrentTargetObjectId => _currentTargetObjectId;
    public WorldObject? CurrentTarget => _currentTargetObjectId > 0 ? _worldState.Get(_currentTargetObjectId) : null;

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

    public async Task SendLoginRequestAsync(CancellationToken cancellationToken = default)
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
        await SendPayloadAsync(payload, encryptLogin: true, encryptGame: false, cancellationToken);
    }

    public async Task SendAuthGameGuardAsync(CancellationToken cancellationToken = default)
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
        await SendPayloadAsync(payload, encryptLogin: true, encryptGame: false, cancellationToken);
    }

    public async Task SendRequestServerListAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildRequestServerList(_loginKey1, _loginKey2));
        await SendPayloadAsync(payload, encryptLogin: true, encryptGame: false, cancellationToken);
    }

    public async Task SendSelectServerAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildRequestPlay(_account.ServerId, _loginKey1, _loginKey2));
        await SendPayloadAsync(payload, encryptLogin: true, encryptGame: false, cancellationToken);
    }

    public async Task SendGameAuthAsync(CancellationToken cancellationToken = default)
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
        await SendPayloadAsync(protocolPayload, encryptLogin: false, encryptGame: false, cancellationToken);
    }

    public async Task SendSelectCharacterAsync(int slot, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildGameCharacterSelect(slot));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
        _pendingEnterWorldAfterCharSelected = true;

        var selected = _lastCharacterEntries.FirstOrDefault(c => c.Slot == slot);
        if (selected is not null)
        {
            _selectedCharacterObjectId = selected.ObjectId;
            _selectedCharacterName = selected.Name;
            EnsureSelfTrackerSeed();
        }

        MessageReceived?.Invoke($"CharacterSelect sent for slot {slot}. Waiting for CharSelected before EnterWorld...");
    }

    public async Task SendEnterWorldAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildGameEnterWorld());
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
        _enterWorldSent = true;
        MessageReceived?.Invoke("EnterWorld sent. Waiting for in-game packet stream confirmation...");
    }

    public async Task SendMoveToLocationAsync(int x, int y, int z, int heading = 0, int originX = 0, int originY = 0, int originZ = 0, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildMoveToLocation(x, y, z, heading, originX, originY, originZ));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
    }

    public async Task SendStopMoveAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildStopMove());
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
    }

    public async Task SendAttackAsync(int targetObjectId, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var self = _worldState.Self;
        var originX = self?.X ?? 0;
        var originY = self?.Y ?? 0;
        var originZ = self?.Z ?? 0;
        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildAttack(targetObjectId, originX, originY, originZ));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
    }

    public async Task SendUseSkillAsync(int skillId, int targetObjectId, int ctrlPressed = 0, int shiftPressed = 0, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildUseSkill(skillId, ctrlPressed, shiftPressed));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
    }

    public async Task SendUseItemAsync(int objectId, int itemId, int targetObjectId = 0, int itemCount = 1, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildUseItem(objectId, itemId, targetObjectId, itemCount));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
    }

    public async Task SendActionAsync(int targetObjectId, int actionId, int actionType = 0, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var self = _worldState.Self;
        var originX = self?.X ?? 0;
        var originY = self?.Y ?? 0;
        var originZ = self?.Z ?? 0;
        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildAction(targetObjectId, originX, originY, originZ, (byte)Math.Clamp(actionId, 0, 255)));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
    }

    public async Task SendRequestTargetAsync(int targetObjectId, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var self = _worldState.Self;
        var originX = self?.X ?? 0;
        var originY = self?.Y ?? 0;
        var originZ = self?.Z ?? 0;
        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildAction(targetObjectId, originX, originY, originZ, 0));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
    }

    public async Task SendAssistTargetAsync(int targetObjectId, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildAssistTarget(targetObjectId));
        await SendPayloadAsync(payload, encryptLogin: false, encryptGame: true, cancellationToken);
    }

    public async Task SendPingAsync(CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var payload = ExtractPayload(L2MobiusPacketBuilder.BuildPingPacket());
        await SendPayloadAsync(payload, encryptLogin: Stage == ConnectionStage.LoginConnected, encryptGame: Stage == ConnectionStage.GameConnected, cancellationToken);
    }

    public async Task SendAsync(byte[] packet, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Stream not initialized.");
        }

        await _stream.WriteAsync(packet.AsMemory(0, packet.Length), cancellationToken == default ? _cts.Token : cancellationToken);
        MessageReceived?.Invoke($"Sent ({packet.Length} bytes): {Convert.ToHexString(packet)}");
    }

    private async Task SendPayloadAsync(byte[] payload, bool encryptLogin, bool encryptGame, CancellationToken cancellationToken = default)
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

        await SendAsync(L2MobiusPacketBuilder.BuildPacket(wirePayload), cancellationToken);
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
                    QueueSend(() => SendAuthGameGuardAsync(), "AuthGameGuard");
                }
                else
                {
                    MessageReceived?.Invoke("Init packet could not be parsed after decryption; login handshake cannot continue.");
                }
                break;
            case 0x0B: // GG_AUTH
                MessageReceived?.Invoke("GG_AUTH received. Sending RequestAuthLogin...");
                QueueSend(() => SendLoginRequestAsync(), "AuthLogin");
                break;
            case 0x03: // LOGIN_OK
                if (payload.Length >= 9)
                {
                    _loginKey1 = BitConverter.ToInt32(payload, 1);
                    _loginKey2 = BitConverter.ToInt32(payload, 5);
                    _loginAuthenticated = true;
                    PublishStatus();
                    MessageReceived?.Invoke($"Login OK. LoginKeys=({_loginKey1}, {_loginKey2}). Requesting server list...");
                    QueueSend(() => SendRequestServerListAsync(), "RequestServerList");
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

                QueueSend(() => SendSelectServerAsync(), "RequestServerLogin");
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
                _lastCharacterEntries = characters;
                var active = characters.FirstOrDefault(c => c.IsActive) ?? characters.FirstOrDefault();
                if (active is not null)
                {
                    _selectedCharacterObjectId = active.ObjectId;
                    _selectedCharacterName = active.Name;
                    EnsureSelfTrackerSeed();
                }

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
            QueueSend(() => SendEnterWorldAsync(), "EnterWorld");
            MessageReceived?.Invoke("CharSelected received. Sending EnterWorld now.");
        }

        if (_enterWorldSent && !_worldEnteredConfirmed && IsWorldStatePacket(opcode))
        {
            _worldEnteredConfirmed = true;
            PublishStatus();
            MessageReceived?.Invoke($"World entry confirmed by server packet 0x{opcode:X2}. Character is in-game server-side.");
        }

        if (opcode == 0x29 && PacketParsing.TryParseTargetSelected(payload, out var selectedTargetId))
        {
            SetCurrentTarget(selectedTargetId, opcode, "TargetSelected");
        }

        if (opcode == 0xA6 && PacketParsing.TryParseMyTargetSelected(payload, out var mySelectedTargetId))
        {
            SetCurrentTarget(mySelectedTargetId, opcode, "MyTargetSelected");
        }

        if (opcode == 0x2A)
        {
            ClearCurrentTarget(opcode, "TargetUnselected");
        }

        if (opcode == 0x0E && PacketParsing.TryParseStatusUpdate(payload, out var statusUpdate))
        {
            ApplyStatusUpdate(statusUpdate, opcode);
        }

        if (opcode == 0x58)
        {
            HandleSkillListPacket(payload);
        }

        if (WorldPacketParser.TryApply(_worldState, opcode, payload, _selectedCharacterObjectId, _selectedCharacterName, out var update) && update.Changed)
        {
            PromoteSelectedCharacterToSelfIfNeeded();
            WorldStateUpdated?.Invoke(update);
            PublishPlayerLocationIfChanged(opcode, update.Summary);
            if (update.ThreatChanged)
            {
                var self = _worldState.Self;
                if (self is not null)
                {
                    var threatCount = _worldState.ThreatsTargeting(self.ObjectId).Count();
                    MessageReceived?.Invoke($"Threat refresh: {threatCount} hostile target(s) currently focused on {self.Name} ({self.ObjectId}).");
                }
            }
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
        _lastCharacterEntries = Array.Empty<CharacterSelectionEntry>();
        _selectedCharacterObjectId = 0;
        _selectedCharacterName = string.Empty;
        _worldState.Clear();
        _learnedSkills.Clear();
        _currentTargetObjectId = 0;
        _currentTargetHp = null;
        _currentTargetMaxHp = null;
        _lastSelfX = null;
        _lastSelfY = null;
        _lastSelfZ = null;
        _lastSelfHeading = null;
    }

    private void ApplyStatusUpdate(StatusUpdatePacket statusUpdate, byte opcode)
    {
        if (statusUpdate.ObjectId <= 0)
        {
            return;
        }

        var tracked = _worldState.Get(statusUpdate.ObjectId);
        if (tracked is not null)
        {
            if (statusUpdate.CurrentHp.HasValue)
            {
                tracked.Hp = statusUpdate.CurrentHp.Value;
            }

            if (statusUpdate.MaxHp.HasValue)
            {
                tracked.MaxHp = statusUpdate.MaxHp.Value;
            }

            tracked.LastSeenUtc = DateTime.UtcNow;
            _worldState.Upsert(tracked);
        }

        if (statusUpdate.ObjectId != _currentTargetObjectId)
        {
            return;
        }

        _currentTargetHp = statusUpdate.CurrentHp ?? _currentTargetHp;
        _currentTargetMaxHp = statusUpdate.MaxHp ?? _currentTargetMaxHp;

        var targetName = tracked?.Name;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            targetName = $"Object {_currentTargetObjectId}";
        }

        TargetUpdated?.Invoke(new TargetUpdate(_currentTargetObjectId, targetName, _currentTargetHp, _currentTargetMaxHp, opcode, "StatusUpdate"));
    }

    private void SetCurrentTarget(int targetObjectId, byte opcode, string source)
    {
        if (targetObjectId <= 0)
        {
            return;
        }

        _currentTargetObjectId = targetObjectId;
        var target = _worldState.Get(targetObjectId);
        _currentTargetHp = target?.Hp > 0 ? target.Hp : null;
        _currentTargetMaxHp = target?.MaxHp > 0 ? target.MaxHp : null;
        var name = string.IsNullOrWhiteSpace(target?.Name) ? $"Object {targetObjectId}" : target!.Name;
        TargetUpdated?.Invoke(new TargetUpdate(targetObjectId, name, _currentTargetHp, _currentTargetMaxHp, opcode, source));
    }

    private void ClearCurrentTarget(byte opcode, string source)
    {
        _currentTargetObjectId = 0;
        _currentTargetHp = null;
        _currentTargetMaxHp = null;
        TargetUpdated?.Invoke(new TargetUpdate(0, string.Empty, null, null, opcode, source));
    }

    private void HandleSkillListPacket(byte[] payload)
    {
        var skills = PacketParsing.ParseSkillList(payload);
        if (skills.Count == 0)
        {
            return;
        }

        _learnedSkills.Clear();
        foreach (var skill in skills)
        {
            _learnedSkills[skill.SkillId] = skill;
        }

        MessageReceived?.Invoke($"SkillList parsed: {_learnedSkills.Count} learned skill(s).");
        LearnedSkillsUpdated?.Invoke(LearnedSkills);
    }

    private void PromoteSelectedCharacterToSelfIfNeeded()
    {
        if (_worldState.Self is not null)
        {
            return;
        }

        if (_selectedCharacterObjectId <= 0)
        {
            return;
        }

        var selected = _worldState.Get(_selectedCharacterObjectId);
        if (selected is null)
        {
            return;
        }

        selected.Name = string.IsNullOrWhiteSpace(selected.Name) ? _selectedCharacterName : selected.Name;
        selected.Type = WorldObjectType.Player;
        selected.Relation = WorldObjectRelation.Self;
        _worldState.SetSelf(selected);
        MessageReceived?.Invoke($"Self tracker attached to object {_selectedCharacterObjectId} ({selected.Name}).");
    }

    private void EnsureSelfTrackerSeed()
    {
        if (_worldState.Self is not null || _selectedCharacterObjectId <= 0)
        {
            return;
        }

        var placeholder = new WorldObject
        {
            ObjectId = _selectedCharacterObjectId,
            Name = string.IsNullOrWhiteSpace(_selectedCharacterName) ? $"Player {_selectedCharacterObjectId}" : _selectedCharacterName,
            Type = WorldObjectType.Player,
            Relation = WorldObjectRelation.Self,
            IsVisible = true,
            LastSeenUtc = DateTime.UtcNow
        };

        _worldState.SetSelf(placeholder);
        MessageReceived?.Invoke($"Self tracker seeded with character object {_selectedCharacterObjectId} ({placeholder.Name}). Waiting for world position packets...");
        PublishPlayerLocationIfChanged(0x00, "Self tracker seeded from character selection.");
    }

    private void PublishPlayerLocationIfChanged(byte opcode, string sourceSummary)
    {
        var self = _worldState.Self;
        if (self is null)
        {
            return;
        }

        var changed = !_lastSelfX.HasValue || !_lastSelfY.HasValue || !_lastSelfZ.HasValue || !_lastSelfHeading.HasValue ||
                      _lastSelfX.Value != self.X || _lastSelfY.Value != self.Y || _lastSelfZ.Value != self.Z || _lastSelfHeading.Value != self.Heading;
        if (!changed)
        {
            return;
        }

        _lastSelfX = self.X;
        _lastSelfY = self.Y;
        _lastSelfZ = self.Z;
        _lastSelfHeading = self.Heading;
        PlayerLocationUpdated?.Invoke(new PlayerLocationUpdate(self.ObjectId, self.Name, self.X, self.Y, self.Z, self.Heading, opcode, sourceSummary));
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
