using System.ComponentModel;
using OogL2Client.Models;
using OogL2Client.Networking;
using OogL2Client.World;

namespace OogL2Client;

public sealed class MainForm : Form
{
    private readonly BindingList<AccountProfile> _accounts = new();
    private readonly BindingList<CharacterSelectionEntry> _characters = new();
    private readonly TextBox _serverHostText;
    private readonly TextBox _loginPortText;
    private readonly TextBox _gamePortText;
    private readonly TextBox _protocolText;
    private readonly TextBox _characterSlotText;
    private readonly TextBox _usernameText;
    private readonly TextBox _passwordText;
    private readonly TextBox _serverIdText;
    private readonly TextBox _moveXText;
    private readonly TextBox _moveYText;
    private readonly TextBox _moveZText;
    private readonly TextBox _targetIdText;
    private readonly TextBox _skillIdText;
    private readonly TextBox _itemIdText;
    private readonly TextBox _itemObjectText;
    private readonly TextBox _actionIdText;
    private readonly ListBox _accountList;
    private readonly RichTextBox _logText;
    private readonly Button _addAccountButton;
    private readonly Button _connectButton;
    private readonly Button _listCharactersButton;
    private readonly Button _enterGameButton;
    private readonly Button _disconnectButton;
    private readonly Button _moveDebugButton;
    private readonly Button _stopMoveDebugButton;
    private readonly Button _attackDebugButton;
    private readonly Button _skillDebugButton;
    private readonly Button _useItemDebugButton;
    private readonly Button _targetDebugButton;
    private readonly Button _assistDebugButton;
    private readonly ComboBox _characterCombo;
    private readonly Label _loginStateLabel;
    private readonly Label _gameStateLabel;
    private readonly Label _worldStateLabel;
    private readonly Label _playerLocationLabel;
    private readonly Label _playerLocationMetaLabel;
    private readonly PictureBox _minimapBox;
    private readonly SplitContainer _worldPanel;
    private readonly WorldState _worldState = new();
    private readonly MinimapRenderer _minimapRenderer;
    private L2MobiusConnection? _session;
    private string _lastThreatSummary = string.Empty;

