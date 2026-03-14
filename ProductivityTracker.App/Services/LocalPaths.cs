namespace ProductivityTracker.App.Services;

public static class LocalPaths
{
    private static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProductivityTrackerLite");

    public static string DataRoot => Root;
    public static string TasksFile => Path.Combine(Root, "tasks.json");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string ReportsDirectory => Path.Combine(Root, "Reports");
    public static string ActivityDirectory => Path.Combine(Root, "Activity");
    public static string ScreenshotsDirectory => Path.Combine(Root, "Screenshots");
    public static string BackupsDirectory => Path.Combine(Root, "Backups");
    public static string ReportUserName => SanitizeFileToken(Environment.UserName);

    public static string GetActivityFile(DateOnly date) =>
        Path.Combine(ActivityDirectory, $"activity-{date:yyyy-MM-dd}.json");

    public static string GetDailyReportFile(DateOnly date) =>
        Path.Combine(ReportsDirectory, $"productivity-report-{ReportUserName}-{date:yyyy-MM-dd}.html");

    public static string GetScreenshotDayFolder(DateOnly date) =>
        Path.Combine(ScreenshotsDirectory, date.ToString("yyyy-MM-dd"));

    public static string GetBackupFile(DateTime when) =>
        Path.Combine(BackupsDirectory, $"backup-{when:yyyyMMdd-HHmmss}.ptbackup");

    public static void EnsureAll()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(ReportsDirectory);
        Directory.CreateDirectory(ActivityDirectory);
        Directory.CreateDirectory(ScreenshotsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "user";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] cleaned = value
            .Trim()
            .Select(ch => invalid.Contains(ch) ? '-' : ch)
            .ToArray();

        string normalized = new string(cleaned);
        return string.IsNullOrWhiteSpace(normalized) ? "user" : normalized;
    }
}
