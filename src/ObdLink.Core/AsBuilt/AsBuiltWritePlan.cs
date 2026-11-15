namespace ObdLink.Core.AsBuilt;

/// <summary>One line as edited in the As-Built grid: data hex (no checksum) and the checksum the user typed.</summary>
public sealed record AsBuiltEditLine(AsBuiltAddress Address, string DataHex, string EnteredChecksum);

/// <summary>
/// v0.6.1 audit: builds the block to write from the grid. The grid can hold several modules and blocks
/// (Read all blocks, Import .abt); only the lines of the selected module and block are used, they must run 01..n with no
/// gaps or duplicates, and every typed checksum must match its data (as in FORScan, the checksum is the typo check).
/// </summary>
public static class AsBuiltWritePlan
{
    public static AsBuiltBlock BuildBlock(ushort module, byte block, IEnumerable<AsBuiltEditLine> rows)
    {
        var mine = rows.Where(r => r.Address.Module == module && r.Address.Block == block).OrderBy(r => r.Address.Line).ToList();
        var where = $"{module:X3}-{block:X2}";
        if (mine.Count == 0)
            throw new InvalidOperationException($"The grid has no lines for {where}. Read that block first. Nothing was written.");
        var data = new List<byte>();
        int? width = null;
        for (int i = 0; i < mine.Count; i++)
        {
            var r = mine[i];
            int ordinal;
            try { ordinal = AsBuiltAddress.FromBcd(r.Address.Line); }
            catch (FormatException) { throw new InvalidDataException($"{r.Address}: line number is not a valid As-Built line. Nothing was written."); }
            if (ordinal != i + 1)
                throw new InvalidDataException($"{where}: lines must run 01, 02, 03... with none missing or repeated (found {r.Address} at position {i + 1}). Nothing was written.");
            byte[] bytes;
            try { bytes = Convert.FromHexString(r.DataHex.Replace(" ", "")); }
            catch (FormatException) { throw new InvalidDataException($"{r.Address}: data '{r.DataHex}' is not whole hex bytes. Nothing was written."); }
            if (bytes.Length == 0) throw new InvalidDataException($"{r.Address}: data is empty. Nothing was written.");
            var expected = AsBuiltLine.ComputeChecksum(r.Address, bytes);
            if (!TryParseByte(r.EnteredChecksum, out var entered) || entered != expected)
                throw new InvalidDataException($"{r.Address}: checksum '{r.EnteredChecksum.Trim()}' does not match the data (it should be {expected:X2}). Check the data, then type the matching checksum. Nothing was written.");
            width ??= bytes.Length;
            data.AddRange(bytes);
        }
        return new AsBuiltBlock { Module = module, Block = block, Data = data.ToArray(), BytesPerLine = width ?? 5 };
    }

    public static bool TryParseByte(string? s, out byte b)
    {
        b = 0;
        var t = (s ?? "").Trim();
        return t.Length is 1 or 2 && byte.TryParse(t, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out b);
    }

    /// <summary>Number of bytes that differ (same length assumed).</summary>
    public static int CountChanges(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        int n = 0;
        for (int i = 0; i < Math.Min(before.Length, after.Length); i++) if (before[i] != after[i]) n++;
        return n;
    }
}

/// <summary>VIN matching for restores. Backups made by the app hold the full VIN; batch files hold a masked one (11 X + last 6).</summary>
public static class VinMatch
{
    public static bool IsKnown(string? vin) => vin is { Length: 17 } && !vin.Equals("UNKNOWNVIN", StringComparison.OrdinalIgnoreCase);
    public static bool IsMasked(string vin) => vin.StartsWith("XXXXXXXXXXX", StringComparison.OrdinalIgnoreCase);

    /// <summary>Full VINs compare in full; if either side is masked only the last 6 characters can be compared.</summary>
    public static bool Same(string? a, string? b)
    {
        if (!IsKnown(a) || !IsKnown(b)) return false;
        if (IsMasked(a!) || IsMasked(b!)) return a![^6..].Equals(b![^6..], StringComparison.OrdinalIgnoreCase);
        return a!.Equals(b, StringComparison.OrdinalIgnoreCase);
    }
}
