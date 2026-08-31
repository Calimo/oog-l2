using OogL2Client.Networking;

namespace OogL2Client.Bot;

public sealed class BotScript
{
    private readonly List<IBotAction> _actions = new();

    public IReadOnlyList<IBotAction> Actions => _actions;

    public void Add(IBotAction action)
    {
        _actions.Add(action);
    }

    public void AddRange(IEnumerable<IBotAction> actions)
    {
        _actions.AddRange(actions);
    }

    public async Task ExecuteAsync(BotEngine engine, CancellationToken cancellationToken = default)
    {
        foreach (var action in _actions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await engine.ExecuteAsync(action, cancellationToken);
        }
    }
}

public sealed class BotScriptRunner : IDisposable
{
    private readonly BotEngine _engine;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    public BotScriptRunner(BotEngine engine)
    {
        _engine = engine;
    }

    public bool IsRunning => _runTask is { IsCompleted: false };

    public void Start(BotScript script, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = Task.Run(() => script.ExecuteAsync(_engine, _cts.Token), _cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        Stop();
    }
}
