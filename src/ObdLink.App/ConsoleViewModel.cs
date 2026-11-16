using ObdLink.Core.Logging;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>
/// Raw console page: one command line, the transcript, and the "Allow writes" toggle.
/// Split out of <see cref="MainViewModel"/>. The <see cref="RawConsoleService"/> itself is created per connection by the main
/// view model (it also applies read-only test mode to it); this page reaches it through <c>service</c>. Every line is checked by
/// the service's guard (ConsoleGuard): with writes off, state-changing services are refused before anything is sent, and
/// pyrotechnic routines are refused always. Send goes through the main command factory (shared bus lock, refused while the bus
/// monitor runs).
/// </summary>
public sealed class ConsoleViewModel : ObservableObject
{
    /// <summary>Builds an adapter command: (name, body, log parameters).</summary>
    public delegate AsyncCommand CommandFactory(string name, Func<Task> body, Func<(string, object?)[]>? parameters = null);

    private readonly IEventLog _log;
    private readonly Func<RawConsoleService?> _service;

    private string _ci = ""; public string ConsoleInput { get => _ci; set => Set(ref _ci, value); }
    private string _co = ""; public string ConsoleOutput { get => _co; set => Set(ref _co, value); }
    private bool _allow;
    public bool ConsoleAllowWrites
    {
        get => _allow;
        set { if (Set(ref _allow, value)) { _log.UserAction("Console 'Allow writes' toggled", ("on", value)); if (_service() is { } c) c.AllowWrites = value; } }
    }

    public AsyncCommand ConsoleSendCommand { get; }

    public ConsoleViewModel(IEventLog log, CommandFactory cmd, Func<RawConsoleService?> service)
    {
        _log = log; _service = service;
        ConsoleSendCommand = cmd("Console send", SendAsync, () => new (string, object?)[] { ("command", ConsoleInput) });
    }

    private async Task SendAsync()
    {
        var input = ConsoleInput; ConsoleInput = "";
        var t = input.Trim().ToUpperInvariant();
        if (t is "CLS" or "CLEAR") { ConsoleOutput = ""; return; }
        var console = _service();
        if (t is "HELP" or "?" or "/?" or "/HELP" && console is null) { ConsoleOutput += $"> {input}{Environment.NewLine}{ObdLink.Core.ConsoleCmds.ConsoleHelp.Text()}{Environment.NewLine}"; return; }
        if (console is null) throw new InvalidOperationException("Not connected (type help for the command list)");
        var result = await console.ExecuteAsync(input);
        ConsoleOutput += $"> {input}{Environment.NewLine}{result}{Environment.NewLine}";
        if (result.StartsWith("BLOCKED")) _log.Message(LogEventLevel.Warning, "Console", $"{input}: {result}");
    }
}
