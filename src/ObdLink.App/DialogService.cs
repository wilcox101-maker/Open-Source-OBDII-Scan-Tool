using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ObdLink.App;

/// <summary>
/// v0.3.0: the ONLY place that shows message boxes, file dialogs and prompts (a static test enforces this).
/// Every dialog is owned by the main window and runs on the UI thread; afterwards the main window is re-activated,
/// so Windows never drops the app behind other windows (the v0.2.x "lost focus" bug: ownerless MessageBox.Show,
/// ShowDialog() without owner, and the VB InputBox all let Windows hand focus to another app when they closed).
/// </summary>
public static class DialogService
{
    /// <summary>The main window, once it is shown; null during early startup (dialogs are then simply ownerless).</summary>
    public static Window? Owner
    {
        get
        {
            var w = Application.Current?.MainWindow;
            return w is { IsLoaded: true, IsVisible: true } ? w : Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? (w is { IsLoaded: true } ? w : null);
        }
    }

    private static T OnUi<T>(Func<T> f)
    {
        var d = Application.Current?.Dispatcher;
        return d is null || d.CheckAccess() ? f() : d.Invoke(f, DispatcherPriority.Normal);
    }

    /// <summary>Brings the main window back to the front after a modal closes or an external app was started.</summary>
    public static void ReActivate()
    {
        var w = Application.Current?.MainWindow;
        if (w is null || !w.IsLoaded) return;
        w.Dispatcher.BeginInvoke(() => { if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal; w.Activate(); }, DispatcherPriority.ApplicationIdle);
    }

    public static MessageBoxResult Show(string text, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None) => OnUi(() =>
    {
        var o = Owner;
        var r = o is null ? MessageBox.Show(text, title, buttons, icon) : MessageBox.Show(o, text, title, buttons, icon);
        ReActivate();
        return r;
    });
    public static void Info(string text, string title) => Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);
    public static void Error(string text, string title) => Show(text, title, MessageBoxButton.OK, MessageBoxImage.Error);
    public static bool YesNo(string text, string title, MessageBoxImage icon = MessageBoxImage.Question) => Show(text, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;

    /// <summary>Open/Save file dialogs, always owned by the main window.</summary>
    public static bool? ShowFileDialog(FileDialog dlg) => OnUi(() =>
    {
        var o = Owner;
        var r = o is null ? dlg.ShowDialog() : dlg.ShowDialog(o);
        ReActivate();
        return r;
    });

    /// <summary>Owned text prompt (replaces Microsoft.VisualBasic.Interaction.InputBox, which has no owner). Null when canceled.</summary>
    public static string? Prompt(string message, string title, string initial = "") => OnUi(() =>
    {
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 8, 0, 8), MinWidth = 320 };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } };
        var panel = new StackPanel { Margin = new Thickness(14), Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 }, box, buttons } };
        var w = new Window { Title = title, Content = panel, SizeToContent = SizeToContent.WidthAndHeight, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var o = Owner; if (o is not null) w.Owner = o; else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (Application.Current?.TryFindResource("Bg") is System.Windows.Media.Brush bg) w.Background = bg;
        ok.Click += (_, _) => w.DialogResult = true;
        w.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        var r = w.ShowDialog();
        ReActivate();
        return r == true ? box.Text : null;
    });

    /// <summary>Opens a URL, file or folder in its default app. Focus moving to Explorer/the browser is expected here; nothing else is touched.</summary>
    public static void OpenExternal(string target, string? args = null)
    {
        // v0.3.4 audit: only https links to known hosts, explorer.exe, or absolute local paths
        if (!ObdLink.Core.Security.ExternalTargets.IsAllowed(target)) throw new InvalidOperationException($"Refused to open \"{target}\": not an allowed link or local path.");
        var psi = args is null ? new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true } : new System.Diagnostics.ProcessStartInfo(target, args) { UseShellExecute = true };
        System.Diagnostics.Process.Start(psi);
    }

    /// <summary>v0.3.1: starts OBDLink's official firmware updater via ShellExecute so Windows shows its own UAC prompt. Returns false if the user canceled UAC.</summary>
    public static bool RunUpdater(string exe)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(exe) ?? "" });
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { return false; } // ERROR_CANCELLED (UAC "No")
    }
}