    public MainForm()
    {
        var mapsDirectory = ResolveMapsDirectory();
        _minimapRenderer = new MinimapRenderer(mapsDirectory);

        Text = "OOG L2 Client";
        Size = new Size(1000, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 24, 35);
        ForeColor = Color.White;

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 1,
            BackColor = BackColor
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));

        var leftPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = BackColor,
            Padding = new Padding(10)
        };

        var titleLabel = new Label
        {
            Text = "Account Manager",
            Font = new Font(FontFamily.GenericSansSerif, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };

        leftPanel.Controls.Add(titleLabel);

        _usernameText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5) };
        _passwordText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5), UseSystemPasswordChar = true };
        _serverHostText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5), Text = "127.0.0.1" };
        _loginPortText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5), Text = "2106" };
        _gamePortText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5), Text = "7777" };
        _serverIdText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5), Text = "1" };
        _protocolText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5), Text = "746" };
        _characterSlotText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5), Text = "0" };

        var accountFields = new[]
        {
            CreateField("Username", _usernameText),
            CreateField("Password", _passwordText),
            CreateField("Login Server", _serverHostText),
            CreateField("Login Port", _loginPortText),
            CreateField("Game Port", _gamePortText),
            CreateField("Server ID", _serverIdText),
            CreateField("Protocol", _protocolText),
            CreateField("Character Slot", _characterSlotText)
        };

        foreach (var box in accountFields)
        {
            leftPanel.Controls.Add(box);
        }

        _addAccountButton = new Button
        {
            Text = "Add account",
            Width = 120,
            Margin = new Padding(0, 12, 0, 0)
        };
        _addAccountButton.Click += AddAccountButton_Click;
        leftPanel.Controls.Add(_addAccountButton);

        _accountList = new ListBox
        {
            Width = 260,
            Height = 200,
            Margin = new Padding(0, 20, 0, 0),
            DataSource = _accounts,
            DisplayMember = nameof(AccountProfile.Username)
        };
        leftPanel.Controls.Add(_accountList);

        _characterCombo = new ComboBox
        {
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 10, 0, 0),
            DataSource = _characters,
            DisplayMember = nameof(CharacterSelectionEntry.Name)
        };
        _characterCombo.SelectedIndexChanged += CharacterCombo_SelectedIndexChanged;
        leftPanel.Controls.Add(CreateField("Character", _characterCombo));

        var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };

        _minimapBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.StretchImage,
            Margin = new Padding(0)
        };

        _logText = new RichTextBox
        {
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen,
            Font = new Font(FontFamily.GenericMonospace, 10f),
            BorderStyle = BorderStyle.FixedSingle
        };

        _worldPanel = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = BackColor,
            SplitterWidth = 8,
            FixedPanel = FixedPanel.None,
            Panel2MinSize = 120
        };
        _worldPanel.Panel1.BackColor = BackColor;
        _worldPanel.Panel2.BackColor = BackColor;
        _worldPanel.Panel1.Controls.Add(_logText);
        _worldPanel.Panel2.Controls.Add(_minimapBox);
        _worldPanel.Resize += (_, _) => ConfigureWorldPanelSplit();
        Shown += (_, _) => ConfigureWorldPanelSplit();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 10)
        };

        _connectButton = new Button { Text = "Connect", Width = 120, Margin = new Padding(0, 0, 8, 0) };
        _connectButton.Click += ConnectButton_Click;

        _listCharactersButton = new Button { Text = "List Characters", Width = 140, Margin = new Padding(0, 0, 8, 0) };
        _listCharactersButton.Click += ListCharactersButton_Click;

        _enterGameButton = new Button { Text = "Enter Game", Width = 120, Margin = new Padding(0, 0, 8, 0) };
        _enterGameButton.Click += EnterGameButton_Click;

        _disconnectButton = new Button { Text = "Disconnect", Width = 120 };
        _disconnectButton.Click += DisconnectButton_Click;

        buttonPanel.Controls.Add(_connectButton);
        buttonPanel.Controls.Add(_listCharactersButton);
        buttonPanel.Controls.Add(_enterGameButton);
        buttonPanel.Controls.Add(_disconnectButton);

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 6)
        };

        _loginStateLabel = CreateStatusLabel("Login: OFF");
        _gameStateLabel = CreateStatusLabel("Game: OFF");
        _worldStateLabel = CreateStatusLabel("World: OFF");
        statusPanel.Controls.Add(_loginStateLabel);
        statusPanel.Controls.Add(_gameStateLabel);
        statusPanel.Controls.Add(_worldStateLabel);

        var locationPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 42,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 4)
        };

        _playerLocationLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.LightSkyBlue,
            Font = new Font(FontFamily.GenericMonospace, 10f, FontStyle.Bold),
            Text = "Player: waiting for packets..."
        };
        _playerLocationMetaLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.LightGray,
            Font = new Font(FontFamily.GenericMonospace, 8f),
            Text = "Source: n/a"
        };
        locationPanel.Controls.Add(_playerLocationLabel);
        locationPanel.Controls.Add(_playerLocationMetaLabel);

        _moveXText = new TextBox { Width = 70, Text = "150000" };
        _moveYText = new TextBox { Width = 70, Text = "150000" };
        _moveZText = new TextBox { Width = 70, Text = "0" };
        _targetIdText = new TextBox { Width = 80, Text = "0" };
        _skillIdText = new TextBox { Width = 80, Text = "1001" };
        _itemIdText = new TextBox { Width = 80, Text = "57" };
        _itemObjectText = new TextBox { Width = 80, Text = "0" };
        _actionIdText = new TextBox { Width = 80, Text = "0" };

        var debugPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 8)
        };

        debugPanel.Controls.Add(CreateDebugField("X", _moveXText));
        debugPanel.Controls.Add(CreateDebugField("Y", _moveYText));
        debugPanel.Controls.Add(CreateDebugField("Z", _moveZText));
        debugPanel.Controls.Add(CreateDebugField("Target", _targetIdText));
        debugPanel.Controls.Add(CreateDebugField("Skill", _skillIdText));
        debugPanel.Controls.Add(CreateDebugField("Item", _itemIdText));
        debugPanel.Controls.Add(CreateDebugField("ItemObj", _itemObjectText));
        debugPanel.Controls.Add(CreateDebugField("Action", _actionIdText));

        _moveDebugButton = new Button { Text = "Move", Width = 90, Margin = new Padding(0, 0, 6, 0) };
        _stopMoveDebugButton = new Button { Text = "Stop", Width = 90, Margin = new Padding(0, 0, 6, 0) };
        _attackDebugButton = new Button { Text = "Attack", Width = 90, Margin = new Padding(0, 0, 6, 0) };
        _skillDebugButton = new Button { Text = "Skill", Width = 90, Margin = new Padding(0, 0, 6, 0) };
        _useItemDebugButton = new Button { Text = "Use Item", Width = 96, Margin = new Padding(0, 0, 6, 0) };
        _targetDebugButton = new Button { Text = "Target", Width = 90, Margin = new Padding(0, 0, 6, 0) };
        _assistDebugButton = new Button { Text = "Assist", Width = 90, Margin = new Padding(0, 0, 6, 0) };

        _moveDebugButton.Click += MoveDebugButton_Click;
        _stopMoveDebugButton.Click += StopMoveDebugButton_Click;
        _attackDebugButton.Click += AttackDebugButton_Click;
        _skillDebugButton.Click += SkillDebugButton_Click;
        _useItemDebugButton.Click += UseItemDebugButton_Click;
        _targetDebugButton.Click += TargetDebugButton_Click;
        _assistDebugButton.Click += AssistDebugButton_Click;

        debugPanel.Controls.Add(_moveDebugButton);
        debugPanel.Controls.Add(_stopMoveDebugButton);
        debugPanel.Controls.Add(_attackDebugButton);
        debugPanel.Controls.Add(_skillDebugButton);
        debugPanel.Controls.Add(_useItemDebugButton);
        debugPanel.Controls.Add(_targetDebugButton);
        debugPanel.Controls.Add(_assistDebugButton);

        rightPanel.Controls.Add(_worldPanel);
        rightPanel.Controls.Add(locationPanel);
        rightPanel.Controls.Add(statusPanel);
        rightPanel.Controls.Add(debugPanel);
        rightPanel.Controls.Add(buttonPanel);
        statusPanel.BringToFront();
        debugPanel.BringToFront();
        buttonPanel.BringToFront();

        table.Controls.Add(leftPanel, 0, 0);
        table.Controls.Add(rightPanel, 1, 0);
        Controls.Add(table);

        SeedExampleWorld();
        RefreshMinimap();

        AppendLog("OOG L2 Client ready.");
        AppendLog($"Minimap maps source: {mapsDirectory}");
        AppendLog("This is a protocol-learning shell for a private L2J Mobius server.");
        AppendLog("Protocol default is 746. Add account, click Connect, then List Characters.");
        UpdateStatus(new SessionStatus(ConnectionStage.Disconnected, false, false, false));
    }

    private static string ResolveMapsDirectory()
    {
        var localRuntimePath = Path.Combine(AppContext.BaseDirectory, "Maps");
        if (Directory.Exists(localRuntimePath))
        {
            return localRuntimePath;
        }

        var localProjectPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "OogL2Client", "Maps");
        var fullProjectPath = Path.GetFullPath(localProjectPath);
        if (Directory.Exists(fullProjectPath))
        {
            return fullProjectPath;
        }

        return @"D:\L2Adrenaline\Maps";
    }

    private void ConfigureWorldPanelSplit()
    {
        var width = _worldPanel.Width;
        if (width <= 0)
        {
            return;
        }

        var minDistance = _worldPanel.Panel1MinSize;
        var maxDistance = width - _worldPanel.Panel2MinSize;
        if (maxDistance <= minDistance)
        {
            return;
        }

        var desiredDistance = (int)(width * 0.58f);
        var splitDistance = Math.Clamp(desiredDistance, minDistance, maxDistance);
        _worldPanel.SplitterDistance = splitDistance;
    }

    private static Label CreateStatusLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Color.LightGray,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(6, 4, 6, 4),
            Margin = new Padding(0, 0, 8, 0)
        };
    }

    private static Control CreateField(string labelText, Control input)
    {
        var panel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.White,
            Margin = new Padding(0, 7, 8, 0)
        };

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(input, 1, 0);
        return panel;
    }

    private static Control CreateDebugField(string labelText, Control input)
    {
        var panel = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = Color.White,
            Margin = new Padding(0, 5, 4, 0)
        };

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(input, 1, 0);
        return panel;
    }

    private void AddAccountButton_Click(object? sender, EventArgs e)
    {
        var username = _usernameText.Text.Trim();
        var password = _passwordText.Text.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            AppendLog("Username and password are required.");
            return;
        }

        var profile = new AccountProfile
        {
            Username = username,
            Password = password,
            ServerHost = _serverHostText.Text.Trim(),
            LoginPort = int.TryParse(_loginPortText.Text, out var port) ? port : 2106,
            GamePort = int.TryParse(_gamePortText.Text, out var gamePort) ? gamePort : 7777,
            ServerId = int.TryParse(_serverIdText.Text, out var serverId) ? serverId : 1
            ,
            ProtocolVersion = int.TryParse(_protocolText.Text, out var protocolVersion) ? protocolVersion : 746,
            CharacterSlot = int.TryParse(_characterSlotText.Text, out var slot) ? slot : 0
        };

        _accounts.Add(profile);
        AppendLog($"Account added: {profile.Username} ({profile.ServerHost}:{profile.LoginPort})");

        _usernameText.Clear();
        _passwordText.Clear();
    }

    private L2MobiusConnection GetOrCreateSession(AccountProfile account)
    {
        if (_session is null)
        {
            _session = new L2MobiusConnection(account);
            _session.MessageReceived += AppendLog;
            _session.CharactersReceived += names => AppendLog($"Character candidates: {string.Join(", ", names)}");
            _session.CharacterListReceived += OnCharacterListReceived;
            _session.StatusChanged += UpdateStatus;
            _session.WorldStateUpdated += OnWorldStateUpdated;
            _session.PlayerLocationUpdated += OnPlayerLocationUpdated;
        }

        return _session;
    }

    private void OnWorldStateUpdated(WorldPacketApplyResult update)
    {
        RefreshMinimap();

        if (!update.ThreatChanged || _session is null)
        {
            return;
        }

        var self = _session.WorldState.Self;
        if (self is null)
        {
            return;
        }

        var threats = _session.WorldState
            .ThreatsTargeting(self.ObjectId)
            .Select(t => $"{t.Name}({t.ObjectId})")
            .Take(5)
            .ToList();

        var summary = threats.Count == 0
            ? "No current threats on self."
            : $"Threats on self: {string.Join(", ", threats)}";

        if (!string.Equals(summary, _lastThreatSummary, StringComparison.Ordinal))
        {
            _lastThreatSummary = summary;
            AppendLog(summary);
        }
    }

    private void OnPlayerLocationUpdated(PlayerLocationUpdate update)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<PlayerLocationUpdate>(OnPlayerLocationUpdated), update);
            return;
        }

        _playerLocationLabel.Text = $"Player {update.Name} ({update.ObjectId}) X:{update.X} Y:{update.Y} Z:{update.Z} H:{update.Heading}";
        _playerLocationMetaLabel.Text = $"Source: 0x{update.Opcode:X2} {update.SourceSummary}";
    }

    private void OnCharacterListReceived(IReadOnlyList<CharacterSelectionEntry> characters)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<IReadOnlyList<CharacterSelectionEntry>>(OnCharacterListReceived), characters);
            return;
        }

        _characters.Clear();
        foreach (var character in characters)
        {
            _characters.Add(character);
        }

        var active = characters.FirstOrDefault(c => c.IsActive) ?? characters.FirstOrDefault();
        if (active is not null)
        {
            var index = _characters.IndexOf(active);
            if (index >= 0)
            {
                _characterCombo.SelectedIndex = index;
            }

            _characterSlotText.Text = active.Slot.ToString();
        }
    }

    private void CharacterCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_characterCombo.SelectedItem is CharacterSelectionEntry character)
        {
            _characterSlotText.Text = character.Slot.ToString();
            if (_accountList.SelectedItem is AccountProfile selected)
            {
                selected.CharacterSlot = character.Slot;
            }
        }
    }

    private void UpdateStatus(SessionStatus status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<SessionStatus>(UpdateStatus), status);
            return;
        }

        var loginOn = status.Stage == ConnectionStage.LoginConnected || status.LoginAuthenticated;
        var gameOn = status.Stage == ConnectionStage.GameConnected || status.GameAuthenticated;
        var worldOn = status.InWorld;

        SetStatusLabel(_loginStateLabel, "Login", loginOn);
        SetStatusLabel(_gameStateLabel, "Game", gameOn);
        SetStatusLabel(_worldStateLabel, "World", worldOn);
    }

    private static void SetStatusLabel(Label label, string title, bool isOn)
    {
        label.Text = $"{title}: {(isOn ? "ON" : "OFF")}";
        label.ForeColor = isOn ? Color.LawnGreen : Color.LightGray;
    }

    private void SeedExampleWorld()
    {
        _worldState.Clear();
        _worldState.SetSelf(new WorldObject
        {
            ObjectId = 1,
            Name = "Self",
            Type = WorldObjectType.Player,
            // Region 14_24 has detailed map content in this map pack.
            X = -196608,
            Y = 196608,
            Z = 0,
            IsAlive = true,
            Relation = WorldObjectRelation.Self,
            IsVisible = true,
            LastSeenUtc = DateTime.UtcNow
        });

        var offsets = new[]
        {
            (1000, 200, WorldObjectType.Monster, "Wolf"),
            (-800, 1500, WorldObjectType.Monster, "Skeleton"),
            (1500, -700, WorldObjectType.Player, "Ally"),
            (-1200, -900, WorldObjectType.NPC, "Merchant"),
            (700, 1200, WorldObjectType.Item, "Herb")
        };

        for (var i = 0; i < offsets.Length; i++)
        {
            var (dx, dy, type, name) = offsets[i];
            _worldState.Upsert(new WorldObject
            {
                ObjectId = 100 + i,
                Name = name,
                Type = type,
                X = -196608 + dx,
                Y = 196608 + dy,
                Z = 0,
                IsAlive = true,
                IsVisible = true,
                Relation = type == WorldObjectType.Monster ? WorldObjectRelation.Enemy : type == WorldObjectType.Player ? WorldObjectRelation.Friendly : WorldObjectRelation.Neutral,
                LastSeenUtc = DateTime.UtcNow
            });
        }
    }

    private void RefreshMinimap()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(RefreshMinimap));
            return;
        }

        var activeWorld = _session?.WorldState ?? _worldState;
        var self = activeWorld.Self;
        if (self is null)
        {
            return;
        }

        var image = _minimapRenderer.Render(activeWorld, self.X, self.Y, _minimapBox.Width, _minimapBox.Height, 2500);
        _minimapBox.Image = image;
    }

    private async void ConnectButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Select an account before connecting.");
            return;
        }

        try
        {
            _session?.Dispose();
            _session = null;
            _characters.Clear();

            var session = GetOrCreateSession(selected);
            await session.ConnectLoginAsync();
            AppendLog("Connected to login server. Waiting for INIT/GG_AUTH/LoginOk/PlayOk packet chain.");
            AppendLog("When PlayOk arrives, click List Characters to open game connection and auth.");
        }
        catch (Exception ex)
        {
            AppendLog($"Connection error: {ex.Message}");
        }
    }

    private async void ListCharactersButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first");
            return;
        }

        try
        {
            var session = GetOrCreateSession(selected);
            await session.ConnectGameAsync();
            await session.SendGameAuthAsync();
            AppendLog("Game protocol/auth sent. Wait for CharSelectInfo (0x13).");
        }
        catch (Exception ex)
        {
            AppendLog($"Character list request failed: {ex.Message}");
        }
    }

    private async void EnterGameButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first");
            return;
        }

        try
        {
            var session = GetOrCreateSession(selected);
            await session.ConnectGameAsync();
            var slot = selected.CharacterSlot;
            if (_characterCombo.SelectedItem is CharacterSelectionEntry character)
            {
                slot = character.Slot;
            }

            await session.SendSelectCharacterAsync(slot);
            AppendLog($"Character select sent for slot {slot}. EnterWorld will be sent after CharSelected.");
        }
        catch (Exception ex)
        {
            AppendLog($"Character select failed: {ex.Message}");
        }
    }

    private async void MoveDebugButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first.");
            return;
        }

        var session = GetOrCreateSession(selected);
        var x = TryParseInt(_moveXText.Text, 150000);
        var y = TryParseInt(_moveYText.Text, 150000);
        var z = TryParseInt(_moveZText.Text, 0);

        await session.SendMoveToLocationAsync(x, y, z);
        AppendLog($"Move request sent: X={x}, Y={y}, Z={z}.");
    }

    private async void StopMoveDebugButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first.");
            return;
        }

        var session = GetOrCreateSession(selected);
        await session.SendStopMoveAsync();
        AppendLog("StopMove sent.");
    }

    private async void AttackDebugButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first.");
            return;
        }

        var session = GetOrCreateSession(selected);
        var targetId = TryParseInt(_targetIdText.Text, 0);
        await session.SendAttackAsync(targetId);
        AppendLog($"Attack sent to target {targetId}.");
    }

    private async void SkillDebugButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first.");
            return;
        }

        var session = GetOrCreateSession(selected);
        var targetId = TryParseInt(_targetIdText.Text, 0);
        var skillId = TryParseInt(_skillIdText.Text, 1001);
        await session.SendUseSkillAsync(skillId, targetId);
        AppendLog($"Skill {skillId} cast on target {targetId}.");
    }

    private async void UseItemDebugButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first.");
            return;
        }

        var session = GetOrCreateSession(selected);
        var itemId = TryParseInt(_itemIdText.Text, 57);
        var itemObjectId = TryParseInt(_itemObjectText.Text, 0);
        var targetId = TryParseInt(_targetIdText.Text, 0);
        await session.SendUseItemAsync(itemObjectId, itemId, targetId, 1);
        AppendLog($"Item {itemId} used from object {itemObjectId} on target {targetId}.");
    }

    private async void TargetDebugButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first.");
            return;
        }

        var session = GetOrCreateSession(selected);
        var targetId = TryParseInt(_targetIdText.Text, 0);
        await session.SendRequestTargetAsync(targetId);
        AppendLog($"Target request sent for object {targetId}.");
    }

    private async void AssistDebugButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first.");
            return;
        }

        var session = GetOrCreateSession(selected);
        var targetId = TryParseInt(_targetIdText.Text, 0);
        await session.SendAssistTargetAsync(targetId);
        AppendLog($"Assist target request sent for object {targetId}.");
    }

    private void DisconnectButton_Click(object? sender, EventArgs e)
    {
        _session?.Dispose();
        _session = null;
        _characters.Clear();
        _lastThreatSummary = string.Empty;
        _playerLocationLabel.Text = "Player: waiting for packets...";
        _playerLocationMetaLabel.Text = "Source: n/a";
        SeedExampleWorld();
        RefreshMinimap();
        AppendLog("Session disconnected.");
    }

    private static int TryParseInt(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendLog), message);
            return;
        }

        _logText.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _logText.ScrollToCaret();
    }
}
