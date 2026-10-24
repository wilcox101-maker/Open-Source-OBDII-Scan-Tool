namespace ObdLink.Core.Ui;

/// <summary>One keyboard shortcut. <see cref="InText"/> = still fires while a TextBox/console has focus (only for keys a text box never uses).</summary>
public sealed record Shortcut(string Id, string Gesture, string Description, string Group, bool InText = true, bool Guarded = false);

/// <summary>v0.3.0: single source for every keyboard shortcut: the window key handler, menu InputGestureText, tooltips, Help list and Guide all read this.</summary>
public static class Shortcuts
{
    public static readonly IReadOnlyList<Shortcut> All = new Shortcut[]
    {
        new("connect", "F5", "Connect", "Vehicle"),
        new("disconnect", "Shift+F5", "Disconnect", "Vehicle"),
        new("identify", "Ctrl+I", "Identify vehicle (VIN, modules, profile)", "Vehicle", InText: false),
        new("scan", "Ctrl+M", "Scan modules", "Vehicle"),
        new("dtcs", "Ctrl+D", "Read OBD-II DTCs", "DTCs"),
        new("dtcsAll", "Ctrl+Shift+D", "Read DTCs from all modules", "DTCs"),
        new("clearDtcs", "Ctrl+Shift+Delete", "Clear DTCs of the selected module (asks first; blocked in read-only mode)", "DTCs", InText: false, Guarded: true),
        new("readAb", "Ctrl+R", "Read As-Built of the selected module", "As-Built"),
        new("readAllAb", "Ctrl+Shift+R", "Read all modules' As-Built (backup)", "As-Built"),
        new("backup", "Ctrl+B", "Backup As-Built", "As-Built", InText: false),
        new("write", "Ctrl+W", "Write + verify As-Built (asks first; blocked in read-only mode)", "As-Built", InText: false, Guarded: true),
        new("open", "Ctrl+O", "Open factory .ab", "File"),
        new("export", "Ctrl+S", "Export factory .ab", "File"),
        new("capture", "Ctrl+Alt+C", "Save diagnostics capture (zip; was Ctrl+Shift+D)", "File"),
        new("exportLogs", "Ctrl+E", "Export events log", "File"),
        new("find", "Ctrl+F", "Find in logs", "View"),
        new("logPanel", "Ctrl+L", "Show / hide log panel", "View"),
        new("console", "Ctrl+`", "Raw console", "Pages"),
        new("settings", "Ctrl+,", "Settings", "Pages"),
        new("page", "Alt+1..9", "Go to page 1-9 (Dashboard, Modules, DTCs, As-Built, Vehicle, Console, Logs, Guide, Settings)", "Pages"),
        new("nextPage", "Ctrl+Tab", "Next page", "Pages"),
        new("prevPage", "Ctrl+Shift+Tab", "Previous page", "Pages"),
        new("pause", "Ctrl+P", "Pause / resume dashboard", "Dashboard"),
        new("record", "Ctrl+Shift+P", "Record dashboard CSV", "Dashboard"),
        new("firmware", "Ctrl+Alt+F", "Check adapter firmware", "Tools"),
        new("guide", "F1", "Guide", "Help"),
        new("shortcuts", "Shift+F1", "Keyboard shortcuts list", "Help"),
        new("fullscreen", "F11", "Full screen", "View"),
        new("zoomIn", "Ctrl++", "Zoom in", "View"),
        new("zoomOut", "Ctrl+-", "Zoom out", "View"),
        new("zoomReset", "Ctrl+0", "Reset zoom", "View"),
        new("exit", "Alt+F4", "Exit", "File"),
    };

    public static readonly string[] Pages = { "Dashboard", "Modules", "DTCs", "As-Built", "Vehicle", "Console", "Logs", "Guide", "Settings" };

    public static Shortcut Get(string id) => All.First(s => s.Id == id);
    public static string Gesture(string id) => Get(id).Gesture;

    /// <summary>Normalised gesture for a key press, e.g. ("D", ctrl, shift, alt) -> "Ctrl+Shift+D". Key names: "F5", "Delete", "Tab", "`", ",", "+", "-", "0", "1".</summary>
    public static string Format(string key, bool ctrl, bool shift, bool alt) => (ctrl ? "Ctrl+" : "") + (shift ? "Shift+" : "") + (alt ? "Alt+" : "") + key;

    /// <summary>Resolves a pressed gesture to a shortcut id (page shortcuts return "page:N"). <paramref name="inText"/>: focus is in a text box.</summary>
    public static string? Resolve(string gesture, bool inText)
    {
        if (gesture.Length == 5 && gesture.StartsWith("Alt+") && gesture[4] is >= '1' and <= '9') return "page:" + gesture[4];
        var s = gesture switch { "Ctrl+=" or "Ctrl+Shift+=" or "Ctrl+Shift++" => Get("zoomIn"), _ => All.FirstOrDefault(x => x.Gesture == gesture) };
        if (s is null || s.Id == "exit" || s.Id == "page") return null;
        return inText && !s.InText ? null : s.Id;
    }

    public static string Markdown()
    {
        var sb = new System.Text.StringBuilder("| Shortcut | Action |\n|---|---|\n");
        foreach (var s in All) sb.Append($"| {s.Gesture} | {s.Description} |\n");
        return sb.ToString();
    }

    public static string Text() => string.Join("\n", All.GroupBy(s => s.Group).Select(g => g.Key + "\n" + string.Join("\n", g.Select(s => $"  {s.Gesture,-20} {s.Description}"))));
}
