namespace ObdLink.Core.Dashboard;

public static class DashboardSession
{
    public static bool ShouldRun(bool connected, bool windowOpen, bool keepLiveWhileClosed) =>
        connected && (windowOpen || keepLiveWhileClosed);

    public static bool PollAllowed(bool connected, bool windowOpen, bool keepLiveWhileClosed, bool userPaused) =>
        ShouldRun(connected, windowOpen, keepLiveWhileClosed) && !userPaused;

    public static string StateText(bool connected, bool windowOpen, bool keepLiveWhileClosed, bool userPaused, bool engaged, bool loopFailed)
    {
        if (!connected) return "Stopped";
        var allowed = PollAllowed(connected, windowOpen, keepLiveWhileClosed, userPaused);
        if (!allowed && (userPaused || engaged)) return "Paused";
        if (!allowed) return "Stopped";
        if (loopFailed) return "Stopped";
        return "Live";
    }
}
