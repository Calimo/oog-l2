using System.Net.Sockets;
using OogL2Client.Models;

namespace OogL2Client.Networking;

public sealed class L2MobiusConnection : IDisposable
{
    private readonly AccountProfile _account;
    private readonly TcpClient _client = new();
    private readonly CancellationTokenSource _cts = new();
    private NetworkStream? _stream;

    public event Action<string>? MessageReceived;

    public L2MobiusConnection(AccountProfile account)
    {
        _account = account;
    }

    public bool IsConnected => _client.Connected;

    public async Task ConnectAsync()
    {
        if (_client.Connected)
        {
            return;
        }

        await _client.ConnectAsync(_account.ServerHost, _account.LoginPort);
        _stream = _client.GetStream();

        MessageReceived?.Invoke($"Connected to {_account.ServerHost}:{_account.LoginPort}.");

        _ = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task SendLoginRequestAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var packet = L2MobiusPacketBuilder.BuildAuthLoginRequest(_account.Username, _account.Password);
        await SendAsync(packet);
    }

    public async Task SendSelectServerAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var packet = L2MobiusPacketBuilder.BuildSelectServerRequest(_account.ServerId);
        await SendAsync(packet);
    }

    public async Task SendPingAsync()
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        await SendAsync(L2MobiusPacketBuilder.BuildPingPacket());
    }

    public async Task SendAsync(byte[] packet)
    {
        if (_stream is null)
        {
            throw new InvalidOperationException("Stream not initialized.");
        }

        await _stream.WriteAsync(packet.AsMemory(0, packet.Length), _cts.Token);
        MessageReceived?.Invoke($"Sent: {Convert.ToHexString(packet)}");
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

                var chunk = new byte[read];
                Array.Copy(buffer, chunk, read);
                MessageReceived?.Invoke($"Received: {Convert.ToHexString(chunk)}");
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
            MessageReceived?.Invoke("Connection closed.");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _stream?.Dispose();
        _client.Dispose();
    }
}
