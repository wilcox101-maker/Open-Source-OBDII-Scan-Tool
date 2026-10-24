using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        PreviewKeyDown += OnShortcutKey;
        Loaded += async (_, _) => { if (vm.UpdateCheckEnabled) { try { await vm.CheckForUpdatesAsync(false); } catch { } } };
    }
    private readonly MainViewModel _vm;
    // ---- v0.3.2: rescan COM ports when a USB serial device (e.g. OBDLink EX) is plugged in or removed ----
    private System.Windows.Threading.DispatcherTimer? _rescan;
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        (System.Windows.PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource)?.AddHook(WndProc);
        // second scan shortly after startup: FTDI COM ports can register a moment after the app starts
        _ = Dispatcher.InvokeAsync(async () => { await Task.Delay(1500); RequestRescan(); });
    }
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_DEVICECHANGE = 0x0219, DBT_DEVNODES_CHANGED = 0x0007, DBT_DEVICEARRIVAL = 0x8000, DBT_DEVICEREMOVECOMPLETE = 0x8004;
        if (msg == WM_DEVICECHANGE && (int)wParam is DBT_DEVICEARRIVAL or DBT_DEVICEREMOVECOMPLETE or DBT_DEVNODES_CHANGED) RequestRescan();
        return IntPtr.Zero;
    }
    private void RequestRescan()
    {
        if (_vm.IsConnected) return; // never disturb a live session
        _rescan ??= new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(800), System.Windows.Threading.DispatcherPriority.Background, (_, _) => { _rescan!.Stop(); if (!_vm.IsConnected) _vm.RefreshPortsCommand.Execute(null); }, Dispatcher);
        _rescan.Stop(); _rescan.Start();
    }
    private double _zoom = 1;
    // ---- v0.3.0 keyboard shortcuts (table in ObdLink.Core.Ui.Shortcuts) ----
    private static string? KeyName(Key k) => k switch
    {
        >= Key.F1 and <= Key.F12 => k.ToString(),
        >= Key.A and <= Key.Z => k.ToString(),
        >= Key.D0 and <= Key.D9 => ((int)(k - Key.D0)).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => ((int)(k - Key.NumPad0)).ToString(),
        Key.Delete => "Delete", Key.Tab => "Tab", Key.Oem3 => "`", Key.OemComma => ",",
        Key.OemPlus or Key.Add => "+", Key.OemMinus or Key.Subtract => "-",
        _ => null,
    };
    private void OnShortcutKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var name = KeyName(key); if (name is null) return;
        var m = Keyboard.Modifiers;
        var inText = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or PasswordBox or ComboBox { IsEditable: true };
        // plain letters/digits while typing belong to the text box
        if (m == ModifierKeys.None && name.Length == 1) return;
        var id = ObdLink.Core.Ui.Shortcuts.Resolve(ObdLink.Core.Ui.Shortcuts.Format(name, m.HasFlag(ModifierKeys.Control), m.HasFlag(ModifierKeys.Shift), m.HasFlag(ModifierKeys.Alt)), inText);
        if (id is null) return;
        e.Handled = true;
        _vm.EventLog.UserAction("Shortcut", ("id", id));
        RunShortcut(id);
    }
    private static void Run(ICommand c) { if (c.CanExecute(null)) c.Execute(null); }
    public void RunShortcut(string id)
    {
        var d = _vm.Dashboard;
        switch (id)
        {
            case var p when p.StartsWith("page:"): Go(ObdLink.Core.Ui.Shortcuts.Pages[p[5] - '1']); break;
            case "connect": Run(_vm.ConnectCommand); break;
            case "disconnect": Run(_vm.DisconnectCommand); break;
            case "identify": Run(_vm.IdentifyVehicleCommand); break;
            case "scan": Run(_vm.ScanCommand); break;
            case "dtcs": Go("DTCs"); Run(_vm.ReadObdDtcsCommand); break;
            case "dtcsAll": Go("DTCs"); Run(_vm.ReadAllModuleDtcsCommand); break;
            case "clearDtcs": Run(_vm.ClearModuleDtcsCommand); break;   // confirmation + read-only gate inside the command
            case "readAb": Go("As-Built"); Run(_vm.ReadAsBuiltCommand); break;
            case "readAllAb": Go("As-Built"); Run(_vm.ReadAllModulesAsBuiltCommand); break;
            case "backup": Run(_vm.BackupAsBuiltCommand); break;
            case "write": Run(_vm.WriteAsBuiltCommand); break;           // confirmation + read-only gate inside the command
            case "open": Run(_vm.ImportFactoryAbCommand); break;
            case "export": Run(_vm.ExportFactoryAbCommand); break;
            case "capture": Run(_vm.DiagnosticsCaptureCommand); break;
            case "exportLogs": Run(_vm.EventsPanel.ExportCommand); break;
            case "find": FindLogs(); break;
            case "logPanel": LogPanelMenu.IsChecked = !LogPanelMenu.IsChecked; ApplyLogPanel(); break;
            case "console": Go("Console"); break;
            case "settings": Go("Settings"); break;
            case "nextPage" or "prevPage": StepPage(id == "nextPage" ? 1 : -1); break;
            case "pause": Run(d.PauseCommand); break;
            case "record": Run(d.RecordCommand); break;
            case "firmware": Run(_vm.CheckFirmwareCommand); break;
            case "guide": Go("Guide"); break;
            case "shortcuts": ShowShortcuts(); break;
            case "fullscreen": ToggleFullScreen(); break;
            case "zoomIn": SetZoom(_zoom + 0.1); break;
            case "zoomOut": SetZoom(_zoom - 0.1); break;
            case "zoomReset": SetZoom(1); break;
        }
    }
    private void StepPage(int dir)
    {
        var pages = ObdLink.Core.Ui.Shortcuts.Pages;
        var i = Array.IndexOf(pages, _vm.SelectedTab); if (i < 0) i = 0;
        Go(pages[(i + dir + pages.Length) % pages.Length]);
    }
    private void ShowShortcuts() => DialogService.Info(ObdLink.Core.Ui.Shortcuts.Text() + "\n\nShortcuts marked as asking first still show their confirmation; Clear DTCs and Write are blocked in read-only mode.\nWhile typing in a text box or the console, Ctrl+I/B/W and Ctrl+Shift+Delete are left to the text box.", "Keyboard shortcuts");
    private void ShowAbout() => DialogService.Info(_vm.AboutText, "About OBDLink Diag");
    private void Shortcuts_Click(object s, RoutedEventArgs e) => ShowShortcuts();
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
        DialogService.OpenExternal("https://github.com/" + ObdLink.Core.Diagnostics.UpdateChecker.Repo);
    }
    private void About_Click(object s, RoutedEventArgs e) { _vm.EventLog.UserAction("About shown"); ShowAbout(); }
    // ---- dashboard customize: drag & drop reorder ----
    private Point _dragStart; private DashItemVm? _dragItem;
    private void DashItem_MouseDown(object sender, MouseButtonEventArgs e)
    { _dragStart = e.GetPosition(this); _dragItem = (sender as FrameworkElement)?.DataContext as DashItemVm; }
    private void DashItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null || DataContext is not MainViewModel vm || !vm.Dashboard.Customizing) return;
        if (e.OriginalSource is DependencyObject d && FindParent<ButtonBase>(d) is not null) return;
        if (e.OriginalSource is DependencyObject d2 && FindParent<ComboBox>(d2) is not null) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _dragItem; _dragItem = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(DashItemVm), item), DragDropEffects.Move);
    }
    private void DashItem_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is MainViewModel vm && e.Data.GetData(typeof(DashItemVm)) is DashItemVm src && (sender as FrameworkElement)?.DataContext is DashItemVm dst && src != dst)
            vm.Dashboard.MoveItem(src, dst);
    }
    private void DashItem_DragOver(object sender, DragEventArgs e)
    { e.Effects = e.Data.GetDataPresent(typeof(DashItemVm)) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; }
    /// <summary>v0.3.3: cards per row in the WrapPanel, so Up/Down moves one row.</summary>
    private int DashRowSize(FrameworkElement card)
    {
        var w = card.ActualWidth + card.Margin.Left + card.Margin.Right;
        return w <= 1 || DashItemsHost.ActualWidth <= 1 ? 1 : Math.Max(1, (int)(DashItemsHost.ActualWidth / w));
    }
    private void DashMove(FrameworkElement src, int delta)
    {
        if (DataContext is not MainViewModel vm || !vm.Dashboard.Customizing || src.DataContext is not DashItemVm it) return;
        if (!vm.Dashboard.MoveBy(it, delta)) return;
        // keep keyboard focus on the moved card so Ctrl+Arrow can be repeated
        Dispatcher.BeginInvoke(() =>
        {
            if (DashItemsHost.ItemContainerGenerator.ContainerFromItem(it) is FrameworkElement c && FindChild<ButtonBase>(c) is { } b) b.Focus();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
    private static FrameworkElement? FindCard(DependencyObject d)
    {
        for (var x = d; x != null; x = System.Windows.Media.VisualTreeHelper.GetParent(x)) if (x is Border { Name: "Card" } c) return c;
        return null;
    }
    private void DashItemRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement b) DashMove(b, (b.Tag as string == "Up" ? -1 : 1) * DashRowSize(FindCard(b) ?? b));
    }
    private void DashItem_KeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || sender is not FrameworkElement card || e.OriginalSource is ComboBox) return;
        var n = (DataContext as MainViewModel)?.Dashboard.Items.Count ?? 0;
        int? d = DashKeyDelta(e.Key, DashRowSize(card), n);
        if (d is null) return;
        DashMove(card, d.Value); e.Handled = true;
    }
    /// <summary>Ctrl+Arrow mapping (public for tests): Left/Right = 1 place, Up/Down = one row, Home/End = to the ends.</summary>
    public static int? DashKeyDelta(Key k, int row, int count) => k switch
    {
        Key.Left => -1, Key.Right => 1, Key.Up => -row, Key.Down => row, Key.Home => -count, Key.End => count, _ => null
    };
    private static T? FindChild<T>(DependencyObject d) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++)
        { var c = System.Windows.Media.VisualTreeHelper.GetChild(d, i); if (c is T t) return t; if (FindChild<T>(c) is { } r) return r; }
        return null;
    }
    private static T? FindParent<T>(DependencyObject d) where T : DependencyObject
    {
        for (var x = d; x != null; x = System.Windows.Media.VisualTreeHelper.GetParent(x) ?? LogicalTreeHelper.GetParent(x)) if (x is T t) return t;
        return null;
    }
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
