using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace ObdLink.App;

public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); DataContext = new MainViewModel(new WpfConfirmationProvider(this)); }
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
