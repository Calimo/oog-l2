using System.ComponentModel;
using OogL2Client.Models;
using OogL2Client.Networking;
using OogL2Client.Storage;
using OogL2Client.World;

namespace OogL2Client;

public sealed class MainForm : Form
{
    private readonly BindingList<SavedServerProfile> _savedServers = new();
    private readonly BindingList<SavedCharacterProfile> _savedCharacters = new();
    private readonly BindingList<CharacterSelectionEntry> _characters = new();

    private readonly ComboBox _menuModeCombo;
    private readonly Panel _menuContentPanel;
    private readonly Panel _addServerPanel;
    private readonly Panel _addCharacterPanel;
    private readonly Panel _loginCharacterPanel;

    private readonly TextBox _serverNameText;
    private readonly TextBox _serverHostText;
    private readonly TextBox _loginPortText;
    private readonly TextBox _gamePortText;
    private readonly TextBox _serverIdText;
    private readonly TextBox _protocolText;
    private readonly Button _saveServerButton;

    private readonly ComboBox _characterServerCombo;
    private readonly TextBox _usernameText;
    private readonly TextBox _passwordText;
    private readonly TextBox _characterSlotText;
    private readonly Button _saveCharacterButton;

    private readonly ComboBox _loginCharacterCombo;
    private readonly ComboBox _characterCombo;
    private readonly ComboBox _skillCombo;

    private readonly TextBox _moveXText;
    private readonly TextBox _moveYText;
    private readonly TextBox _moveZText;
    private readonly TextBox _targetIdText;
    private readonly TextBox _itemIdText;
    private readonly TextBox _itemObjectText;

    private readonly Button _connectButton;
    private readonly Button _listCharactersButton;
    private readonly Button _enterGameButton;
    private readonly Button _disconnectButton;
    private readonly Button _moveDebugButton;
    private readonly Button _stopMoveDebugButton;
    private readonly Button _attackDebugButton;
    private readonly Button _skillDebugButton;
    private readonly Button _useItemDebugButton;
    private readonly Button _getTargetDebugButton;
    private readonly Button _targetDebugButton;
    private readonly Button _assistDebugButton;

    private readonly RichTextBox _logText;
    private readonly PictureBox _minimapBox;
    private readonly ListView _visibleObjectsList;

    private readonly Label _loginStateLabel;
    private readonly Label _gameStateLabel;
    private readonly Label _worldStateLabel;
    private readonly Label _playerLocationLabel;
    private readonly Label _playerLocationMetaLabel;

    private readonly WorldState _worldState = new();
    private readonly MinimapRenderer _minimapRenderer;
    private readonly SavedServerStore _savedServerStore;
    private readonly SavedCharacterStore _savedCharacterStore;
    private readonly ClassNameResolver _classNameResolver;
    private readonly NpcNameResolver _npcNameResolver;
    private readonly SkillNameResolver _skillNameResolver;
    private readonly string _savedServersPath;
    private readonly string _savedCharactersPath;

    private L2MobiusConnection? _session;
    private bool _isRefreshingLoginCombo;

    public MainForm()
    {
        var mapsDirectory = ResolveMapsDirectory();
        _minimapRenderer = new MinimapRenderer(mapsDirectory);

        _savedServersPath = ResolveSavedServersPath();
        _savedCharactersPath = ResolveSavedCharactersPath();
        _savedServerStore = new SavedServerStore(_savedServersPath);
        _savedCharacterStore = new SavedCharacterStore(_savedCharactersPath);
        _classNameResolver = new ClassNameResolver(ResolveClassMapPath());
        _npcNameResolver = new NpcNameResolver(ResolveNpcMapPath());
        _skillNameResolver = new SkillNameResolver();

        Text = "OOG L2 Client";
        Size = new Size(1200, 760);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(18, 24, 35);
        ForeColor = Color.White;

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 8,
            FixedPanel = FixedPanel.None
        };

