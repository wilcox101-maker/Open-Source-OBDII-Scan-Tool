using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// Static XAML checker (runs on Linux): builds a key->element-type map over App.xaml's merged dictionaries plus window resources and
/// verifies every {StaticResource}/{DynamicResource} reference resolves to a compatible type. Catches runtime-only crashes such as
/// v0.1.1/0.1.2 "'System.Windows.Style' is not a valid value for property 'BorderBrush'" (brush "Danger" vs style "Danger").
/// </summary>
public class XamlResourceTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static string AppDir([System.Runtime.CompilerServices.CallerFilePath] string f = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", "..", "src", "ObdLink.App"));

    private static readonly string[] Dictionaries = { "Themes/Dark.xaml", "Themes/Light.xaml", "Themes/Controls.xaml" };
    private static readonly string[] Views = { "App.xaml", "MainWindow.xaml" };
    private static readonly HashSet<string> BrushProps = new() { "Background", "Foreground", "BorderBrush", "Fill", "Stroke", "CaretBrush", "SelectionBrush", "OpacityMask", "TextElement.Foreground" };
    private static readonly HashSet<string> BrushTypes = new() { "SolidColorBrush", "LinearGradientBrush", "RadialGradientBrush", "DrawingBrush", "ImageBrush", "VisualBrush" };
    private static readonly Regex Ref = new(@"^\{(Static|Dynamic)Resource\s+([A-Za-z0-9_]+)\s*\}$");

    private static IEnumerable<(string File, XElement El)> All(IEnumerable<string> files) =>
        files.SelectMany(f => XDocument.Load(Path.Combine(AppDir(), f)).Descendants().Select(e => (f, e)));

    /// <summary>key -> list of (file, element type) for keys defined with a plain string x:Key.</summary>
    private static Dictionary<string, List<(string File, string Type)>> Keys()
    {
        var map = new Dictionary<string, List<(string, string)>>();
        foreach (var (f, e) in All(Dictionaries.Concat(Views)))
            if (e.Attribute(X + "Key")?.Value is { } k && !k.StartsWith("{"))
                (map.TryGetValue(k, out var l) ? l : map[k] = new()).Add((f, e.Name.LocalName));
        return map;
    }

    [Fact]
    public void NoKeyIsDefinedWithDifferentTypes()
    {
        var bad = Keys().Where(kv => kv.Value.Select(v => v.Type).Distinct().Count() > 1)
            .Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value.Select(v => $"{v.Type}@{v.File}"))}").ToList();
        Assert.True(bad.Count == 0, "Key collisions:\n" + string.Join("\n", bad));
    }

    [Fact]
    public void ThemePalettesDefineTheSameKeys()
    {
        var d = Keys().Where(k => k.Value.Any(v => v.File.EndsWith("Dark.xaml"))).Select(k => k.Key).ToHashSet();
        var l = Keys().Where(k => k.Value.Any(v => v.File.EndsWith("Light.xaml"))).Select(k => k.Key).ToHashSet();
        Assert.Empty(d.Except(l)); Assert.Empty(l.Except(d));
    }

    [Fact]
    public void EveryResourceReferenceResolvesToACompatibleType()
    {
        var keys = Keys();
        var errors = new List<string>();
        void Check(string file, string prop, string value)
        {
            var m = Ref.Match(value.Trim());
            if (!m.Success) return;
            var key = m.Groups[2].Value;
            if (!keys.TryGetValue(key, out var defs)) { errors.Add($"{file}: {prop} -> '{key}' is not defined"); return; }
            var types = defs.Select(v => v.Type).Distinct().ToList();
            string p = prop.Contains('.') && prop != "TextElement.Foreground" ? prop[(prop.LastIndexOf('.') + 1)..] : prop;
            if (BrushProps.Contains(p) && !types.All(BrushTypes.Contains)) errors.Add($"{file}: {prop} -> '{key}' is {string.Join("/", types)}, expected a Brush");
            if ((p is "Style" or "BasedOn" or "ItemContainerStyle") && !types.All(t => t == "Style")) errors.Add($"{file}: {prop} -> '{key}' is {string.Join("/", types)}, expected Style");
            if (p is "Template" && !types.All(t => t == "ControlTemplate")) errors.Add($"{file}: {prop} -> '{key}' is {string.Join("/", types)}, expected ControlTemplate");
            if (p is "ContentTemplate" or "ItemTemplate" or "CellTemplate" && !types.All(t => t == "DataTemplate")) errors.Add($"{file}: {prop} -> '{key}' is {string.Join("/", types)}, expected DataTemplate");
            if (p is "FontFamily" && !types.All(t => t == "FontFamily")) errors.Add($"{file}: {prop} -> '{key}' is {string.Join("/", types)}, expected FontFamily");
            if (p is "CornerRadius" && !types.All(t => t == "CornerRadius")) errors.Add($"{file}: {prop} -> '{key}' is {string.Join("/", types)}, expected CornerRadius");
        }
        foreach (var (f, e) in All(Dictionaries.Concat(Views)))
        {
            foreach (var a in e.Attributes()) if (!a.IsNamespaceDeclaration) Check(f, a.Name.LocalName, a.Value);
            if (e.Name.LocalName == "Setter" && e.Attribute("Property") is { } pr && e.Attribute("Value") is { } v) Check(f, pr.Value, v.Value);
        }
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void XamlFilesAreWellFormedAndNoDuplicateKeysWithinAFile()
    {
        foreach (var f in Dictionaries.Concat(Views))
        {
            var dups = XDocument.Load(Path.Combine(AppDir(), f)).Descendants()
                .Where(e => e.Parent != null && e.Attribute(X + "Key") != null)
                .GroupBy(e => (e.Parent, e.Attribute(X + "Key")!.Value)).Where(g => g.Count() > 1).Select(g => g.Key.Value).ToList();
            Assert.True(dups.Count == 0, $"{f}: duplicate keys {string.Join(", ", dups)}");
        }
    }
}
