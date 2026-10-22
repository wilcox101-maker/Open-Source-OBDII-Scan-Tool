using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ObdLink.App;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
    { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; PropertyChanged?.Invoke(this, new(n)); return true; }
}

public sealed class AsyncCommand(Func<Task> run, Action<Exception> onError) : ICommand
{
    private bool _busy;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => !_busy;
    public async void Execute(object? p)
    {
        _busy = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await run(); } catch (Exception ex) { onError(ex); }
        finally { _busy = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
