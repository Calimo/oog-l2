namespace OogL2Client.World;

public sealed class WorldState
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<int, WorldObject> _objects = new();

    public WorldObject? Self { get; private set; }
    public IReadOnlyCollection<WorldObject> Objects => _objects.Values;

    public void SetSelf(WorldObject self)
    {
        lock (_syncRoot)
        {
            Self = self;
            _objects[self.ObjectId] = self;
        }
    }

    public void Upsert(WorldObject worldObject)
    {
        lock (_syncRoot)
        {
            _objects[worldObject.ObjectId] = worldObject;
            if (string.Equals(worldObject.Name, "self", StringComparison.OrdinalIgnoreCase))
            {
                Self = worldObject;
            }
        }
    }

    public void Remove(int objectId)
    {
        lock (_syncRoot)
        {
            _objects.Remove(objectId);
        }
    }

    public WorldObject? Get(int objectId)
    {
        lock (_syncRoot)
        {
            return _objects.TryGetValue(objectId, out var value) ? value : null;
        }
    }

    public int? GetAggroTargetObjectId(int objectId)
    {
        var obj = Get(objectId);
        if (obj is null)
        {
            return null;
        }

        var isAggroed = obj.IsAggroed || obj.AggroTargetObjectId > 0;
        return isAggroed ? obj.AggroTargetObjectId : null;
    }

    public bool IsMonsterAggroedOn(int monsterObjectId, int targetObjectId)
    {
        var target = GetAggroTargetObjectId(monsterObjectId);
        return target.HasValue && target.Value == targetObjectId && targetObjectId > 0;
    }

    public IEnumerable<WorldObject> ThreatsTargeting(int targetObjectId)
    {
        lock (_syncRoot)
        {
            return _objects.Values
                .Where(o => (o.IsAggroed || o.AggroTargetObjectId > 0) && o.AggroTargetObjectId == targetObjectId)
                .OrderBy(o => o.Name)
                .ToList();
        }
    }

    public IEnumerable<WorldObject> Nearby(int x, int y, int radius = 2000)
    {
        lock (_syncRoot)
        {
            return _objects.Values
                .Where(o => o.IsVisible)
                .Where(o => Math.Abs(o.X - x) <= radius && Math.Abs(o.Y - y) <= radius)
                .ToList();
        }
    }

    public void ClearExpired(TimeSpan maxAge)
    {
        lock (_syncRoot)
        {
            var now = DateTime.UtcNow;
            foreach (var item in _objects.Where(kvp => now - kvp.Value.LastSeenUtc > maxAge).ToList())
            {
                _objects.Remove(item.Key);
            }
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _objects.Clear();
            Self = null;
        }
    }
}
