using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ObdLink.App;

/// <summary>
/// Dashboard card templates live in an application dictionary so the pop-out can use them.
/// Event attributes on those templates bind here, then forward to the open <see cref="DashboardWindow"/>.
/// </summary>
public partial class DashboardTemplates : ResourceDictionary
{
    public DashboardTemplates() => InitializeComponent();

    private static DashboardWindow? Win(object sender) =>
        Window.GetWindow(sender as DependencyObject) as DashboardWindow;

    private void DashItem_MouseDown(object sender, MouseButtonEventArgs e) => Win(sender)?.DashItem_MouseDown(sender, e);
    private void DashItem_MouseMove(object sender, MouseEventArgs e) => Win(sender)?.DashItem_MouseMove(sender, e);
    private void DashItem_Drop(object sender, DragEventArgs e) => Win(sender)?.DashItem_Drop(sender, e);
    private void DashItem_DragOver(object sender, DragEventArgs e) => Win(sender)?.DashItem_DragOver(sender, e);
    private void DashItem_KeyDown(object sender, KeyEventArgs e) => Win(sender)?.DashItem_KeyDown(sender, e);
    private void DashItemRow_Click(object sender, RoutedEventArgs e) => Win(sender)?.DashItemRow_Click(sender, e);
}
