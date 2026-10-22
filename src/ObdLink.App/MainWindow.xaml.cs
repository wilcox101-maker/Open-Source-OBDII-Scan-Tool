namespace ObdLink.App;
public partial class MainWindow : System.Windows.Window
{
    public MainWindow() { InitializeComponent(); DataContext = new MainViewModel(new WpfConfirmationProvider(this)); }
}
