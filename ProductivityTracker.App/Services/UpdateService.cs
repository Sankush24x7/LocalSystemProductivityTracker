using System.Reflection;
using System.Text.Json;

namespace ProductivityTracker.App.Services;

public sealed class UpdateService
{
    private const string ManifestName = "productivity-update.json";

    public UpdateCheckResult CheckOfflineUpdate(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return UpdateCheckResult.None("Offline update path is not configured.");
        }

        string manifestPath = configuredPath;
        if (Directory.Exists(configuredPath))
        {
            manifestPath = Path.Combine(configuredPath, ManifestName);
        }

        if (!File.Exists(manifestPath))
        {
            return UpdateCheckResult.None($"Manifest not found: {manifestPath}");
        }

        string json = File.ReadAllText(manifestPath);
        OfflineUpdateManifest? manifest = JsonSerializer.Deserialize<OfflineUpdateManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
        {
            return UpdateCheckResult.None("Invalid update manifest.");
        }

        Version current = GetCurrentVersion();
        Version available = ParseVersion(manifest.Version) ?? current;

        if (available <= current)
        {
            return UpdateCheckResult.None($"No newer update found. Current: {current}");
        }

        string packagePath = manifest.PackagePath ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(packagePath) && !Path.IsPathRooted(packagePath) && Directory.Exists(configuredPath))
        {
            packagePath = Path.GetFullPath(Path.Combine(configuredPath, packagePath));
        }

        return new UpdateCheckResult(true, $"Update available: {available}", packagePath, manifest.Notes ?? string.Empty, available.ToString());
    }

    private static Version GetCurrentVersion()
    {
        Version? v = Assembly.GetExecutingAssembly().GetName().Version;
        return v ?? new Version(1, 0, 0, 0);
    }

    private static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Version.TryParse(value, out Version? parsed) ? parsed : null;
    }

    private sealed class OfflineUpdateManifest
    {
        public string? Version { get; set; }
        public string? PackagePath { get; set; }
        public string? Notes { get; set; }
    }
}

public sealed record UpdateCheckResult(bool HasUpdate, string Message, string PackagePath, string Notes, string Version)
{
    public static UpdateCheckResult None(string message) => new(false, message, string.Empty, string.Empty, string.Empty);
}
