using OogL2Client.Networking;

namespace OogL2Client.Bot;

public sealed class BotEngine : IDisposable
{
    private readonly L2MobiusConnection _connection;
    private readonly Queue<IBotAction> _actions = new();
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _cts;
    private Task? _workerTask;

    public BotEngine(L2MobiusConnection connection, BotContext? context = null)
    {
        _connection = connection;
        Context = context ?? new BotContext();
    }

    public BotContext Context { get; }
    public bool IsRunning => _workerTask is { IsCompleted: false };
    public event Action<string>? Log;

    public void Enqueue(IBotAction action)
    {
        lock (_syncRoot)
        {
            _actions.Enqueue(action);
        }
    }

    public void EnqueueRange(IEnumerable<IBotAction> actions)
    {
        foreach (var action in actions)
        {
            Enqueue(action);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return _workerTask!;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _workerTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        return _workerTask;
    }

    public async Task ExecuteAsync(IBotAction action, CancellationToken cancellationToken = default)
    {
        Log?.Invoke($"Executing {action.Name}...");
        await action.ExecuteAsync(Context, _connection, cancellationToken);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IBotAction? action;
            lock (_syncRoot)
            {
                if (_actions.Count == 0)
                {
                    action = null;
                }
                else
                {
                    action = _actions.Dequeue();
                }
            }

            if (action is null)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            try
            {
                Log?.Invoke($"Bot action: {action.Name}");
                await action.ExecuteAsync(Context, _connection, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Bot action failed: {action.Name} :: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
