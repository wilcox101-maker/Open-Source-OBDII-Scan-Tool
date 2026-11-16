using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// KB-R7: no security keys or algorithms in the public repo (Dale, 2026-11-16 10:22 ET). Seed/key is user input through a
/// plug-in DLL that lives outside this repo. A mistaken commit to a public repo is permanent (history, forks), so CI fails on:
/// a new <c>ISeedKeyProvider</c> implementation, any committed binary, a plug-in folder in the tree, or the name of the
/// private resources repo (matched by hash, so this file doesn't name it either).
/// </summary>
public class KeyGuardTests
{
    /// <summary>
    /// The only implementations allowed in this repo: infrastructure, the documented sample, and DEMO/simulator stand-ins whose
    /// "key" is a trivial function of the seed. Adding one here is a deliberate, reviewed change. A real provider belongs in
    /// the private repo, never here.
    /// </summary>
    private static readonly HashSet<string> Allowed = new()
    {
        "src/ObdLink.Core/Security/SeedKeyPlugins.cs:CompositeSeedKeyProvider",   // chains the user's plug-ins
        "src/ObdLink.Core/Uds/UdsClient.cs:NullSeedKeyProvider",                   // no plug-in: refuses
        "src/ObdLink.App/FlashViewModel.cs:DemoKey",                               // DEMO bootloader: seed reversed
        "tools/SeedKeyPluginSample/DemoSeedKeyProvider.cs:DemoSeedKeyProvider",    // the documented sample (simulator only)
        "tests/ObdLink.Core.Tests/FlashBusQuietTests.cs:SimKey",
        "tests/ObdLink.Core.Tests/FlashRecoveryTests.cs:SimKey",
        "tests/ObdLink.Core.Tests/FlashEngineTests.cs:SimKey",
        "tests/ObdLink.Core.Tests/FlashEngineTests.cs:WrongKey",
        "tests/ObdLink.Core.Tests/FlashSessionTests.cs:SimKey",
        "tests/ObdLink.Core.Tests/SeedKeyPluginTests.cs:Throws",
        "tests/ObdLink.Core.Tests/SeedKeyPluginTests.cs:Wrong",
        "tests/ObdLink.Core.Tests/Tests.cs:FakeKey",
    };

    /// <summary>SHA-256 of the lower-cased private repo name (see <c>claude/lanes.md</c>, outside this repo).</summary>
    private const string PrivateRepoNameSha256 = "4c882be78570975f28bad82306f3f75ad4c9b8916f87717f5261bdfffb73f9d0";

    private static readonly string[] SkipDirs = { ".git", "bin", "obj", "artifacts", "TestResults", ".vs" };
    private static readonly string[] TextExt = { ".cs", ".md", ".txt", ".yml", ".yaml", ".json", ".xaml", ".csproj", ".sln", ".props", ".targets", ".ps1", ".sh", ".cmd", ".bat", ".xml", ".ini", ".abt", ".csv", ".html", ".config" };
    private static readonly string[] BinaryExt = { ".dll", ".exe", ".so", ".dylib", ".pdb", ".lib", ".a" };

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "ObdLink.sln"))) d = d.Parent;
        return d!.FullName;
    }

    internal static IEnumerable<string> RepoFiles(string root)
    {
        var stack = new Stack<string>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
                if (!SkipDirs.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase)) stack.Push(sub);
            foreach (var f in Directory.EnumerateFiles(dir)) yield return f;
        }
    }

    private static string Rel(string root, string f) => Path.GetRelativePath(root, f).Replace('\\', '/');

    internal static IEnumerable<string> Implementations(string relPath, string source) =>
        Regex.Matches(source, @"\b(?:class|record|struct)\s+(\w+)[^{;]*?:\s*[^{;]*\bISeedKeyProvider\b")
            .Select(m => $"{relPath}:{m.Groups[1].Value}");

    internal static bool NamesPrivateRepo(string text, string sha256 = PrivateRepoNameSha256)
    {
        foreach (Match m in Regex.Matches(text, @"[A-Za-z0-9][A-Za-z0-9._-]{8,}"))
        {
            var token = m.Value.ToLowerInvariant().TrimEnd('.');
            foreach (var t in new[] { token, token.EndsWith(".git") ? token[..^4] : token })
                if (Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(t))).Equals(sha256, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        return false;
    }

    [Fact]
    public void Only_the_listed_seed_key_providers_exist_in_this_repo()
    {
        var root = Root();
        var found = RepoFiles(root).Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => Implementations(Rel(root, f), File.ReadAllText(f))).ToHashSet();
        var unexpected = found.Except(Allowed).ToList();
        Assert.True(unexpected.Count == 0,
            "New ISeedKeyProvider implementation(s): " + string.Join(", ", unexpected)
            + ". Real seed/key providers live in the private resources repo and are installed as plug-in DLLs; never commit one here.");
        Assert.Empty(Allowed.Except(found)); // keep the list honest when a stand-in is removed
    }

    [Fact]
    public void No_binaries_and_no_plugin_folders_are_committed()
    {
        var root = Root();
        var files = RepoFiles(root).Select(f => Rel(root, f)).ToList();
        Assert.Empty(files.Where(f => BinaryExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)));
        Assert.Empty(files.Where(f => f.Contains("/plugins/", StringComparison.OrdinalIgnoreCase) || f.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void The_private_resources_repo_is_never_named_in_this_repo()
    {
        var root = Root();
        var hits = RepoFiles(root).Where(f => TextExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => new FileInfo(f).Length < 4_000_000 && NamesPrivateRepo(File.ReadAllText(f)))
            .Select(f => Rel(root, f)).ToList();
        Assert.True(hits.Count == 0, "The private resources repo is named in: " + string.Join(", ", hits) + ". Keep it out of the public repo.");
    }

    [Fact]
    public void Gitignore_keeps_plugin_folders_and_binaries_out()
    {
        var gi = File.ReadAllLines(Path.Combine(Root(), ".gitignore")).Select(l => l.Trim()).ToList();
        Assert.Contains("**/Data/plugins/", gi);
        Assert.Contains("*.dll", gi);
    }

    [Fact]
    public void The_guards_catch_what_they_are_for()
    {
        // Detection self-test, so a regex change can't silently disable the guard.
        const string Iface = "ISeedKey" + "Provider"; // split so the source scan above doesn't flag this file
        Assert.Equal(new[] { "x.cs:FordKey" }, Implementations("x.cs", $"internal sealed class FordKey : IDisposable, {Iface} {{ }}"));
        Assert.Equal(new[] { "x.cs:K" }, Implementations("x.cs", $"public sealed record K(int A) : ObdLink.Core.Uds.{Iface};"));
        Assert.Empty(Implementations("x.cs", "// implement ISeedKeyProvider in your own DLL"));
        // The matcher is exercised with a stand-in name, so the real name never appears here, not even in pieces.
        var standIn = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("example-private-keys")));
        Assert.True(NamesPrivateRepo("see https://github.com/someone/Example-Private-Keys.git", standIn));
        Assert.True(NamesPrivateRepo("clone example-private-keys.", standIn));
        Assert.False(NamesPrivateRepo("example-public-docs", standIn));
        Assert.False(NamesPrivateRepo("Open-Source-OBDII-Scan-Tool"));
        Assert.Equal(64, PrivateRepoNameSha256.Length);
    }
}
