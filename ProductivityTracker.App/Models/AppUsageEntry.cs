namespace ProductivityTracker.App.Models;

public sealed class AppUsageEntry
{
    public string ApplicationName { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }

    public string DurationLabel
    {
        get
        {
            TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, DurationSeconds));
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}h {span.Minutes}m";
            }

            return $"{span.Minutes}m {span.Seconds}s";
        }
    }
}
