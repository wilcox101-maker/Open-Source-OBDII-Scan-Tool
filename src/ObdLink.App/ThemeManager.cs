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
        var dicts = app.Resources.MergedDictionaries;
        var pal = new ResourceDictionary { Source = new Uri($"pack://application:,,,/OBDLinkDiag;component/Themes/{Effective}.xaml") };
        var old = dicts.FirstOrDefault(d => d.Source?.OriginalString.EndsWith("Light.xaml") == true || d.Source?.OriginalString.EndsWith("Dark.xaml") == true);
        if (old != null) dicts[dicts.IndexOf(old)] = pal; else dicts.Insert(0, pal);
        if (!_hooked)
        {
            _hooked = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (Mode == "System" && e.Category == UserPreferenceCategory.General) app.Dispatcher.BeginInvoke(() => Apply("System"));
            };
        }
        return Effective;
    }
}
