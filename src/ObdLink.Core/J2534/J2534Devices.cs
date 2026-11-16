using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ObdLink.Core.J2534;

/// <summary>CPU type a DLL was built for (PE/COFF Machine field).</summary>
public enum DllMachine { Unknown, X86, X64, Arm64 }

/// <summary>A J2534 v04.04 device as its installer registered it.</summary>
public sealed record J2534Device(string Name, string Vendor, string FunctionLibrary, bool Can, bool Iso15765, bool In32BitView)
{
    public string Display => $"{Name}{(Vendor.Length > 0 ? " (" + Vendor + ")" : "")}";
}

/// <summary>One registry key under PassThruSupport.04.04: which view it came from, its name and its values.</summary>
public sealed record J2534RegistryKey(bool In32BitView, string KeyName, IReadOnlyDictionary<string, object?> Values);

public interface IJ2534RegistrySource
{
    IEnumerable<J2534RegistryKey> PassThruKeys();
}

/// <summary>
/// Finds installed J2534 v04.04 devices. Installers register under HKLM\SOFTWARE\PassThruSupport.04.04\&lt;device&gt; with the
/// values Name, Vendor, FunctionLibrary (the DLL path) and one DWORD per supported protocol (CAN, ISO15765, …). 32-bit drivers
/// register in the 32-bit registry view (WOW6432Node on 64-bit Windows), which is where most of them are.
/// </summary>
public static class J2534Registry
{
    public const string KeyPath = @"SOFTWARE\PassThruSupport.04.04";

    public static IReadOnlyList<J2534Device> Enumerate(IJ2534RegistrySource source)
    {
        var list = new List<J2534Device>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in source.PassThruKeys())
        {
            if (Str(k.Values, "FunctionLibrary") is not { Length: > 0 } dll) continue;
            if (!seen.Add(dll)) continue; // the same driver can appear in both views
            list.Add(new J2534Device(Str(k.Values, "Name") is { Length: > 0 } n ? n : k.KeyName, Str(k.Values, "Vendor") ?? "", dll,
                Flag(k.Values, "CAN"), Flag(k.Values, "ISO15765"), k.In32BitView));
        }
        return list;
    }

    private static string? Str(IReadOnlyDictionary<string, object?> v, string name) =>
        v.FirstOrDefault(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)).Value?.ToString()?.Trim();

    private static bool Flag(IReadOnlyDictionary<string, object?> v, string name) =>
        v.FirstOrDefault(kv => string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase)).Value is int i && i != 0;
}

/// <summary>Reads both registry views on Windows. Elsewhere it finds nothing.</summary>
public sealed class WindowsJ2534Registry : IJ2534RegistrySource
{
    public IEnumerable<J2534RegistryKey> PassThruKeys() => OperatingSystem.IsWindows() ? Read() : Array.Empty<J2534RegistryKey>();

    [SupportedOSPlatform("windows")]
    private static List<J2534RegistryKey> Read()
    {
        var res = new List<J2534RegistryKey>();
        foreach (var (view, is32) in new[] { (Microsoft.Win32.RegistryView.Registry32, true), (Microsoft.Win32.RegistryView.Registry64, false) })
        {
            try
            {
                using var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, view);
                using var root = hklm.OpenSubKey(J2534Registry.KeyPath);
                if (root is null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    using var k = root.OpenSubKey(name);
                    if (k is null) continue;
                    res.Add(new J2534RegistryKey(is32, name, k.GetValueNames().ToDictionary(v => v, v => k.GetValue(v), StringComparer.OrdinalIgnoreCase)));
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { /* unreadable view: skip it */ }
        }
        return res;
    }
}

public static class DllInfo
{
    /// <summary>Reads the Machine field of a PE file (MZ header → e_lfanew → "PE\0\0" → Machine). Unknown if it is not a PE file.</summary>
    public static DllMachine Machine(Stream s)
    {
        using var r = new BinaryReader(s, System.Text.Encoding.ASCII, leaveOpen: true);
        try
        {
            if (r.ReadUInt16() != 0x5A4D) return DllMachine.Unknown; // "MZ"
            s.Seek(0x3C, SeekOrigin.Begin);
            int peOffset = r.ReadInt32();
            if (peOffset <= 0 || peOffset > s.Length - 6) return DllMachine.Unknown;
            s.Seek(peOffset, SeekOrigin.Begin);
            if (r.ReadUInt32() != 0x00004550) return DllMachine.Unknown; // "PE\0\0"
            return r.ReadUInt16() switch { 0x014C => DllMachine.X86, 0x8664 => DllMachine.X64, 0xAA64 => DllMachine.Arm64, _ => DllMachine.Unknown };
        }
        catch (EndOfStreamException) { return DllMachine.Unknown; }
    }

    public static DllMachine Machine(string path) { using var f = File.OpenRead(path); return Machine(f); }

    /// <summary>
    /// True when this process can load a DLL built for <paramref name="m"/>. A 64-bit process cannot load a 32-bit DLL (most
    /// J2534 drivers are 32-bit); those need a 32-bit helper process.
    /// </summary>
    public static bool LoadsInProcess(DllMachine m, Architecture? process = null) => (process ?? RuntimeInformation.ProcessArchitecture, m) switch
    {
        (Architecture.X86, DllMachine.X86) => true,
        (Architecture.X64, DllMachine.X64) => true,
        (Architecture.Arm64, DllMachine.Arm64) => true,
        _ => false,
    };
}
