using OogL2Client.Networking;

namespace OogL2Client.Bot;

public interface IBotAction
{
    string Name { get; }
    Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default);
}

public abstract class BotAction : IBotAction
{
    public abstract string Name { get; }

    public abstract Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default);
}

public sealed class MoveToLocationAction : BotAction
{
    public MoveToLocationAction(int x, int y, int z, int heading = 0)
    {
        X = x;
        Y = y;
        Z = z;
        Heading = heading;
    }

    public int X { get; }
    public int Y { get; }
    public int Z { get; }
    public int Heading { get; }

    public override string Name => "MoveToLocation";

    public override async Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default)
    {
        await connection.SendMoveToLocationAsync(X, Y, Z, Heading, cancellationToken: cancellationToken);
        context.X = X;
        context.Y = Y;
        context.Z = Z;
        context.LastUpdatedUtc = DateTime.UtcNow;
    }
}

public sealed class StopMovementAction : BotAction
{
    public override string Name => "StopMovement";

    public override async Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default)
    {
        await connection.SendStopMoveAsync();
        context.LastUpdatedUtc = DateTime.UtcNow;
    }
}

public sealed class AttackTargetAction : BotAction
{
    public AttackTargetAction(int targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    public int TargetObjectId { get; }

    public override string Name => "AttackTarget";

    public override async Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default)
    {
        await connection.SendAttackAsync(TargetObjectId);
        context.TargetObjectId = TargetObjectId;
        context.LastUpdatedUtc = DateTime.UtcNow;
    }
}

public sealed class CastSkillAction : BotAction
{
    public CastSkillAction(int skillId, int targetObjectId, int ctrlPressed = 0, int shiftPressed = 0)
    {
        SkillId = skillId;
        TargetObjectId = targetObjectId;
        CtrlPressed = ctrlPressed;
        ShiftPressed = shiftPressed;
    }

    public int SkillId { get; }
    public int TargetObjectId { get; }
    public int CtrlPressed { get; }
    public int ShiftPressed { get; }

    public override string Name => "CastSkill";

    public override async Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default)
    {
        await connection.SendUseSkillAsync(SkillId, TargetObjectId, CtrlPressed, ShiftPressed);
        context.TargetObjectId = TargetObjectId;
        context.LastUpdatedUtc = DateTime.UtcNow;
    }
}

public sealed class UseItemAction : BotAction
{
    public UseItemAction(int itemObjectId, int itemId, int targetObjectId = 0, int itemCount = 1)
    {
        ItemObjectId = itemObjectId;
        ItemId = itemId;
        TargetObjectId = targetObjectId;
        ItemCount = itemCount;
    }

    public int ItemObjectId { get; }
    public int ItemId { get; }
    public int TargetObjectId { get; }
    public int ItemCount { get; }

    public override string Name => "UseItem";

    public override async Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default)
    {
        await connection.SendUseItemAsync(ItemObjectId, ItemId, TargetObjectId, ItemCount);
        context.TargetObjectId = TargetObjectId;
        context.LastUpdatedUtc = DateTime.UtcNow;
    }
}

public sealed class RequestTargetAction : BotAction
{
    public RequestTargetAction(int targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    public int TargetObjectId { get; }

    public override string Name => "RequestTarget";

    public override async Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default)
    {
        await connection.SendRequestTargetAsync(TargetObjectId);
        context.TargetObjectId = TargetObjectId;
        context.LastUpdatedUtc = DateTime.UtcNow;
    }
}

public sealed class AssistTargetAction : BotAction
{
    public AssistTargetAction(int targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    public int TargetObjectId { get; }

    public override string Name => "AssistTarget";

    public override async Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default)
    {
        await connection.SendAssistTargetAsync(TargetObjectId);
        context.TargetObjectId = TargetObjectId;
        context.LastUpdatedUtc = DateTime.UtcNow;
    }
}

public sealed class WaitAction : BotAction
{
    public WaitAction(TimeSpan delay)
    {
        Delay = delay;
    }

    public TimeSpan Delay { get; }

    public override string Name => "Wait";

    public override async Task ExecuteAsync(BotContext context, L2MobiusConnection connection, CancellationToken cancellationToken = default)
    {
        await Task.Delay(Delay, cancellationToken);
        context.LastUpdatedUtc = DateTime.UtcNow;
    }
}
