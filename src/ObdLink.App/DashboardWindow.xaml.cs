using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ObdLink.Core.Dashboard;

namespace ObdLink.App;

public partial class DashboardWindow : Window
{
    public DashboardWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainViewModel Vm => (MainViewModel)DataContext;
    private DashboardViewModel Dash => Vm.Dashboard;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Dash.PropertyChanged += (_, ev) =>
        {
            if (ev.PropertyName is nameof(DashboardViewModel.BackgroundId) or nameof(DashboardViewModel.BackgroundFile) or nameof(DashboardViewModel.BackgroundFullPath))
                ApplyDashBackground();
        };
        ApplyDashBackground();
        ApplyDashFlow();
        Dash.SetWindowOpen(true);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var monitors = MonitorAreas.WorkingAreasInDips(this);
        var target = monitors[0];
        if (Application.Current?.MainWindow is { } main && !ReferenceEquals(main, this))
        {
            var cx = main.Left + main.Width / 2;
            var cy = main.Top + main.Height / 2;
            foreach (var m in monitors)
            {
                if (cx >= m.Left && cx < m.Left + m.Width && cy >= m.Top && cy < m.Top + m.Height)
                {
                    target = m;
                    break;
                }
            }
        }
        var place = DashboardWindowPlacement.Resolve(Dash.SavedPlacement, target, monitors);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = place.Left;
        Top = place.Top;
        Width = place.Width;
        Height = place.Height;
        if (place.Maximize) WindowState = WindowState.Maximized;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!e.Cancel)
        {
            var bounds = WindowState == WindowState.Maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
            Dash.CapturePlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized);
        }
        base.OnClosing(e);
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
        Dash.ApplyViewport(w);
    }
    private void ApplyDashBackground()
    {
        DashCanvas.Background = DashboardBackdrop.For(Dash.BackgroundId, Dash.BackgroundFullPath);
    }

    // ---- dashboard customize: drag & drop reorder ----
    // Root cause (widgets never moved on Windows): OleDropTarget.GetCurrentTarget only accepts the
    // element under the pointer, and only when THAT element has AllowDrop. It does not walk up to a
    // parent. AllowDrop was on the card border, but the gauge covered it, so a drop on a widget set
    // the target to null and MoveItem never ran. DropOverlay covers the card and is hit-testable only
    // while a drag is in progress. The payload is the item key (a string OLE understands) plus _dragKey;
    // the card view-model is not a drag format.
    private Point _dragStart; private DashItemVm? _dragItem; private string? _dragKey; private string? _dropSrc; private string? _dropDst;
    internal void DashItem_MouseDown(object sender, MouseButtonEventArgs e)
    { _dragStart = e.GetPosition(this); _dragItem = (sender as FrameworkElement)?.DataContext as DashItemVm; }
    internal void DashItem_MouseMove(object sender, MouseEventArgs e)
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
        try
        {
            if (!data.GetDataPresent(DataFormats.UnicodeText) || data.GetData(DataFormats.UnicodeText) is not string s) return null;
            if (s.Length is > 0 and < 80 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) return s;
        }
        catch (Exception ex) { Vm.EventLog.Message(Serilog.Events.LogEventLevel.Debug, "Dashboard", "Drag data was not a card key", ex); }
        return null;
    }
    internal void DashItem_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm || !vm.Dashboard.Customizing) return;
        if ((sender as FrameworkElement)?.DataContext is not DashItemVm dst) return;
        var src = DragSourceKey(e.Data);
        if (src is null || src == dst.Key) return;
        _dropSrc = src; _dropDst = dst.Key;
        e.Handled = true;
    }
    internal void DashItem_DragOver(object sender, DragEventArgs e)
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
    private void SetDashDropTargets(bool on)
    {
        for (int i = 0; i < DashItemsHost.Items.Count; i++)
        {
            if (DashItemsHost.ItemContainerGenerator.ContainerFromIndex(i) is DependencyObject c && FindNamed(c, "DropOverlay") is UIElement overlay)
                overlay.IsHitTestVisible = on;
        }
    }
    private int DashRowSize(FrameworkElement card)
    {
        var w = card.ActualWidth + card.Margin.Left + card.Margin.Right;
        return w <= 1 || DashItemsHost.ActualWidth <= 1 ? 1 : Math.Max(1, (int)(DashItemsHost.ActualWidth / w));
    }
    private void DashMove(FrameworkElement src, int delta)
    {
        if (DataContext is not MainViewModel vm || !vm.Dashboard.Customizing || src.DataContext is not DashItemVm it) return;
        if (!vm.Dashboard.MoveBy(it, delta)) return;
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
    internal void DashItemRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement b) return;
        int dir = b.Tag as string == "Up" ? -1 : 1;
        int delta = VerticalDelta(b, dir);
        if (delta != 0) DashMove(b, delta);
    }
    internal void DashItem_KeyDown(object sender, KeyEventArgs e)
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
    /// <summary>Ctrl+Arrow mapping: Left/Right = 1 place, Up/Down = one row, Home/End = to the ends.</summary>
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
