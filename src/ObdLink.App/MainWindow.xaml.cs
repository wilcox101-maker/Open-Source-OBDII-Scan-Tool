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
        var vm = new MainViewModel(new WpfConfirmationProvider());
        DataContext = vm;
        try { GuideViewer.Document = MarkdownView.Build(vm.GuideText); }
        catch (Exception ex) { GuideViewer.Document = new System.Windows.Documents.FlowDocument(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(vm.GuideText))); vm.EventLog.Message(Serilog.Events.LogEventLevel.Warning, "Guide", "Guide markdown render failed; showing plain text", ex); }
        _vm = vm;
        PreviewKeyDown += OnShortcutKey;
        vm.Dashboard.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DashboardViewModel.BackgroundId) or nameof(DashboardViewModel.BackgroundFile) or nameof(DashboardViewModel.BackgroundFullPath))
                ApplyDashBackground();
        };
        Loaded += async (_, _) =>
        {
            ApplyDashBackground();
            ApplyDashFlow();
            if (vm.UpdateCheckEnabled) { try { await vm.CheckForUpdatesAsync(false); } catch (Exception ex) { Serilog.Log.Warning("Startup update check failed: {Err}", ex.Message); } }
        };
    }
    private void DashCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyDashFlow();
    private void DashCanvas_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ViewportWidthChange != 0) ApplyDashFlow();
    }
    private void ApplyDashFlow()
    {
        var w = DashCanvas.ViewportWidth;
        if (double.IsNaN(w) || w < 1) w = DashCanvas.ActualWidth;
        _vm.Dashboard.ApplyViewport(w);
    }
    private void ApplyDashBackground()
    {
        DashCanvas.Background = DashboardBackdrop.For(_vm.Dashboard.BackgroundId, _vm.Dashboard.BackgroundFullPath);
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
        if (msg == WM_DEVICECHANGE && wParam.ToInt64() is DBT_DEVICEARRIVAL or DBT_DEVICEREMOVECOMPLETE or DBT_DEVNODES_CHANGED) RequestRescan();
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
    private void SetZoom(double z) { _zoom = Math.Clamp(Math.Round(z, 1), 0.7, 2.0); Zoom.ScaleX = Zoom.ScaleY = _zoom; ApplyDashFlow(); _vm.EventLog.UserAction("Zoom", ("zoom", _zoom)); }
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
    // Root cause (widgets never moved on Windows): OleDropTarget.GetCurrentTarget only accepts the
    // element under the pointer, and only when THAT element has AllowDrop. It does not walk up to a
    // parent. AllowDrop was on the card border, but the gauge covered it, so a drop on a widget set
    // the target to null and MoveItem never ran. DropOverlay covers the card and is hit-testable only
    // while a drag is in progress. The payload is the item key (a string OLE understands) plus _dragKey;
    // the card view-model is not a drag format.
    private Point _dragStart; private DashItemVm? _dragItem; private string? _dragKey; private string? _dropSrc; private string? _dropDst;
    private void DashItem_MouseDown(object sender, MouseButtonEventArgs e)
    { _dragStart = e.GetPosition(this); _dragItem = (sender as FrameworkElement)?.DataContext as DashItemVm; }
    private void DashItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null || DataContext is not MainViewModel vm || !vm.Dashboard.Customizing) return;
        if (e.OriginalSource is DependencyObject d && IsDashEditor(d)) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var item = _dragItem; _dragItem = null;
        _dragKey = item.Key;
        _dropSrc = _dropDst = null;
        SetDashDropTargets(true);
        try
        {
            var data = new DataObject(DataFormats.UnicodeText, item.Key);
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
        }
        finally
        {
            // Apply the reorder after DoDragDrop returns. Moving the items collection inside the
            // drop handler fights the panel that is still tracking the drag.
            var src = _dropSrc; var dst = _dropDst;
            _dropSrc = _dropDst = null;
            _dragKey = null;
            SetDashDropTargets(false);
            if (src is not null && dst is not null && DataContext is MainViewModel owner) owner.Dashboard.MoveItemByKey(src, dst);
        }
    }
    private string? DragSourceKey(IDataObject data)
    {
        if (!string.IsNullOrEmpty(_dragKey)) return _dragKey;
        // Same-window drags keep the key in _dragKey. A foreign Unicode drop must look like a card key (letters, digits, underscore).
        try
        {
            if (!data.GetDataPresent(DataFormats.UnicodeText) || data.GetData(DataFormats.UnicodeText) is not string s) return null;
            if (s.Length is > 0 and < 80 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) return s;
        }
        catch (Exception ex) { _vm.EventLog.Message(Serilog.Events.LogEventLevel.Debug, "Dashboard", "Drag data was not a card key", ex); }
        return null;
    }
    private void DashItem_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.Dashboard.Customizing) return;
        if ((sender as FrameworkElement)?.DataContext is not DashItemVm dst) return;
        var src = DragSourceKey(e.Data);
        if (src is null || src == dst.Key) return;
        _dropSrc = src; _dropDst = dst.Key;
        e.Handled = true;
    }
    private void DashItem_DragOver(object sender, DragEventArgs e)
    {
        var ok = DataContext is MainViewModel vm && vm.Dashboard.Customizing && DragSourceKey(e.Data) is not null;
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }
    private void DashHost_DragOver(object sender, DragEventArgs e)
    {
        var ok = DataContext is MainViewModel vm && vm.Dashboard.Customizing && DragSourceKey(e.Data) is not null;
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }
    private void DashHost_Drop(object sender, DragEventArgs e)
    {
        if (e.Handled || DataContext is not MainViewModel vm || !vm.Dashboard.Customizing) return;
        var src = DragSourceKey(e.Data);
        if (src is null) return;
        var slots = SlotCenters();
        var p = e.GetPosition(DashItemsHost);
        int dest = ObdLink.Core.Dashboard.DashReorder.NearestIndex(slots, p.X, p.Y);
        if (dest < 0 || dest >= vm.Dashboard.Items.Count) return;
        _dropSrc = src; _dropDst = vm.Dashboard.Items[dest].Key;
        e.Handled = true;
    }
    /// <summary>Turns the full-card drop overlay on for every realized card. Off again when the drag ends.</summary>
    private void SetDashDropTargets(bool on)
    {
        for (int i = 0; i < DashItemsHost.Items.Count; i++)
        {
            if (DashItemsHost.ItemContainerGenerator.ContainerFromIndex(i) is DependencyObject c && FindNamed(c, "DropOverlay") is UIElement overlay)
                overlay.IsHitTestVisible = on;
        }
    }
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
        if (sender is not FrameworkElement b) return;
        int dir = b.Tag as string == "Up" ? -1 : 1;
        int delta = VerticalDelta(b, dir);
        if (delta != 0) DashMove(b, delta);
    }
    private void DashItem_KeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || sender is not FrameworkElement card) return;
        if (e.OriginalSource is DependencyObject src && (src is ComboBox || FindParent<ComboBox>(src) is not null || src is TextBoxBase)) return;
        var n = (DataContext as MainViewModel)?.Dashboard.Items.Count ?? 0;
        int? d = e.Key switch
        {
            Key.Up => VerticalDelta(card, -1),
            Key.Down => VerticalDelta(card, +1),
            _ => DashKeyDelta(e.Key, DashRowSize(card), n),
        };
        if (d is null || d == 0) return;
        DashMove(card, d.Value); e.Handled = true;
    }
    private int VerticalDelta(FrameworkElement src, int direction)
    {
        if (DataContext is not MainViewModel vm || src.DataContext is not DashItemVm it) return direction;
        return ObdLink.Core.Dashboard.DashReorder.VerticalDelta(SlotCenters(), vm.Dashboard.Items.IndexOf(it), direction, DashRowSize(FindCard(src) ?? src));
    }
    private List<ObdLink.Core.Dashboard.DashReorder.Slot> SlotCenters()
    {
        var list = new List<ObdLink.Core.Dashboard.DashReorder.Slot>();
        if (DataContext is not MainViewModel vm) return list;
        foreach (var item in vm.Dashboard.Items)
        {
            if (DashItemsHost.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement c && c.ActualWidth > 1 && c.ActualHeight > 1)
            {
                var p = c.TranslatePoint(new Point(c.ActualWidth / 2, c.ActualHeight / 2), DashItemsHost);
                list.Add(new(p.X, p.Y));
            }
            else list.Add(new(0, 0));
        }
        return list;
    }
    private static bool IsDashEditor(DependencyObject d) => FindParent<ButtonBase>(d) is not null || FindParent<ComboBox>(d) is not null || FindParent<TextBoxBase>(d) is not null;
    private static FrameworkElement? FindNamed(DependencyObject d, string name)
    {
        if (d is FrameworkElement fe && (fe.Name == name || fe.Tag as string == name)) return fe;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++)
            if (FindNamed(System.Windows.Media.VisualTreeHelper.GetChild(d, i), name) is { } found) return found;
        return null;
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
    private static readonly Dictionary<ListBox, bool> _hooked = new();
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(AutoScroll),
        new PropertyMetadata(false, (d, e) => { if (d is ListBox lb && !_hooked.ContainsKey(lb)) Hook(lb); }));
    public static bool GetEnabled(DependencyObject d) => d.GetValue(EnabledProperty) is true;
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);

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
