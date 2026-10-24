namespace ObdLink.Core.Dashboard;

/// <summary>
/// Priority polling scheduler. Each item has a base interval from its priority (0 = fast, 1 = normal, 2 = slow) times a shared
/// adaptive factor. When a polling cycle overruns its budget (slow adapter/bus), the factor grows; when there is headroom it shrinks
/// back toward 1. Items that fail repeatedly are backed off. Pure logic with an injected clock so it is unit-testable.
/// </summary>
public sealed class PollScheduler
{
    public sealed class Item
    {
        public required string Key { get; init; }
        public int Priority { get; init; }
        public long NextDueMs { get; set; }
        public int Failures { get; set; }
        public bool Disabled { get; set; }
    }
    private readonly List<Item> _items = new();
    public int BaseMs { get; set; } = 250;
    public double Factor { get; private set; } = 1.0;
    public double MaxFactor { get; set; } = 8;
    public int MaxFailures { get; set; } = 3;
    public IReadOnlyList<Item> Items => _items;
    public static readonly int[] PriorityMultiplier = { 1, 2, 8 };

    public void Add(string key, int priority) { if (_items.All(i => i.Key != key)) _items.Add(new Item { Key = key, Priority = Math.Clamp(priority, 0, 2) }); }
    public void Clear() => _items.Clear();
    public long IntervalMs(Item i) => (long)(BaseMs * PriorityMultiplier[i.Priority] * Factor * (1 << Math.Min(i.Failures, 4)));

    /// <summary>Items due at <paramref name="nowMs"/>, highest priority first, at most <paramref name="max"/>.</summary>
    public List<Item> Due(long nowMs, int max = 6) =>
        _items.Where(i => !i.Disabled && i.NextDueMs <= nowMs).OrderBy(i => i.Priority).ThenBy(i => i.NextDueMs).Take(max).ToList();

    public void Completed(Item i, long nowMs, bool ok)
    {
        if (ok) i.Failures = 0; else if (++i.Failures >= MaxFailures * 3) i.Disabled = true;
        i.NextDueMs = nowMs + IntervalMs(i);
    }

    /// <summary>Adaptive rate: call after each cycle with how long it took.</summary>
    public void CycleTook(long elapsedMs)
    {
        if (elapsedMs > BaseMs) Factor = Math.Min(MaxFactor, Factor * 1.25);
        else if (elapsedMs < BaseMs / 2) Factor = Math.Max(1.0, Factor / 1.1);
    }
}
