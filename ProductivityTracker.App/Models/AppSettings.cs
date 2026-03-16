namespace ProductivityTracker.App.Models;

public sealed class AppSettings
{
    public bool ScreenshotEnabled { get; set; }
    public int ScreenshotIntervalMinutes { get; set; } = 10;
    public int IdleThresholdMinutes { get; set; } = 3;

    public bool DueSoundEnabled { get; set; } = true;
    public bool DueTrayBalloonEnabled { get; set; } = true;
    public int DueCheckIntervalSeconds { get; set; } = 30;
    public bool DueReminder5MinEnabled { get; set; } = true;
    public bool DueReminder15MinEnabled { get; set; } = true;
    public bool DueReminder30MinEnabled { get; set; } = true;

    public int ActivityTrackingIntervalSeconds { get; set; } = 5;
    public bool TrackingPaused { get; set; }

    // Daily tracking window (local time). Default: 09:00 AM to 07:00 PM.
    public string BackgroundStartTime { get; set; } = "09:00 AM";
    public string BackgroundStopTime { get; set; } = "07:00 PM";

    // One rule per line: token=Category (Category: Coding/Browsing/Other)
    public string AppCategoryRulesText { get; set; } = "devenv=Coding\ncode=Coding\nchrome=Browsing\nmsedge=Browsing\npowershell=Coding";

    public bool RetentionEnabled { get; set; } = true;
    public int RetentionActivityDays { get; set; } = 30;
    public int RetentionScreenshotsDays { get; set; } = 14;
    public int RetentionReportsDays { get; set; } = 30;

    public bool PinLockEnabled { get; set; }
    public string PinHash { get; set; } = string.Empty;

    public int DailyActiveGoalHours { get; set; } = 6;
    public int DailyTaskGoal { get; set; } = 5;

    public bool OfflineUpdateCheckEnabled { get; set; }
    public string OfflineUpdatePath { get; set; } = string.Empty;

    // Local backup encryption password.
    public string BackupPassword { get; set; } = string.Empty;

    // Mail settings for "Send Report".
    public string MailFrom { get; set; } = string.Empty;
    public string MailTo { get; set; } = string.Empty;
    public string MailPassword { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
}