        var leftRoot = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 7,
            ColumnCount = 1,
            Padding = new Padding(10),
            BackColor = BackColor
        };
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var modePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };
        modePanel.Controls.Add(new Label
        {
            Text = "Menu",
            ForeColor = Color.White,
            AutoSize = true,
            Margin = new Padding(0, 6, 10, 0)
        });

        _menuModeCombo = new ComboBox
        {
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _menuModeCombo.Items.AddRange(new object[]
        {
            "Add New Server",
            "Add New Character",
            "Log In Character"
        });
        _menuModeCombo.SelectedIndexChanged += MenuModeCombo_SelectedIndexChanged;
        modePanel.Controls.Add(_menuModeCombo);

        _menuContentPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };

        _addServerPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        _serverNameText = CreateInlineText(130);
        _serverHostText = CreateInlineText(120, "127.0.0.1");
        _loginPortText = CreateInlineText(60, "2106");
        _gamePortText = CreateInlineText(60, "7777");
        _serverIdText = CreateInlineText(60, "1");
        _protocolText = CreateInlineText(70, "746");
        _saveServerButton = new Button { Text = "Save Server", Width = 110, Height = 28 };
        _saveServerButton.Click += SaveServerButton_Click;
        _addServerPanel.Controls.Add(CreateInlineLabeledRow(new[]
        {
            ("Name", (Control)_serverNameText),
            ("IP", _serverHostText),
            ("Login", _loginPortText),
            ("Game", _gamePortText),
            ("SID", _serverIdText),
            ("Proto", _protocolText),
            ("", _saveServerButton)
        }));

        _addCharacterPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        _characterServerCombo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, DataSource = _savedServers, DisplayMember = nameof(SavedServerProfile.DisplayText) };
        _usernameText = CreateInlineText(120);
        _passwordText = CreateInlineText(120);
        _passwordText.UseSystemPasswordChar = true;
        _characterSlotText = CreateInlineText(60, "0");
        _saveCharacterButton = new Button { Text = "Save Character", Width = 120, Height = 28 };
        _saveCharacterButton.Click += SaveCharacterButton_Click;
        _addCharacterPanel.Controls.Add(CreateInlineLabeledRow(new[]
        {
            ("Server", (Control)_characterServerCombo),
            ("User", _usernameText),
            ("Pass", _passwordText),
            ("Slot", _characterSlotText),
            ("", _saveCharacterButton)
        }));

        _loginCharacterPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        _loginCharacterCombo = new ComboBox
        {
            Width = 420,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DataSource = _savedCharacters,
            DisplayMember = nameof(SavedCharacterProfile.DisplayText)
        };
        _loginCharacterCombo.SelectedIndexChanged += LoginCharacterCombo_SelectedIndexChanged;
        _characterCombo = new ComboBox
        {
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DataSource = _characters,
            DisplayMember = nameof(CharacterSelectionEntry.Name)
        };
        _characterCombo.SelectedIndexChanged += CharacterCombo_SelectedIndexChanged;
        _loginCharacterPanel.Controls.Add(CreateInlineLabeledRow(new[]
        {
            ("Saved", (Control)_loginCharacterCombo),
            ("Live", _characterCombo)
        }));

        _menuContentPanel.Controls.Add(_addServerPanel);
        _menuContentPanel.Controls.Add(_addCharacterPanel);
        _menuContentPanel.Controls.Add(_loginCharacterPanel);

        var locationPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 4)
        };
        _playerLocationLabel = new Label { AutoSize = true, ForeColor = Color.LightSkyBlue, Font = new Font(FontFamily.GenericMonospace, 10f, FontStyle.Bold), Text = "Player: waiting for packets..." };
        _playerLocationMetaLabel = new Label { AutoSize = true, ForeColor = Color.LightGray, Font = new Font(FontFamily.GenericMonospace, 8f), Text = "Source: n/a" };
        locationPanel.Controls.Add(_playerLocationLabel);
        locationPanel.Controls.Add(_playerLocationMetaLabel);

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 0, 0, 4)
        };
        _loginStateLabel = CreateStatusLabel("Login: OFF");
        _gameStateLabel = CreateStatusLabel("Game: OFF");
        _worldStateLabel = CreateStatusLabel("World: OFF");
        statusPanel.Controls.Add(_loginStateLabel);
        statusPanel.Controls.Add(_gameStateLabel);
        statusPanel.Controls.Add(_worldStateLabel);

        _moveXText = CreateInlineText(70, "150000");
        _moveYText = CreateInlineText(70, "150000");
        _moveZText = CreateInlineText(60, "0");
        _targetIdText = CreateInlineText(80, "0");
        _itemIdText = CreateInlineText(80, "57");
        _itemObjectText = CreateInlineText(80, "0");

        _skillCombo = new ComboBox
        {
            Width = 220,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        PopulateSkillCombo();

        var debugFieldsPanel = CreateInlineLabeledRow(new[]
        {
            ("X", (Control)_moveXText),
            ("Y", _moveYText),
            ("Z", _moveZText),
            ("Target", _targetIdText),
            ("Skill", _skillCombo),
            ("Item", _itemIdText),
            ("ItemObj", _itemObjectText)
        });

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 2, 0, 0)
        };
        _moveDebugButton = CreateActionButton("Move", MoveDebugButton_Click);
        _stopMoveDebugButton = CreateActionButton("Stop", StopMoveDebugButton_Click);
        _attackDebugButton = CreateActionButton("Attack", AttackDebugButton_Click);
        _skillDebugButton = CreateActionButton("Skill", SkillDebugButton_Click);
        _useItemDebugButton = CreateActionButton("Use Item", UseItemDebugButton_Click, 96);
        _getTargetDebugButton = CreateActionButton("Get Target", GetTargetDebugButton_Click, 104);
        _targetDebugButton = CreateActionButton("Target", TargetDebugButton_Click);
        _assistDebugButton = CreateActionButton("Assist", AssistDebugButton_Click);
        _connectButton = CreateActionButton("Auto Connect", ConnectButton_Click, 120);
        _listCharactersButton = CreateActionButton("List Characters", ListCharactersButton_Click, 130);
        _enterGameButton = CreateActionButton("Enter Game", EnterGameButton_Click, 110);
        _disconnectButton = CreateActionButton("Disconnect", DisconnectButton_Click, 110);
        actionPanel.Controls.AddRange(new Control[]
        {
            _moveDebugButton, _stopMoveDebugButton, _attackDebugButton, _skillDebugButton, _useItemDebugButton, _getTargetDebugButton, _targetDebugButton, _assistDebugButton,
            _connectButton, _listCharactersButton, _enterGameButton, _disconnectButton
        });

        _logText = new RichTextBox
        {
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen,
            Font = new Font(FontFamily.GenericMonospace, 10f),
            BorderStyle = BorderStyle.FixedSingle
        };

        leftRoot.Controls.Add(modePanel, 0, 0);
        leftRoot.Controls.Add(_menuContentPanel, 0, 1);
        leftRoot.Controls.Add(locationPanel, 0, 2);
        leftRoot.Controls.Add(statusPanel, 0, 3);
        leftRoot.Controls.Add(debugFieldsPanel, 0, 4);
        leftRoot.Controls.Add(actionPanel, 0, 5);
        leftRoot.Controls.Add(_logText, 0, 6);

        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 8,
            FixedPanel = FixedPanel.None
        };

        _minimapBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.StretchImage
        };

        _visibleObjectsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HideSelection = false,
            BackColor = Color.FromArgb(14, 20, 30),
            ForeColor = Color.White
        };
        _visibleObjectsList.Columns.Add("Name", 320, HorizontalAlignment.Left);
        _visibleObjectsList.Columns.Add("ObjId", 90, HorizontalAlignment.Left);
        _visibleObjectsList.Columns.Add("NpcId", 90, HorizontalAlignment.Left);
        _visibleObjectsList.Columns.Add("Type", 90, HorizontalAlignment.Left);
        _visibleObjectsList.Columns.Add("Lvl", 50, HorizontalAlignment.Left);
        _visibleObjectsList.Columns.Add("HP", 110, HorizontalAlignment.Left);
        _visibleObjectsList.SelectedIndexChanged += VisibleObjectsList_SelectedIndexChanged;

        var objectHeader = new Label
        {
            Text = "Visible Objects",
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Color.LightSteelBlue,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 0, 0)
        };
        var objectPanel = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
        objectPanel.Controls.Add(_visibleObjectsList);
        objectPanel.Controls.Add(objectHeader);

        rightSplit.Panel1.Controls.Add(_minimapBox);
        rightSplit.Panel2.Controls.Add(objectPanel);
        rightSplit.Resize += (_, _) =>
        {
            if (rightSplit.Height > 0)
            {
                rightSplit.SplitterDistance = Math.Max(220, (int)(rightSplit.Height * 0.55f));
            }

            AdjustVisibleObjectsColumns();
        };

        mainSplit.Panel1.Controls.Add(leftRoot);
        mainSplit.Panel2.Controls.Add(rightSplit);
        mainSplit.Resize += (_, _) =>
        {
            if (mainSplit.Width > 0)
            {
                mainSplit.SplitterDistance = Math.Max(540, (int)(mainSplit.Width * 0.64f));
            }
        };

        Controls.Add(mainSplit);

        LoadSavedServers();
        LoadSavedCharacters();
        if (_menuModeCombo.Items.Count > 0)
        {
            _menuModeCombo.SelectedIndex = 2;
        }

        SeedExampleWorld();
        RefreshMinimap();
        RefreshVisibleObjectsList();

        AppendLog("OOG L2 Client ready.");
        AppendLog($"Saved servers file: {_savedServersPath}");
        AppendLog($"Saved characters file: {_savedCharactersPath}");
        AppendLog("Use menu dropdown to add servers, add characters, or select character login.");
        UpdateStatus(new SessionStatus(ConnectionStage.Disconnected, false, false, false));
    }

    private static TextBox CreateInlineText(int width, string text = "")
    {
        return new TextBox { Width = width, Text = text, Margin = new Padding(0, 0, 8, 0) };
    }

    private static Button CreateActionButton(string text, EventHandler handler, int width = 90)
    {
        var button = new Button { Text = text, Width = width, Margin = new Padding(0, 0, 6, 6) };
        button.Click += handler;
        return button;
    }

    private static FlowLayoutPanel CreateInlineLabeledRow((string label, Control control)[] fields)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        foreach (var (labelText, control) in fields)
        {
            if (!string.IsNullOrWhiteSpace(labelText))
            {
                panel.Controls.Add(new Label
                {
                    Text = labelText,
                    AutoSize = true,
                    ForeColor = Color.White,
                    Margin = new Padding(0, 6, 4, 0)
                });
            }

            panel.Controls.Add(control);
        }

        return panel;
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

    private static string ResolveSavedServersPath()
    {
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "OogL2Client", "Data", "saved-servers.json"));
    }

    private static string ResolveSavedCharactersPath()
    {
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "OogL2Client", "Data", "saved-characters.json"));
    }

    private static string ResolveClassMapPath()
    {
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "OogL2Client", "Data", "class-map.json"));
    }

    private static string ResolveNpcMapPath()
    {
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "OogL2Client", "Data", "npc-map.json"));
    }

    private void LoadSavedServers()
    {
        _savedServers.Clear();
        foreach (var server in _savedServerStore.Load())
        {
            _savedServers.Add(server);
        }

        if (_savedServers.Count == 0)
        {
            _savedServers.Add(new SavedServerProfile
            {
                Name = "Local Mobius",
                ServerHost = "127.0.0.1",
                LoginPort = 2106,
                GamePort = 7777,
                ServerId = 1,
                ProtocolVersion = 746
            });

            _savedServerStore.Save(_savedServers.ToList());
        }
    }

    private void LoadSavedCharacters()
    {
        _savedCharacters.Clear();
        foreach (var character in _savedCharacterStore.Load())
        {
            if (string.IsNullOrWhiteSpace(character.CharacterClassName))
            {
                character.CharacterClassName = _classNameResolver.Resolve(character.CharacterClassId);
            }

            if (string.IsNullOrWhiteSpace(character.ServerName) && !string.IsNullOrWhiteSpace(character.ServerHost))
            {
                character.ServerName = _savedServers.FirstOrDefault(s => string.Equals(s.ServerHost, character.ServerHost, StringComparison.OrdinalIgnoreCase))?.Name ?? "Server";
            }

            _savedCharacters.Add(character);
        }

        RefreshLoginCharacterCombo();
    }

    private void RefreshLoginCharacterCombo(SavedCharacterProfile? preferred = null)
    {
        _isRefreshingLoginCombo = true;
        var selected = preferred ?? _loginCharacterCombo.SelectedItem as SavedCharacterProfile;

        _loginCharacterCombo.DataSource = null;
        _loginCharacterCombo.DataSource = _savedCharacters;
        _loginCharacterCombo.DisplayMember = nameof(SavedCharacterProfile.DisplayText);

        if (selected is not null)
        {
            _loginCharacterCombo.SelectedItem = selected;
        }

        _isRefreshingLoginCombo = false;
    }

    private void MenuModeCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var selected = _menuModeCombo.SelectedItem?.ToString() ?? string.Empty;
        _addServerPanel.Visible = selected == "Add New Server";
        _addCharacterPanel.Visible = selected == "Add New Character";
        _loginCharacterPanel.Visible = selected == "Log In Character";
    }

    private void SaveServerButton_Click(object? sender, EventArgs e)
    {
        var name = _serverNameText.Text.Trim();
        var host = _serverHostText.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host))
        {
            AppendLog("Server name and IP are required.");
            return;
        }

        var profile = new SavedServerProfile
        {
            Name = name,
            ServerHost = host,
            LoginPort = TryParseInt(_loginPortText.Text, 2106),
            GamePort = TryParseInt(_gamePortText.Text, 7777),
            ServerId = TryParseInt(_serverIdText.Text, 1),
            ProtocolVersion = TryParseInt(_protocolText.Text, 746)
        };

        var existing = _savedServers.FirstOrDefault(s => string.Equals(s.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _savedServers.Add(profile);
        }
        else
        {
            existing.ServerHost = profile.ServerHost;
            existing.LoginPort = profile.LoginPort;
            existing.GamePort = profile.GamePort;
            existing.ServerId = profile.ServerId;
            existing.ProtocolVersion = profile.ProtocolVersion;
        }

        _savedServerStore.Save(_savedServers.ToList());
        _characterServerCombo.DataSource = null;
        _characterServerCombo.DataSource = _savedServers;
        _characterServerCombo.DisplayMember = nameof(SavedServerProfile.DisplayText);
        _characterServerCombo.SelectedItem = existing ?? profile;
        AppendLog($"Saved server: {profile.DisplayText}");
    }

    private void SaveCharacterButton_Click(object? sender, EventArgs e)
    {
        if (_characterServerCombo.SelectedItem is not SavedServerProfile server)
        {
            AppendLog("Select a server before saving character.");
            return;
        }

        var username = _usernameText.Text.Trim();
        var password = _passwordText.Text.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            AppendLog("Username and password are required.");
            return;
        }

        var slot = TryParseInt(_characterSlotText.Text, 0);
        var liveName = _characterCombo.SelectedItem is CharacterSelectionEntry entry ? entry.Name : $"CharSlot{slot}";
        var liveLevel = _characterCombo.SelectedItem is CharacterSelectionEntry entry2 ? entry2.Level : 0;
        var liveClass = _characterCombo.SelectedItem is CharacterSelectionEntry entry3 ? entry3.ClassId : 0;

        var profile = new SavedCharacterProfile
        {
            Username = username,
            Password = password,
            ServerHost = server.ServerHost,
            LoginPort = server.LoginPort,
            GamePort = server.GamePort,
            ServerId = server.ServerId,
            ProtocolVersion = server.ProtocolVersion,
            CharacterSlot = slot,
            CharacterName = liveName,
            CharacterLevel = liveLevel,
            CharacterClassId = liveClass,
            CharacterClassName = _classNameResolver.Resolve(liveClass),
            ServerName = server.Name
        };

        var existing = _savedCharacters.FirstOrDefault(c =>
            string.Equals(c.Username, profile.Username, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.CharacterName, profile.CharacterName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            _savedCharacters.Add(profile);
            existing = profile;
        }
        else
        {
            existing.Password = profile.Password;
            existing.ServerHost = profile.ServerHost;
            existing.LoginPort = profile.LoginPort;
            existing.GamePort = profile.GamePort;
            existing.ServerId = profile.ServerId;
            existing.ProtocolVersion = profile.ProtocolVersion;
            existing.CharacterSlot = profile.CharacterSlot;
            existing.CharacterLevel = profile.CharacterLevel;
            existing.CharacterClassId = profile.CharacterClassId;
            existing.CharacterClassName = profile.CharacterClassName;
            existing.ServerName = profile.ServerName;
        }

        _savedCharacterStore.Save(_savedCharacters.ToList());
        RefreshLoginCharacterCombo(existing);
        _menuModeCombo.SelectedItem = "Log In Character";
        AppendLog($"Saved character: {existing.DisplayText}");
    }

    private void LoginCharacterCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isRefreshingLoginCombo)
        {
            return;
        }

        if (_loginCharacterCombo.SelectedItem is not SavedCharacterProfile selected)
        {
            return;
        }

        _usernameText.Text = selected.Username;
        _passwordText.Text = selected.Password;
        _characterSlotText.Text = selected.CharacterSlot.ToString();
        var server = _savedServers.FirstOrDefault(s => string.Equals(s.Name, selected.ServerName, StringComparison.OrdinalIgnoreCase))
                     ?? _savedServers.FirstOrDefault(s => string.Equals(s.ServerHost, selected.ServerHost, StringComparison.OrdinalIgnoreCase));
        if (server is not null)
        {
            _characterServerCombo.SelectedItem = server;
            _serverNameText.Text = server.Name;
            _serverHostText.Text = server.ServerHost;
            _loginPortText.Text = server.LoginPort.ToString();
            _gamePortText.Text = server.GamePort.ToString();
            _serverIdText.Text = server.ServerId.ToString();
            _protocolText.Text = server.ProtocolVersion.ToString();
        }
    }

    private void CharacterCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_characterCombo.SelectedItem is not CharacterSelectionEntry character)
        {
            return;
        }

        _characterSlotText.Text = character.Slot.ToString();

        if (_loginCharacterCombo.SelectedItem is not SavedCharacterProfile selected)
        {
            return;
        }

        selected.CharacterSlot = character.Slot;
        selected.CharacterName = character.Name;
        selected.CharacterLevel = character.Level;
        selected.CharacterClassId = character.ClassId;
        selected.CharacterClassName = _classNameResolver.Resolve(character.ClassId);
        _savedCharacterStore.Save(_savedCharacters.ToList());
        RefreshLoginCharacterCombo(selected);
    }

    private SavedCharacterProfile? GetSelectedCharacter()
    {
        return _loginCharacterCombo.SelectedItem as SavedCharacterProfile;
    }

    private L2MobiusConnection GetOrCreateSession(AccountProfile account)
    {
        if (_session is null)
        {
            _session = new L2MobiusConnection(account);
            _session.MessageReceived += AppendLog;
            _session.CharacterListReceived += OnCharacterListReceived;
            _session.StatusChanged += UpdateStatus;
            _session.WorldStateUpdated += OnWorldStateUpdated;
            _session.PlayerLocationUpdated += OnPlayerLocationUpdated;
            _session.LearnedSkillsUpdated += OnLearnedSkillsUpdated;
            _session.TargetUpdated += OnTargetUpdated;
        }

        return _session;
    }

    private async void ConnectButton_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedCharacter();
        if (selected is null)
        {
            AppendLog("Select a saved character in Log In Character menu.");
            return;
        }

        try
        {
            _session?.Dispose();
            _session = null;
            _characters.Clear();

            var session = GetOrCreateSession(selected.ToAccountProfile());
            await session.ConnectLoginAsync();
            await WaitForConditionAsync(() => session.HasPlayKeys, TimeSpan.FromSeconds(15), "PlayOk keys");
            await session.ConnectGameAsync();
            await session.SendGameAuthAsync();
            await WaitForConditionAsync(() => _characters.Count > 0, TimeSpan.FromSeconds(15), "CharSelectInfo");

            var slot = selected.CharacterSlot;
            var byName = _characters.FirstOrDefault(c => string.Equals(c.Name, selected.CharacterName, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
            {
                slot = byName.Slot;
            }

            await session.SendSelectCharacterAsync(slot);
            AppendLog($"Auto connect: selected {selected.CharacterName} (slot {slot}). Waiting for world entry...");
            await WaitForConditionAsync(() => session.IsInWorld, TimeSpan.FromSeconds(20), "World entry");
            AppendLog("Auto connect complete. Character is in world.");
        }
        catch (Exception ex)
        {
            AppendLog($"Connection error: {ex.Message}");
        }
    }

    private async void ListCharactersButton_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedCharacter();
        if (selected is null)
        {
            AppendLog("Select a saved character in Log In Character menu.");
            return;
        }

        try
        {
            var session = GetOrCreateSession(selected.ToAccountProfile());
            await session.ConnectGameAsync();
            await session.SendGameAuthAsync();
            AppendLog("Game protocol/auth sent. Wait for character list.");
        }
        catch (Exception ex)
        {
            AppendLog($"Character list request failed: {ex.Message}");
        }
    }

    private async void EnterGameButton_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedCharacter();
        if (selected is null)
        {
            AppendLog("Select a saved character in Log In Character menu.");
            return;
        }

        try
        {
            var session = GetOrCreateSession(selected.ToAccountProfile());
            await session.ConnectGameAsync();
            var slot = _characterCombo.SelectedItem is CharacterSelectionEntry live ? live.Slot : selected.CharacterSlot;
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
        var session = _session;
        if (session is null)
        {
            AppendLog("Connect first.");
            return;
        }

        var x = TryParseInt(_moveXText.Text, 150000);
        var y = TryParseInt(_moveYText.Text, 150000);
        var z = TryParseInt(_moveZText.Text, 0);
        await session.SendMoveToLocationAsync(x, y, z);
        AppendLog($"Move request sent: X={x}, Y={y}, Z={z}.");
    }

    private async void StopMoveDebugButton_Click(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            AppendLog("Connect first.");
            return;
        }

        await _session.SendStopMoveAsync();
        AppendLog("StopMove sent.");
    }

    private async void AttackDebugButton_Click(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            AppendLog("Connect first.");
            return;
        }

        var targetId = TryParseInt(_targetIdText.Text, 0);
        if (targetId <= 0)
        {
            AppendLog("Select a valid ObjId from the list before attacking.");
            return;
        }

        await _session.SendRequestTargetAsync(targetId);
        await Task.Delay(75);
        await _session.SendAttackAsync(targetId);
        AppendLog($"Attack sent to ObjId {targetId} (target selected first).");
    }

    private async void SkillDebugButton_Click(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            AppendLog("Connect first.");
            return;
        }

        var targetId = TryParseInt(_targetIdText.Text, 0);
        var skillId = _skillCombo.SelectedValue is int selectedId ? selectedId : 1001;
        if (targetId > 0)
        {
            await _session.SendRequestTargetAsync(targetId);
            await Task.Delay(75);
        }

        await _session.SendUseSkillAsync(skillId, targetId);
        AppendLog(targetId > 0
            ? $"Skill {skillId} cast requested on ObjId {targetId} (target selected first)."
            : $"Skill {skillId} cast requested with no explicit target.");
    }

    private async void UseItemDebugButton_Click(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            AppendLog("Connect first.");
            return;
        }

        var itemId = TryParseInt(_itemIdText.Text, 57);
        var itemObjectId = TryParseInt(_itemObjectText.Text, 0);
        var targetId = TryParseInt(_targetIdText.Text, 0);
        await _session.SendUseItemAsync(itemObjectId, itemId, targetId, 1);
        AppendLog($"Item {itemId} used from object {itemObjectId} on target {targetId}.");
    }

    private async void TargetDebugButton_Click(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            AppendLog("Connect first.");
            return;
        }

        var targetId = TryParseInt(_targetIdText.Text, 0);
        await _session.SendRequestTargetAsync(targetId);
        AppendLog($"Target request sent for object {targetId}.");
    }

    private void GetTargetDebugButton_Click(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            AppendLog("Connect first.");
            return;
        }

        var target = _session.CurrentTarget;
        if (target is null)
        {
            AppendLog("No current target from server yet. Select a target in-game first.");
            return;
        }

        _targetIdText.Text = target.ObjectId.ToString();
        var hpText = target.MaxHp > 0 ? $"{target.Hp}/{target.MaxHp}" : target.Hp > 0 ? target.Hp.ToString() : "-";
        AppendLog($"Current target: {target.Name} (ObjId {target.ObjectId}, HP {hpText}).");
        RefreshMinimap();
    }

    private async void AssistDebugButton_Click(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            AppendLog("Connect first.");
            return;
        }

        var targetId = TryParseInt(_targetIdText.Text, 0);
        await _session.SendAssistTargetAsync(targetId);
        AppendLog($"Assist target request sent for object {targetId}.");
    }

    private void DisconnectButton_Click(object? sender, EventArgs e)
    {
        _session?.Dispose();
        _session = null;
        _characters.Clear();
        _playerLocationLabel.Text = "Player: waiting for packets...";
        _playerLocationMetaLabel.Text = "Source: n/a";
        SeedExampleWorld();
        RefreshMinimap();
        RefreshVisibleObjectsList();
        AppendLog("Session disconnected.");
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

        var selected = GetSelectedCharacter();
        if (selected is null)
        {
            return;
        }

        var matched = characters.FirstOrDefault(c => c.Slot == selected.CharacterSlot) ??
                      characters.FirstOrDefault(c => string.Equals(c.Name, selected.CharacterName, StringComparison.OrdinalIgnoreCase));
        if (matched is null)
        {
            return;
        }

        selected.CharacterSlot = matched.Slot;
        selected.CharacterName = matched.Name;
        selected.CharacterLevel = matched.Level;
        selected.CharacterClassId = matched.ClassId;
        selected.CharacterClassName = _classNameResolver.Resolve(matched.ClassId);
        _savedCharacterStore.Save(_savedCharacters.ToList());
        RefreshLoginCharacterCombo(selected);
    }

    private void OnWorldStateUpdated(WorldPacketApplyResult update)
    {
        RefreshMinimap();
        RefreshVisibleObjectsList();
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

    private void OnTargetUpdated(TargetUpdate update)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<TargetUpdate>(OnTargetUpdated), update);
            return;
        }

        if (update.ObjectId > 0)
        {
            _targetIdText.Text = update.ObjectId.ToString();
        }

        RefreshMinimap();
    }

    private void UpdateStatus(SessionStatus status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<SessionStatus>(UpdateStatus), status);
            return;
        }

        SetStatusLabel(_loginStateLabel, "Login", status.Stage == ConnectionStage.LoginConnected || status.LoginAuthenticated);
        SetStatusLabel(_gameStateLabel, "Game", status.Stage == ConnectionStage.GameConnected || status.GameAuthenticated);
        SetStatusLabel(_worldStateLabel, "World", status.InWorld);
    }

    private static void SetStatusLabel(Label label, string title, bool on)
    {
        label.Text = $"{title}: {(on ? "ON" : "OFF")}";
        label.ForeColor = on ? Color.LawnGreen : Color.LightGray;
    }

    private void SeedExampleWorld()
    {
        _worldState.Clear();
        _worldState.SetSelf(new WorldObject
        {
            ObjectId = 1,
            Name = "Self",
            Type = WorldObjectType.Player,
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
            (-1200, -900, WorldObjectType.NPC, "Merchant")
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
                Relation = type == WorldObjectType.Monster ? WorldObjectRelation.Enemy : WorldObjectRelation.Friendly,
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

        var world = _session?.WorldState ?? _worldState;
        var self = world.Self;
        if (self is null)
        {
            return;
        }

        var targetObjectId = _session?.CurrentTargetObjectId ?? 0;
        _minimapBox.Image = _minimapRenderer.Render(world, self.X, self.Y, _minimapBox.Width, _minimapBox.Height, 2500, targetObjectId);
    }

    private void RefreshVisibleObjectsList()
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(RefreshVisibleObjectsList));
            return;
        }

        var world = _session?.WorldState ?? _worldState;
        var self = world.Self;
        var visible = world.VisibleObjectsSnapshot().Where(o => self is null || o.ObjectId != self.ObjectId).ToList();

        _visibleObjectsList.BeginUpdate();
        try
        {
            _visibleObjectsList.Items.Clear();
            foreach (var obj in visible)
            {
                var type = obj.Type switch
                {
                    WorldObjectType.Player => "Player",
                    WorldObjectType.NPC => "NPC",
                    WorldObjectType.Monster => "Monster",
                    WorldObjectType.Item => "Item",
                    _ => "Unknown"
                };

                var name = string.IsNullOrWhiteSpace(obj.Name) ? "(unnamed)" : obj.Name;
                if (obj.Type == WorldObjectType.NPC || obj.Type == WorldObjectType.Monster)
                {
                    var translated = _npcNameResolver.Resolve(obj.TemplateId);
                    if (!string.IsNullOrWhiteSpace(translated))
                    {
                        name = translated;
                    }
                    else if (string.Equals(name, "Monster", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(name, "NPC", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith("Mob ", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith("Npc ", StringComparison.OrdinalIgnoreCase))
                    {
                        name = obj.Type == WorldObjectType.Monster ? $"Monster {obj.TemplateId}" : $"NPC {obj.TemplateId}";
                    }
                }

                var level = obj.Level > 0 ? obj.Level.ToString() : "-";
                var hp = obj.MaxHp > 0 ? $"{obj.Hp}/{obj.MaxHp}" : obj.Hp > 0 ? obj.Hp.ToString() : "-";
                var objectId = obj.ObjectId > 0 ? obj.ObjectId.ToString() : "-";
                var templateId = obj.TemplateId > 0 ? obj.TemplateId.ToString() : "-";

                var row = new ListViewItem(name);
                row.SubItems.Add(objectId);
                row.SubItems.Add(templateId);
                row.SubItems.Add(type);
                row.SubItems.Add(level);
                row.SubItems.Add(hp);
                _visibleObjectsList.Items.Add(row);
            }

            AdjustVisibleObjectsColumns();
        }
        finally
        {
            _visibleObjectsList.EndUpdate();
        }
    }

    private void AdjustVisibleObjectsColumns()
    {
        if (_visibleObjectsList.Columns.Count < 6)
        {
            return;
        }

        var width = _visibleObjectsList.ClientSize.Width;
        if (width <= 0)
        {
            return;
        }

        var objectId = 90;
        var templateId = 90;
        var type = 90;
        var lvl = 50;
        var hp = 110;
        var fixedWidth = objectId + templateId + type + lvl + hp;
        var name = Math.Max(220, width - fixedWidth - 10);

        _visibleObjectsList.Columns[0].Width = name;
        _visibleObjectsList.Columns[1].Width = objectId;
        _visibleObjectsList.Columns[2].Width = templateId;
        _visibleObjectsList.Columns[3].Width = type;
        _visibleObjectsList.Columns[4].Width = lvl;
        _visibleObjectsList.Columns[5].Width = hp;
    }

    private void PopulateSkillCombo()
    {
        _skillCombo.DataSource = null;
        _skillCombo.Items.Clear();
        _skillCombo.Items.Add(new SkillComboItem(1001, "Waiting learned skills", 1, false));
        _skillCombo.DisplayMember = nameof(SkillComboItem.DisplayText);
        _skillCombo.ValueMember = nameof(SkillComboItem.SkillId);
        _skillCombo.SelectedIndex = 0;
    }

    private void OnLearnedSkillsUpdated(IReadOnlyList<LearnedSkillEntry> skills)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<IReadOnlyList<LearnedSkillEntry>>(OnLearnedSkillsUpdated), skills);
            return;
        }

        var selectedSkillId = _skillCombo.SelectedValue is int skillId ? skillId : 0;
        var learned = skills
            .Where(s => !s.IsPassive)
            .Where(s => !s.IsDisabled)
            .Where(s => s.SkillId is > 0 and <= 50000)
            .Where(s => s.Level is >= 0 and <= 100)
            .OrderBy(s => s.SkillId)
            .Select(s =>
            {
                var name = _skillNameResolver.Resolve(s.SkillId) ?? $"Skill {s.SkillId}";
                return new SkillComboItem(s.SkillId, name, s.Level, s.IsPassive);
            })
            .ToList();

        _skillCombo.DataSource = null;
        _skillCombo.Items.Clear();

        if (learned.Count == 0)
        {
            _skillCombo.Items.Add(new SkillComboItem(1001, "No active learned skills", 1, false));
            _skillCombo.DisplayMember = nameof(SkillComboItem.DisplayText);
            _skillCombo.ValueMember = nameof(SkillComboItem.SkillId);
            _skillCombo.SelectedIndex = 0;
            AppendLog("Skill list received, but no active skills were available for casting.");
            return;
        }

        _skillCombo.DataSource = learned;
        _skillCombo.DisplayMember = nameof(SkillComboItem.DisplayText);
        _skillCombo.ValueMember = nameof(SkillComboItem.SkillId);

        var selectedIndex = learned.FindIndex(s => s.SkillId == selectedSkillId);
        _skillCombo.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        AppendLog($"Loaded {learned.Count} learned active skill(s) for this character.");
    }

    private void VisibleObjectsList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_visibleObjectsList.SelectedItems.Count == 0)
        {
            return;
        }

        var selected = _visibleObjectsList.SelectedItems[0];
        if (selected.SubItems.Count < 2)
        {
            return;
        }

        var objectIdText = selected.SubItems[1].Text;
        if (int.TryParse(objectIdText, out var objectId) && objectId > 0)
        {
            _targetIdText.Text = objectId.ToString();
        }
    }

    private sealed class SkillComboItem
    {
        public SkillComboItem(int skillId, string skillName, int level, bool isPassive)
        {
            SkillId = skillId;
            SkillName = skillName;
            Level = level;
            IsPassive = isPassive;
        }

        public int SkillId { get; }
        public string SkillName { get; }
        public int Level { get; }
        public bool IsPassive { get; }

        public string DisplayText => $"{SkillName} Lv{Math.Max(1, Level)} ({SkillId})";
    }

    private static int TryParseInt(string value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout, string label)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException($"Timeout while waiting for {label}.");
            }

            await Task.Delay(150);
        }
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
