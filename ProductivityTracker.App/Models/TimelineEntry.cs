namespace ProductivityTracker.App.Models;

public sealed class TimelineEntry
{
    public DateTime Timestamp { get; set; }
    public string ActivityName { get; set; } = string.Empty;
}
