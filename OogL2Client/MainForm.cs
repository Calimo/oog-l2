using System.ComponentModel;
using OogL2Client.Models;
using OogL2Client.Networking;

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
    private readonly ListBox _accountList;
    private readonly RichTextBox _logText;
    private readonly Button _addAccountButton;
    private readonly Button _connectButton;
    private readonly Button _listCharactersButton;
    private readonly Button _enterGameButton;
    private readonly Button _disconnectButton;
    private readonly ComboBox _characterCombo;
    private readonly Label _loginStateLabel;
    private readonly Label _gameStateLabel;
    private readonly Label _worldStateLabel;
    private L2MobiusConnection? _session;

    public MainForm()
    {
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

        _logText = new RichTextBox
        {
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen,
            Font = new Font(FontFamily.GenericMonospace, 10f),
            BorderStyle = BorderStyle.FixedSingle
        };

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

        rightPanel.Controls.Add(_logText);
        rightPanel.Controls.Add(statusPanel);
        rightPanel.Controls.Add(buttonPanel);
        statusPanel.BringToFront();
        buttonPanel.BringToFront();

        table.Controls.Add(leftPanel, 0, 0);
        table.Controls.Add(rightPanel, 1, 0);
        Controls.Add(table);

        AppendLog("OOG L2 Client ready.");
        AppendLog("This is a protocol-learning shell for a private L2J Mobius server.");
        AppendLog("Protocol default is 746. Add account, click Connect, then List Characters.");
        UpdateStatus(new SessionStatus(ConnectionStage.Disconnected, false, false, false));
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
        }

        return _session;
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

    private void DisconnectButton_Click(object? sender, EventArgs e)
    {
        _session?.Dispose();
        _session = null;
        _characters.Clear();
        AppendLog("Session disconnected.");
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
