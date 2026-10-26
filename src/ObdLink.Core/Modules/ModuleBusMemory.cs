using ObdLink.Core.Elm;

namespace ObdLink.Core.Modules;

/// <summary>
/// Remembers which Ford CAN bus a module answered on (HS-CAN, HS-CAN2, or MS-CAN) and tries that bus first next time.
/// Demo sessions do not write the map, so a simulated bus cannot stick to a later real connection.
/// </summary>
public static class ModuleBusMemory
{
    public static bool ShouldPersist(bool simulatedAdapter) => !simulatedAdapter;

    public static string Key(uint txId) => txId.ToString("X3");

    public static bool TryParse(string? text, out Bus bus)
    {
        bus = default;
        return Enum.TryParse(text, out bus) && bus is Bus.HsCan or Bus.HsCan2 or Bus.MsCan;
    }

    public static void Remember(IDictionary<string, string> map, uint txId, Bus bus)
    {
        if (!TryParse(bus.ToString(), out _)) throw new ArgumentOutOfRangeException(nameof(bus));
        map[Key(txId)] = bus.ToString();
    }

    public static Bus? Recall(IReadOnlyDictionary<string, string>? map, uint txId) =>
        map != null && map.TryGetValue(Key(txId), out var text) && TryParse(text, out var bus) ? bus : null;

    /// <summary>Scan definition that uses <paramref name="effective"/> when it differs from the table.</summary>
    public static ModuleDefinition ForScan(ModuleDefinition def, Bus effective) =>
        effective == def.Bus ? def : def with { Bus = effective };

    /// <summary>Buses still worth trying after <paramref name="tried"/>, table bus first, then the other Ford buses.</summary>
    public static Bus[] Remaining(Bus tried, Bus table)
    {
        var alt = ModuleReplies.Alternates(tried);
        return new[] { table }.Concat(alt).Distinct().Where(b => b != tried && b is Bus.HsCan or Bus.HsCan2 or Bus.MsCan).ToArray();
    }
}
