namespace ProductivityTracker.App.Models;

public sealed class DailyActivityData
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public int ActiveSeconds { get; set; }
    public int IdleSeconds { get; set; }
    public int CodingSeconds { get; set; }
    public int BrowsingSeconds { get; set; }
    public int OtherSeconds { get; set; }
    public List<AppUsageEntry> ApplicationUsage { get; set; } = new();
    public List<TimelineEntry> Timeline { get; set; } = new();
    public List<IdleSession> IdleSessions { get; set; } = new();
}
