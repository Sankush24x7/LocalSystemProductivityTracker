namespace ProductivityTracker.App.Services;

public static class ErrorLogger
{
    private static readonly object Sync = new();

    public static string LogException(string source, Exception exception)
    {
        lock (Sync)
        {
            string logsDirectory = ResolveLogsDirectory();
            string filePath = Path.Combine(logsDirectory, $"error-{DateTime.Now:yyyyMMdd}.log");
            string content =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Source: {source}{Environment.NewLine}" +
                $"Type: {exception.GetType().FullName}{Environment.NewLine}" +
                $"Message: {exception.Message}{Environment.NewLine}" +
                $"StackTrace:{Environment.NewLine}{exception.StackTrace}{Environment.NewLine}";

            if (exception.InnerException is not null)
            {
                content +=
                    $"Inner Type: {exception.InnerException.GetType().FullName}{Environment.NewLine}" +
                    $"Inner Message: {exception.InnerException.Message}{Environment.NewLine}" +
                    $"Inner StackTrace:{Environment.NewLine}{exception.InnerException.StackTrace}{Environment.NewLine}";
            }

            content += new string('-', 80) + Environment.NewLine;
            File.AppendAllText(filePath, content);
            return filePath;
        }
    }

    private static string ResolveLogsDirectory()
    {
        try
        {
            string localDataPath = Path.Combine(LocalPaths.DataRoot, "ErrorLogs");
            Directory.CreateDirectory(localDataPath);
            return localDataPath;
        }
        catch
        {
            string appBase = AppContext.BaseDirectory;
            string fallbackPath = Path.Combine(appBase, "ErrorLogs");
            Directory.CreateDirectory(fallbackPath);
            return fallbackPath;
        }
    }
}
