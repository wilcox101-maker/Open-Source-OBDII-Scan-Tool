namespace ObdLink.Core.AsBuilt;

/// <summary>Whether a module answered As-Built DID DE00. Unknown means this session has not checked yet.</summary>
public enum AsBuiltSupport { Unknown, Supported, Unsupported }

/// <summary>
/// A module answered ReadDataByIdentifier for an As-Built DID with NRC 0x31 (requestOutOfRange).
/// This is a normal "no As-Built" result, not a failed request.
/// </summary>
public sealed class AsBuiltNotSupportedException : Exception
{
    public uint Module { get; }
    public ushort Did { get; }
    public byte Nrc => 0x31;

    public AsBuiltNotSupportedException(uint module, ushort did, string? acronym = null)
        : base(Describe(acronym, module, did))
    {
        Module = module;
        Did = did;
    }

    public static string Describe(string? acronym, uint module, ushort did)
    {
        var who = string.IsNullOrWhiteSpace(acronym) ? $"Module 0x{module:X3}" : $"{acronym} (0x{module:X3})";
        return $"{who} has no As-Built data. DID 0x{did:X4} is not supported (NRC 0x31 requestOutOfRange). Read, backup, and write stay off for this module.";
    }
}

/// <summary>Which modules are offered on the As-Built page, and which actions stay available.</summary>
public static class AsBuiltSupportPolicy
{
    public static AsBuiltSupport FromDe00(bool readable) => readable ? AsBuiltSupport.Supported : AsBuiltSupport.Unsupported;

    public static bool ActionsEnabled(AsBuiltSupport support) => support != AsBuiltSupport.Unsupported;

    /// <summary><paramref name="found"/> is the scan text: "YES", "no", or "not scanned".</summary>
    public static bool ShowInList(AsBuiltSupport support, bool guessed, bool showAll, string found)
    {
        if (found == "no") return false;
        if (showAll) return true;
        return support switch
        {
            AsBuiltSupport.Supported => true,
            AsBuiltSupport.Unsupported => false,
            _ => guessed,
        };
    }
}
