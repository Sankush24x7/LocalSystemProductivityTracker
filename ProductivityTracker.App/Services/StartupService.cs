using Microsoft.Win32;

namespace ProductivityTracker.App.Services;

public sealed class StartupService
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppName = "ProductivityTrackerLite";

    public StartupHealthResult EnsureAutoStartWithHealth()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return new StartupHealthResult(false, false, "Process path not found.");
        }

        bool registryOk = TryRegisterInRunKey(exePath);
        bool startupFolderOk = EnsureStartupFolderLauncher(exePath);

        if (registryOk || startupFolderOk)
        {
            return new StartupHealthResult(registryOk, startupFolderOk, "Startup self-check complete.");
        }

        return new StartupHealthResult(false, false, "Could not register startup. App still runs this session.");
    }

    public void EnsureAutoStart()
    {
        _ = EnsureAutoStartWithHealth();
    }

    private static bool TryRegisterInRunKey(string exePath)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.SetValue(AppName, $"\"{exePath}\"");
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    private static bool EnsureStartupFolderLauncher(string exePath)
    {
        try
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (string.IsNullOrWhiteSpace(startupFolder))
            {
                return false;
            }

            Directory.CreateDirectory(startupFolder);
            string launcherPath = Path.Combine(startupFolder, $"{AppName}.vbs");
            string escapedExe = exePath.Replace("\"", "\"\"");

            string script =
                "Set WshShell = CreateObject(\"WScript.Shell\")" + Environment.NewLine +
                $"WshShell.Run \"\"\"\" & \"{escapedExe}\" & \"\"\"\", 0" + Environment.NewLine +
                "Set WshShell = Nothing";

            File.WriteAllText(launcherPath, script);
            return true;
        }
        catch
        {
            // If both mechanisms are blocked, app still runs for this session.
            return false;
        }
    }
}

public sealed record StartupHealthResult(bool RegistryRunConfigured, bool StartupFolderConfigured, string Message);
