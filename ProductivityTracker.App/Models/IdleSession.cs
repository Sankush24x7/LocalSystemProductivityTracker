namespace ProductivityTracker.App.Models;

public sealed class IdleSession
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    public int DurationSeconds => (int)Math.Max(0, (End - Start).TotalSeconds);
}
