using System.ComponentModel;
using OogL2Client.Models;
using OogL2Client.Networking;

namespace OogL2Client;

public sealed class MainForm : Form
{
    private readonly BindingList<AccountProfile> _accounts = new();
    private readonly TextBox _serverHostText;
    private readonly TextBox _loginPortText;
    private readonly TextBox _usernameText;
    private readonly TextBox _passwordText;
    private readonly TextBox _serverIdText;
    private readonly ListBox _accountList;
    private readonly RichTextBox _logText;
    private readonly Button _addAccountButton;
    private readonly Button _connectButton;
    private readonly Button _pingButton;
    private readonly Button _selectServerButton;

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
        _serverIdText = new TextBox { Width = 260, Margin = new Padding(0, 5, 0, 5), Text = "1" };

        var accountFields = new[]
        {
            CreateField("Username", _usernameText),
            CreateField("Password", _passwordText),
            CreateField("Login Server", _serverHostText),
            CreateField("Login Port", _loginPortText),
            CreateField("Server ID", _serverIdText)
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

        _pingButton = new Button { Text = "Ping", Width = 120, Margin = new Padding(0, 0, 8, 0) };
        _pingButton.Click += PingButton_Click;

        _selectServerButton = new Button { Text = "Select Server", Width = 140 };
        _selectServerButton.Click += SelectServerButton_Click;

        buttonPanel.Controls.Add(_connectButton);
        buttonPanel.Controls.Add(_pingButton);
        buttonPanel.Controls.Add(_selectServerButton);

        rightPanel.Controls.Add(_logText);
        rightPanel.Controls.Add(buttonPanel);
        buttonPanel.BringToFront();

        table.Controls.Add(leftPanel, 0, 0);
        table.Controls.Add(rightPanel, 1, 0);
        Controls.Add(table);

        AppendLog("OOG L2 Client ready.");
        AppendLog("This is a protocol-learning shell for a private L2J Mobius server.");
        AppendLog("Configure the login data and click Connect.");
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
            ServerId = int.TryParse(_serverIdText.Text, out var serverId) ? serverId : 1
        };

        _accounts.Add(profile);
        AppendLog($"Account added: {profile.Username} ({profile.ServerHost}:{profile.LoginPort})");

        _usernameText.Clear();
        _passwordText.Clear();
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
            using var client = new L2MobiusConnection(selected);
            client.MessageReceived += AppendLog;
            await client.ConnectAsync();
            await client.SendLoginRequestAsync();
            AppendLog("Login request sent. Waiting for server response...");
            await Task.Delay(4000);
            AppendLog("Connection test complete. The socket is ready to be expanded with a full game protocol parser.");
        }
        catch (Exception ex)
        {
            AppendLog($"Connection error: {ex.Message}");
        }
    }

    private async void PingButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first");
            return;
        }

        try
        {
            using var client = new L2MobiusConnection(selected);
            client.MessageReceived += AppendLog;
            await client.ConnectAsync();
            await client.SendPingAsync();
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            AppendLog($"Ping failed: {ex.Message}");
        }
    }

    private async void SelectServerButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountProfile selected)
        {
            AppendLog("Pick an account first");
            return;
        }

        try
        {
            using var client = new L2MobiusConnection(selected);
            client.MessageReceived += AppendLog;
            await client.ConnectAsync();
            await client.SendSelectServerAsync();
            AppendLog("Server selection packet sent.");
            await Task.Delay(1500);
        }
        catch (Exception ex)
        {
            AppendLog($"Server selection failed: {ex.Message}");
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
