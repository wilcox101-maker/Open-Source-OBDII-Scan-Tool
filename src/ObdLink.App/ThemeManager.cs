using System.Windows;
using Microsoft.Win32;

namespace ObdLink.App;

/// <summary>Swaps the palette dictionary (Themes/Light.xaml | Dark.xaml) at runtime; all controls use DynamicResource so no restart is needed.</summary>
public static class ThemeManager
{
    public static readonly string[] Modes = { "System", "Light", "Dark" };
    public static string Mode { get; private set; } = "System";
    public static string Effective { get; private set; } = "Dark";
    private static bool _hooked;

    public static bool WindowsUsesLight()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int i ? i != 0 : true;
        }
        catch { return true; }
    }

    public static string Apply(string mode)
    {
        Mode = Modes.Contains(mode) ? mode : "System";
        Effective = Mode == "System" ? (WindowsUsesLight() ? "Light" : "Dark") : Mode;
        var app = Application.Current;
        if (app == null) return Effective;
        try
        {
            var dicts = app.Resources.MergedDictionaries;
            var pal = new ResourceDictionary { Source = new Uri($"pack://application:,,,/OBDLinkDiag;component/Themes/{Effective}.xaml", UriKind.Absolute) };
            var old = dicts.FirstOrDefault(d => d.Source?.OriginalString.EndsWith("Light.xaml") == true || d.Source?.OriginalString.EndsWith("Dark.xaml") == true);
            if (old != null) dicts[dicts.IndexOf(old)] = pal; else dicts.Insert(0, pal);
        }
        catch (Exception ex)
        {
            // Keep whatever palette is loaded (App.xaml ships Dark as the built-in default).
            Serilog.Log.Warning(ex, "Theme {Theme} failed to load; keeping built-in default palette", Effective);
            Effective = "Dark";
        }
        if (!_hooked)
        {
            _hooked = true;
            try { SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (Mode == "System" && e.Category == UserPreferenceCategory.General) app.Dispatcher.BeginInvoke(() => Apply("System"));
            }; } catch (Exception ex) { Serilog.Log.Warning(ex, "Could not hook Windows theme change notifications"); }
        }
        return Effective;
    }
}
