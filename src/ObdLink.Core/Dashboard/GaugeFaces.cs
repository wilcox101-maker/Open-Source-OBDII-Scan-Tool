namespace ObdLink.Core.Dashboard;

public static class GaugeFaces
{
    public const string Analog = "Analog";
    public const string Digital = "Digital";

    public static string Normalize(string? face) =>
        string.Equals(face, Digital, StringComparison.OrdinalIgnoreCase) ? Digital : Analog;

    public static bool ShowsNeedle(DisplayKind display, string? face) =>
        display == DisplayKind.Dial && Normalize(face) == Analog;
}
