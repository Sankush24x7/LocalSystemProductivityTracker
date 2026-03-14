using System.Text.Json;
using ProductivityTracker.App.Models;

namespace ProductivityTracker.App.Services;

public sealed class StorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public StorageService()
    {
        LocalPaths.EnsureAll();
    }

    public List<TaskItem> LoadTasks()
    {
        if (!File.Exists(LocalPaths.TasksFile))
        {
            return new List<TaskItem>();
        }

        string json = File.ReadAllText(LocalPaths.TasksFile);
        return JsonSerializer.Deserialize<List<TaskItem>>(json, JsonOptions) ?? new List<TaskItem>();
    }

    public void SaveTasks(IEnumerable<TaskItem> tasks)
    {
        string json = JsonSerializer.Serialize(tasks, JsonOptions);
        File.WriteAllText(LocalPaths.TasksFile, json);
    }

    public AppSettings LoadSettings()
    {
        if (!File.Exists(LocalPaths.SettingsFile))
        {
            var defaultSettings = new AppSettings();
            SaveSettings(defaultSettings);
            return defaultSettings;
        }

        string json = File.ReadAllText(LocalPaths.SettingsFile);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(LocalPaths.SettingsFile, json);
    }

    public DailyActivityData LoadActivity(DateOnly date)
    {
        string path = LocalPaths.GetActivityFile(date);
        if (!File.Exists(path))
        {
            return new DailyActivityData { Date = date };
        }

        string json = File.ReadAllText(path);
        DailyActivityData? data = JsonSerializer.Deserialize<DailyActivityData>(json, JsonOptions);
        return data ?? new DailyActivityData { Date = date };
    }

    public void SaveActivity(DailyActivityData data)
    {
        string path = LocalPaths.GetActivityFile(data.Date);
        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }

    public List<DailyActivityData> LoadRecentActivity(int dayCount)
    {
        var result = new List<DailyActivityData>();
        for (int i = dayCount - 1; i >= 0; i--)
        {
            DateOnly day = DateOnly.FromDateTime(DateTime.Today.AddDays(-i));
            result.Add(LoadActivity(day));
        }

        return result;
    }
}
