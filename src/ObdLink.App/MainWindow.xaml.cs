using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ObdLink.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel(new WpfConfirmationProvider(this));
        DataContext = vm;
        try { GuideViewer.Document = MarkdownView.Build(vm.GuideText); }
        catch (Exception ex) { GuideViewer.Document = new System.Windows.Documents.FlowDocument(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(vm.GuideText))); vm.EventLog.Message(Serilog.Events.LogEventLevel.Warning, "Guide", "Guide markdown render failed; showing plain text", ex); }
        _vm = vm;
        InputBindings.Add(new KeyBinding(new RelayCommand("Open factory .ab (Ctrl+O)", null, _ => vm.ImportFactoryAbCommand.Execute(null)), Key.O, ModifierKeys.Control));
        Bind(Key.F1, ModifierKeys.None, () => Go("Guide"));
        Bind(Key.F11, ModifierKeys.None, ToggleFullScreen);
        Bind(Key.L, ModifierKeys.Control, () => { LogPanelMenu.IsChecked = !LogPanelMenu.IsChecked; ApplyLogPanel(); });
        Bind(Key.F, ModifierKeys.Control, FindLogs);
        Bind(Key.OemPlus, ModifierKeys.Control, () => SetZoom(_zoom + 0.1)); Bind(Key.Add, ModifierKeys.Control, () => SetZoom(_zoom + 0.1));
        Bind(Key.OemMinus, ModifierKeys.Control, () => SetZoom(_zoom - 0.1)); Bind(Key.Subtract, ModifierKeys.Control, () => SetZoom(_zoom - 0.1));
        Bind(Key.D0, ModifierKeys.Control, () => SetZoom(1));
        Loaded += async (_, _) => { if (vm.UpdateCheckEnabled) { try { await vm.CheckForUpdatesAsync(false); } catch { } } };
    }
    private readonly MainViewModel _vm;
    private double _zoom = 1;
    private void Bind(Key k, ModifierKeys m, Action a) => InputBindings.Add(new KeyBinding(new RelayCommand($"Shortcut {m}+{k}", _vm.EventLog, _ => a()), k, m));
    private void Go(string page) => _vm.SelectedTab = page;
    private void SetZoom(double z) { _zoom = Math.Clamp(Math.Round(z, 1), 0.7, 2.0); Zoom.ScaleX = Zoom.ScaleY = _zoom; _vm.EventLog.UserAction("Zoom", ("zoom", _zoom)); }
    private WindowState _prevState; private WindowStyle _prevStyle; private bool _full;
    private void ToggleFullScreen()
    {
        _full = !_full;
        if (_full) { _prevState = WindowState; _prevStyle = WindowStyle; WindowStyle = WindowStyle.None; WindowState = WindowState.Normal; WindowState = WindowState.Maximized; }
        else { WindowStyle = _prevStyle; WindowState = _prevState; }
        _vm.EventLog.UserAction("Full screen", ("on", _full));
    }
    private void ApplyLogPanel()
    {
        var on = LogPanelMenu.IsChecked;
        LogPanel.Visibility = LogSplitter.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        LogRow.Height = on ? new GridLength(190) : new GridLength(0); SplitRow.Height = on ? new GridLength(6) : new GridLength(0);
        _vm.EventLog.UserAction("Log panel", ("visible", on));
    }
    private void FindLogs() { Go("Logs"); _vm.EventLog.UserAction("Find in logs"); }

    private void Exit_Click(object s, RoutedEventArgs e) => Close();
    private void FindLogs_Click(object s, RoutedEventArgs e) => FindLogs();
    private void Theme_Click(object s, RoutedEventArgs e) => _vm.SelectedTheme = (string)((FrameworkElement)s).Tag;
    private void LogPanel_Click(object s, RoutedEventArgs e) => ApplyLogPanel();
    private void ZoomIn_Click(object s, RoutedEventArgs e) => SetZoom(_zoom + 0.1);
    private void ZoomOut_Click(object s, RoutedEventArgs e) => SetZoom(_zoom - 0.1);
    private void ZoomReset_Click(object s, RoutedEventArgs e) => SetZoom(1);
    private void FullScreen_Click(object s, RoutedEventArgs e) => ToggleFullScreen();
    private void Nav_Click(object s, RoutedEventArgs e) => Go((string)((FrameworkElement)s).Tag);
    private void ConsoleHelp_Click(object s, RoutedEventArgs e) { Go("Console"); _vm.ConsoleInput = "help"; _vm.ConsoleSendCommand.Execute(null); }
    private void GitHub_Click(object s, RoutedEventArgs e)
    {
        _vm.EventLog.UserAction("Open GitHub page");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/" + ObdLink.Core.Diagnostics.UpdateChecker.Repo) { UseShellExecute = true });
    }
    private void About_Click(object s, RoutedEventArgs e) { _vm.EventLog.UserAction("About shown"); MessageBox.Show(this, _vm.AboutText, "About OBDLink Diag", MessageBoxButton.OK, MessageBoxImage.Information); }
}

/// <summary>Attached property: keeps a ListBox/ListView scrolled to the newest item while enabled.</summary>
public static class AutoScroll
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(AutoScroll),
        new PropertyMetadata(false, (d, e) => { if (d is ListBox lb && !_hooked.ContainsKey(lb)) Hook(lb); }));
    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);
    private static readonly Dictionary<ListBox, bool> _hooked = new();

    private static void Hook(ListBox lb)
    {
        _hooked[lb] = true;
        lb.Unloaded += (_, _) => _hooked.Remove(lb);
        if (lb.Items is INotifyCollectionChanged ncc)
            ncc.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add && GetEnabled(lb) && lb.Items.Count > 0)
                    lb.Dispatcher.BeginInvoke(() => lb.ScrollIntoView(lb.Items[^1]), System.Windows.Threading.DispatcherPriority.Background);
            };
    }
}
