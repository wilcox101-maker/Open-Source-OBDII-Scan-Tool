using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ObdLink.Core.Logging;
using Serilog.Events;
using ObdLink.Core.Safety;

namespace ObdLink.App;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
    { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; PropertyChanged?.Invoke(this, new(n)); return true; }
    protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new(n));
}

/// <summary>Thrown when the user cancels a file dialog or similar; logged as Cancelled, never shown as an error.</summary>
public sealed class UserCancelledException(string what) : OperationCanceledException(what);

/// <summary>
/// Async command that records itself in the event log: "Started", then "Succeeded", "Cancelled" or "Failed" (with a meaningful error).
/// Every button in the UI goes through this, so user interaction is logged centrally rather than ad hoc.
/// </summary>
public sealed class AsyncCommand : ICommand
{
    private readonly string _name; private readonly IEventLog _log; private readonly Func<Task> _run;
    private readonly Action<string, Exception> _onError; private readonly Func<(string, object?)[]>? _params;
    private bool _busy;
    public AsyncCommand(string name, IEventLog log, Func<Task> run, Action<string, Exception> onError, Func<(string, object?)[]>? parameters = null)
    { _name = name; _log = log; _run = run; _onError = onError; _params = parameters; }
    public string Name => _name;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => !_busy;
    public async void Execute(object? p)
    {
        // v0.3.4 audit: async void must never throw (an exception here would crash the dispatcher)
        try { await ExecuteAsync(); } catch (Exception ex) { try { _log.Message(LogEventLevel.Error, _name, "Unhandled command error", ex); } catch { /* logging itself failed; never crash from a command */ } }
    }
    public async Task ExecuteAsync()
    {
        _busy = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        (string, object?)[] ps;
        try { ps = _params?.Invoke() ?? Array.Empty<(string, object?)>(); } catch { ps = Array.Empty<(string, object?)>(); } // log parameters are optional context
        var op = _log.Begin(_name, ps);
        try { await _run(); op.Succeeded(); }
        catch (UserDeclinedException ex) { op.Cancelled(ex.Message); }
        catch (OperationCanceledException ex) { op.Cancelled(ex.Message); }
        catch (Exception ex) { op.Failed(ex); _onError(_name, ex); }
        finally { _busy = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}

/// <summary>Synchronous command (log panel buttons etc.) with the same event logging.</summary>
public sealed class RelayCommand(string name, IEventLog? log, Action<object?> run) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p)
    {
        var op = log?.Begin(name);
        try { run(p); op?.Succeeded(); }
        catch (OperationCanceledException ex) { op?.Cancelled(ex.Message); }
        catch (Exception ex) { op?.Failed(ex); DialogService.Error(ErrorText.ForOperation(name, ex), "Error"); }
    }
}
