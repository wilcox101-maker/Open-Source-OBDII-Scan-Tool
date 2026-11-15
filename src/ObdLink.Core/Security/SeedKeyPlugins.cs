using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Security;

/// <summary>
/// v0.7.0: user-supplied SecurityAccess (0x27) seed→key plug-ins.
/// The project ships NO algorithms or keys. A plug-in is a .NET 8 class library with one or more public classes that implement
/// <see cref="ISeedKeyProvider"/> (parameterless constructor). It is only loaded when the feature is enabled AND the file's
/// SHA-256 is in the trusted list, which the user adds explicitly. A plug-in runs with the app's full rights.
/// </summary>
public sealed class SeedKeyOptions
{
    /// <summary>Master switch. Default OFF; no plug-in file is opened while off.</summary>
    public bool Enabled { get; set; }
    /// <summary>SHA-256 (hex) of plug-in files the user approved. A changed file has a new hash and is ignored until approved again.</summary>
    public List<string> TrustedSha256 { get; set; } = new();
    /// <summary>SecurityAccess level requested before an As-Built write (odd, e.g. 0x01 or 0x03). Null = do not unlock.</summary>
    public byte? AsBuiltLevel { get; set; }

    public SeedKeyOptions Normalize()
    {
        TrustedSha256 = (TrustedSha256 ?? new()).Where(h => h is { Length: 64 } && h.All(Uri.IsHexDigit)).Select(h => h.ToUpperInvariant()).Distinct().ToList();
        if (AsBuiltLevel is byte l && (l % 2 == 0 || l > 0x7F)) AsBuiltLevel = null; // seed requests are odd, 0x01..0x7F (ISO 14229)
        return this;
    }
}

public sealed record SeedKeyPluginFile(string FileName, string Sha256, bool Trusted, IReadOnlyList<string> Providers, string? Error)
{
    public string Summary => !Trusted ? $"{FileName}: not approved (SHA-256 {Sha256[..12]}…), not loaded"
        : Error is not null ? $"{FileName}: failed to load: {Error}"
        : $"{FileName}: {(Providers.Count == 0 ? "no ISeedKeyProvider classes" : string.Join(", ", Providers))}";
}

/// <summary>Asks each loaded provider in turn; the first non-null key wins. A provider that throws is skipped.</summary>
public sealed class CompositeSeedKeyProvider(IReadOnlyList<ISeedKeyProvider> providers, Action<string>? log = null) : ISeedKeyProvider
{
    public IReadOnlyList<ISeedKeyProvider> Providers { get; } = providers;
    public async Task<byte[]?> ComputeKeyAsync(uint moduleTxId, byte level, byte[] seed, CancellationToken ct = default)
    {
        foreach (var p in Providers)
        {
            try
            {
                var k = await p.ComputeKeyAsync(moduleTxId, level, seed, ct).ConfigureAwait(false);
                if (k is { Length: > 0 }) { log?.Invoke($"Seed/key: {p.GetType().FullName} produced a key for 0x{moduleTxId:X3} level 0x{level:X2}"); return k; }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { log?.Invoke($"Seed/key: {p.GetType().FullName} failed for 0x{moduleTxId:X3}: {ex.Message}"); }
        }
        return null;
    }
}

public static class SeedKeyPlugins
{
    public static string Sha256Of(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(s));
    }

    /// <summary>
    /// Loads approved plug-ins from <paramref name="dir"/> (top level, *.dll). Files whose hash is not trusted are listed but never opened
    /// as code. Returns the composite provider (empty when disabled or nothing approved) and one entry per file.
    /// </summary>
    public static (CompositeSeedKeyProvider Provider, IReadOnlyList<SeedKeyPluginFile> Files) Load(string dir, SeedKeyOptions opt, Action<string>? log = null)
    {
        var files = new List<SeedKeyPluginFile>();
        var providers = new List<ISeedKeyProvider>();
        if (!opt.Enabled || !Directory.Exists(dir)) return (new CompositeSeedKeyProvider(providers, log), files);
        var trusted = new HashSet<string>(opt.Normalize().TrustedSha256, StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(path);
            string sha;
            try { sha = Sha256Of(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { files.Add(new(name, new string('0', 64), false, Array.Empty<string>(), ex.Message)); continue; }
            if (!trusted.Contains(sha)) { files.Add(new(name, sha, false, Array.Empty<string>(), null)); log?.Invoke($"Seed/key plug-in {name} is not approved; not loaded"); continue; }
            try
            {
                // Hash is re-checked on the bytes actually loaded, so a file swapped between hashing and loading is refused.
                var bytes = File.ReadAllBytes(path);
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(sha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("file changed while loading");
                var alc = new PluginContext(name);
                var asm = alc.LoadFromStream(new MemoryStream(bytes));
                var found = new List<string>();
                foreach (var t in SafeTypes(asm).Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && typeof(ISeedKeyProvider).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null))
                {
                    providers.Add((ISeedKeyProvider)Activator.CreateInstance(t)!);
                    found.Add(t.FullName ?? t.Name);
                }
                files.Add(new(name, sha, true, found, null));
                log?.Invoke($"Seed/key plug-in {name} loaded: {(found.Count == 0 ? "no providers" : string.Join(", ", found))}");
            }
            catch (Exception ex) { files.Add(new(name, sha, true, Array.Empty<string>(), ex.Message)); log?.Invoke($"Seed/key plug-in {name} failed to load: {ex.Message}"); }
        }
        return (new CompositeSeedKeyProvider(providers, log), files);
    }

    /// <summary>Copies a plug-in into <paramref name="dir"/> under a safe file name and returns (destination, SHA-256). Does NOT approve it.</summary>
    public static (string Path, string Sha256) Install(string source, string dir)
    {
        if (!source.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A seed/key plug-in must be a .dll");
        Directory.CreateDirectory(dir);
        var dest = System.IO.Path.Combine(dir, SafePaths.Token(System.IO.Path.GetFileNameWithoutExtension(source)) + ".dll");
        if (!SafePaths.IsUnder(dir, dest)) throw new InvalidOperationException("Plug-in path escaped the plug-in folder");
        File.Copy(source, dest, overwrite: true);
        return (dest, Sha256Of(dest));
    }

    private static IEnumerable<Type> SafeTypes(Assembly a)
    {
        try { return a.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }

    /// <summary>Own load context per plug-in; ObdLink.Core and framework assemblies resolve to the app's copies (shared interface type).</summary>
    private sealed class PluginContext(string name) : AssemblyLoadContext("seedkey:" + name, isCollectible: false)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
