using System.Globalization;
using System.Text.RegularExpressions;

namespace ObdLink.Core.LiveData;

public enum TriggerOp { Above, Below }

/// <summary>
/// v0.12.0: "fire when <see cref="Key"/> goes above / below <see cref="Threshold"/>". Edge-triggered with hysteresis: after it
/// fires it re-arms only once the value is back past the threshold by <see cref="Hysteresis"/>, so a value hovering on the line
/// fires once, not every cycle.
/// </summary>
public sealed record SignalTrigger(string Key, TriggerOp Op, double Threshold, double Hysteresis = 0)
{
    public string Text => $"{Key} {(Op == TriggerOp.Above ? ">" : "<")} {Threshold.ToString("0.###", CultureInfo.InvariantCulture)}" +
                          (Hysteresis > 0 ? $" (±{Hysteresis.ToString("0.###", CultureInfo.InvariantCulture)})" : "");

    private static readonly Regex Rx = new(@"^\s*([A-Za-z0-9_.\-]+)\s*([<>])\s*(-?\d+(?:\.\d+)?)\s*(?:hys(?:teresis)?\s*(\d+(?:\.\d+)?))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Parses "rpm &gt; 3000" or "ect &lt; 70 hys 2". Throws <see cref="FormatException"/> with an example on failure.</summary>
    public static SignalTrigger Parse(string text)
    {
        var m = Rx.Match(text ?? "");
        if (!m.Success) throw new FormatException($"'{text}' is not a trigger. Examples: rpm > 3000, ect < 70 hys 2");
        double th = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
        double hy = m.Groups[4].Success ? double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        return new SignalTrigger(m.Groups[1].Value, m.Groups[2].Value == ">" ? TriggerOp.Above : TriggerOp.Below, th, hy);
    }
}

public sealed record TriggerHit(SignalTrigger Trigger, DateTime T, double Value);

/// <summary>Evaluates triggers against poll cycles. A signal absent from a cycle leaves its trigger's state unchanged.</summary>
public sealed class TriggerMonitor
{
    private readonly List<(SignalTrigger T, bool Armed)> _items = new();

    public IReadOnlyList<SignalTrigger> Triggers => _items.Select(i => i.T).ToList();

    public void Add(SignalTrigger t)
    {
        if (t.Hysteresis < 0) throw new ArgumentOutOfRangeException(nameof(t), "Hysteresis cannot be negative");
        _items.Add((t, true));
    }
    public bool Remove(SignalTrigger t) { int i = _items.FindIndex(x => x.T == t); if (i < 0) return false; _items.RemoveAt(i); return true; }
    public void Clear() => _items.Clear();
    /// <summary>Arms every trigger again (for example when polling or a replay restarts).</summary>
    public void Rearm() { for (int i = 0; i < _items.Count; i++) _items[i] = (_items[i].T, true); }

    public IReadOnlyList<TriggerHit> Apply(IReadOnlyDictionary<string, double> cycle, DateTime t)
    {
        List<TriggerHit>? hits = null;
        for (int i = 0; i < _items.Count; i++)
        {
            var (tr, armed) = _items[i];
            if (!TryGet(cycle, tr.Key, out var v)) continue;
            bool past = tr.Op == TriggerOp.Above ? v > tr.Threshold : v < tr.Threshold;
            bool clear = tr.Op == TriggerOp.Above ? v <= tr.Threshold - tr.Hysteresis : v >= tr.Threshold + tr.Hysteresis;
            if (armed && past) { (hits ??= new()).Add(new TriggerHit(tr, t, v)); _items[i] = (tr, false); }
            else if (!armed && clear) _items[i] = (tr, true);
        }
        return (IReadOnlyList<TriggerHit>?)hits ?? Array.Empty<TriggerHit>();
    }

    private static bool TryGet(IReadOnlyDictionary<string, double> c, string key, out double v)
    {
        if (c.TryGetValue(key, out v) && double.IsFinite(v)) return true;
        foreach (var (k, x) in c) if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase) && double.IsFinite(x)) { v = x; return true; }
        return false;
    }
}
