using ProductivityTracker.App.Models;

namespace ProductivityTracker.App.Services;

public sealed class RetentionService
{
    public int Apply(AppSettings settings)
    {
        if (!settings.RetentionEnabled)
        {
            return 0;
        }

        int deleted = 0;
        deleted += CleanupFilePattern(LocalPaths.ActivityDirectory, "activity-*.json", settings.RetentionActivityDays);
        deleted += CleanupFilePattern(LocalPaths.ReportsDirectory, "productivity-report-*.html", settings.RetentionReportsDays);
        deleted += CleanupScreenshotFolders(settings.RetentionScreenshotsDays);
        return deleted;
    }

    private static int CleanupFilePattern(string directory, string pattern, int keepDays)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        int days = Math.Max(1, keepDays);
        DateTime cutoff = DateTime.Today.AddDays(-days);
        int deleted = 0;

        foreach (string file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
        {
            DateTime created = File.GetLastWriteTime(file);
            if (created < cutoff)
            {
                try
                {
                    File.Delete(file);
                    deleted++;
                }
                catch
                {
                    // Ignore deletion issues to keep app resilient.
                }
            }
        }

        return deleted;
    }

    private static int CleanupScreenshotFolders(int keepDays)
    {
        if (!Directory.Exists(LocalPaths.ScreenshotsDirectory))
        {
            return 0;
        }

        int days = Math.Max(1, keepDays);
        DateOnly cutoff = DateOnly.FromDateTime(DateTime.Today.AddDays(-days));
        int deleted = 0;

        foreach (string dir in Directory.EnumerateDirectories(LocalPaths.ScreenshotsDirectory))
        {
            string name = Path.GetFileName(dir);
            if (!DateOnly.TryParseExact(name, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly folderDay))
            {
                continue;
            }

            if (folderDay < cutoff)
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    deleted++;
                }
                catch
                {
                    // Ignore deletion issues to keep app resilient.
                }
            }
        }

        return deleted;
    }
}
