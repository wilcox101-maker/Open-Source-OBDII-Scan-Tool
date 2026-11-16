using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// v0.17.x: WPF reports a broken binding only at runtime (a silent trace line), and this repo's CI cannot run WPF. This test
/// reads MainWindow.xaml, follows DataContext changes, and checks every plain {Binding Path} against the public members of the
/// view-model class it binds to (found in the App and Core sources). Bindings inside templates and styles (their data context is
/// a list item or a control), and bindings with ElementName / RelativeSource / Source, are skipped. It guards view-model
/// refactors such as splitting MainViewModel.
/// </summary>
public class XamlBindingTests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] TemplateTags = { "DataTemplate", "ControlTemplate", "ItemsPanelTemplate", "HierarchicalDataTemplate", "Style", "Setter" };

    /// <summary>Class name → (member name → declared type text) for public properties and fields in the given sources.</summary>
    private static Dictionary<string, Dictionary<string, string>> Members(IEnumerable<string> files)
    {
        var map = new Dictionary<string, Dictionary<string, string>>();
        // Declarations only at the start of a line, so "record CSV" inside a string or a comment is not a class.
        var classRx = new Regex(@"^\s*(?:(?:public|internal|private|protected|sealed|static|abstract|partial|readonly)\s+)*(?:class|record(?:\s+struct)?)\s+(\w+)", RegexOptions.Multiline);
        // public [static] [readonly] [required] Type Name { ... | => ... | = ... | ;   (Type may be generic / nullable / array)
        var memberRx = new Regex(@"public\s+(?:static\s+|readonly\s+|required\s+|override\s+|virtual\s+|new\s+)*([\w<>\[\],?. ]+?)\s+(\w+)\s*(?:\{|=>|=|;)");
        var recordParamRx = new Regex(@"^\s*(?:(?:public|internal|sealed|readonly)\s+)*record(?:\s+struct)?\s+(\w+)\s*\(([^)]*)\)", RegexOptions.Multiline);
        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            // Split the file at class/record declarations; members belong to the nearest preceding one (good enough here).
            var decls = classRx.Matches(text).Cast<Match>().ToList();
            for (int i = 0; i < decls.Count; i++)
            {
                var name = decls[i].Groups[1].Value;
                int start = decls[i].Index, end = i + 1 < decls.Count ? decls[i + 1].Index : text.Length;
                var body = text[start..end];
                if (!map.TryGetValue(name, out var members)) map[name] = members = new();
                foreach (Match m in memberRx.Matches(body)) members.TryAdd(m.Groups[2].Value, m.Groups[1].Value.Trim());
            }
            foreach (Match r in recordParamRx.Matches(text))
            {
                if (!map.TryGetValue(r.Groups[1].Value, out var members)) map[r.Groups[1].Value] = members = new();
                foreach (var p in r.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = p.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2) members.TryAdd(parts[^1], string.Join(' ', parts[..^1]));
                }
            }
        }
        return map;
    }

    private static string? BindingPath(string value)
    {
        var m = Regex.Match(value, @"^\{Binding\s*(.*)\}$", RegexOptions.Singleline);
        if (!m.Success) return null;
        var inner = m.Groups[1].Value.Trim();
        if (Regex.IsMatch(inner, @"\b(ElementName|RelativeSource|Source)\s*=")) return null;
        if (inner.Length == 0) return "";
        var first = inner.Split(',')[0].Trim();
        if (first.StartsWith("Path=", StringComparison.Ordinal)) return first[5..].Trim();
        if (first.Contains('=')) return ""; // only named parameters, e.g. {Binding Mode=OneWay}: binds to the context itself
        return first;
    }

    /// <summary>Resolves a dotted path from <paramref name="cls"/>. Returns the error text, or null when it resolves (or leaves known types).</summary>
    private static string? Check(Dictionary<string, Dictionary<string, string>> types, string cls, string path, out string? endType)
    {
        endType = cls;
        foreach (var raw in path.Split('.'))
        {
            var seg = Regex.Replace(raw, @"\[.*\]$", "");
            if (seg.Length == 0) continue;
            if (endType is null || !types.TryGetValue(endType, out var members)) { endType = null; return null; } // left our own types
            if (!members.TryGetValue(seg, out var t)) return $"'{seg}' is not a public property of {endType}";
            var simple = Regex.Replace(t, @"[?\[\]]", "");
            simple = simple.Contains('<') ? null! : simple.Split('.').Last();
            endType = simple;
        }
        return null;
    }

    private static Dictionary<string, Dictionary<string, string>> AppTypes() =>
        Members(Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")));

    /// <summary>Walks <paramref name="doc"/> from <paramref name="rootContext"/>; returns (bindings checked, errors).</summary>
    private static (int Checked, List<string> Errors) Verify(XDocument doc, string rootContext, Dictionary<string, Dictionary<string, string>> types)
    {
        var errors = new List<string>();
        int checkedCount = 0;

        void Walk(XElement e, string? ctx)
        {
            if (TemplateTags.Contains(e.Name.LocalName)) return;
            // A DataContext on this element applies to its own other attributes too.
            var dc = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "DataContext");
            if (dc != null && BindingPath(dc.Value) is null)
                // {Binding DataContext, RelativeSource={RelativeSource AncestorType=Window}} = back to the window's context;
                // any other non-plain DataContext makes the subtree's context unknown, so it is not checked.
                ctx = Regex.IsMatch(dc.Value, @"^\{Binding\s+DataContext\s*,\s*RelativeSource=\{RelativeSource\s+AncestorType=Window\}\s*\}$") ? rootContext : null;
            else if (dc != null && BindingPath(dc.Value) is string dcPath && ctx is not null)
            {
                var err = Check(types, ctx, dcPath, out var newCtx);
                if (err != null) errors.Add($"line {((System.Xml.IXmlLineInfo)e).LineNumber} DataContext: {err}");
                ctx = err == null ? newCtx : null;
            }
            if (ctx is not null)
                foreach (var a in e.Attributes().Where(a => a.Name.LocalName != "DataContext"))
                {
                    if (BindingPath(a.Value) is not string p || p.Length == 0) continue;
                    checkedCount++;
                    var err = Check(types, ctx, p, out _);
                    if (err != null) errors.Add($"line {((System.Xml.IXmlLineInfo)e).LineNumber} {e.Name.LocalName}.{a.Name.LocalName}=\"{a.Value}\": {err}");
                }
            foreach (var c in e.Elements())
            {
                // Property elements such as <DataGrid.Columns> keep the context; column bindings bind to row items, so skip them.
                if (c.Name.LocalName.EndsWith(".Columns", StringComparison.Ordinal) || c.Name.LocalName.EndsWith(".ItemTemplate", StringComparison.Ordinal)) continue;
                Walk(c, ctx);
            }
        }

        Walk(doc.Root!, rootContext);
        return (checkedCount, errors);
    }

    [Theory]
    [InlineData("MainWindow.xaml", 150)]
    [InlineData("DashboardWindow.xaml", 5)]   // its DataContext is the MainViewModel (see DashboardWindow.xaml.cs)
    public void Every_window_binding_resolves(string file, int minChecked)
    {
        var types = AppTypes();
        Assert.True(types.ContainsKey("MainViewModel"));
        var doc = XDocument.Load(Path.Combine(Root(), "src", "ObdLink.App", file), LoadOptions.SetLineInfo);
        var (n, errors) = Verify(doc, "MainViewModel", types);
        Assert.True(n >= minChecked, $"only {n} bindings checked in {file}; the walker is skipping too much");
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void The_checker_catches_broken_bindings()
    {
        var xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel>
                <TextBlock Text="{Binding NoSuchProperty}"/>
                <TextBlock Text="{Binding Routines.NoSuchCommand}"/>
                <Border DataContext="{Binding Monitor}"><TextBlock Text="{Binding Status}"/><TextBlock Text="{Binding Typo}"/></Border>
                <TextBlock Text="{Binding Status, ElementName=x}"/>
                <ItemsControl><ItemsControl.ItemTemplate><DataTemplate><TextBlock Text="{Binding Anything}"/></DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
              </StackPanel>
            </Window>
            """;
        var (n, errors) = Verify(XDocument.Parse(xaml, LoadOptions.SetLineInfo), "MainViewModel", AppTypes());
        Assert.Equal(4, n);
        Assert.Equal(3, errors.Count);
        Assert.Contains(errors, e => e.Contains("NoSuchProperty"));
        Assert.Contains(errors, e => e.Contains("NoSuchCommand") && e.Contains("RoutinesViewModel"));
        Assert.Contains(errors, e => e.Contains("Typo") && e.Contains("BusMonitorViewModel"));
    }
}
