using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ProductivityTracker.App.Models;

namespace ProductivityTracker.App.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private string _aiSuggestion = "No suggestion yet.";
    private int _totalTasks;
    private int _completedTasks;
    private int _pendingTasks;
    private int _productivityScore;
    private int _taskPoints;
    private string _activeTime = "0m 0s";
    private string _idleTime = "0m 0s";
    private string _statusText = "Running in background";
    private string _healthTracking = "Unknown";
    private string _healthHotkeys = "Unknown";
    private string _healthStartup = "Unknown";

    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<AppUsageEntry> TopApps { get; } = new();

    public int TotalTasks { get => _totalTasks; set => Set(ref _totalTasks, value); }
    public int CompletedTasks { get => _completedTasks; set => Set(ref _completedTasks, value); }
    public int PendingTasks { get => _pendingTasks; set => Set(ref _pendingTasks, value); }
    public int ProductivityScore { get => _productivityScore; set => Set(ref _productivityScore, value); }
    public int TaskPoints { get => _taskPoints; set => Set(ref _taskPoints, value); }
    public string ActiveTime { get => _activeTime; set => Set(ref _activeTime, value); }
    public string IdleTime { get => _idleTime; set => Set(ref _idleTime, value); }
    public string AiSuggestion { get => _aiSuggestion; set => Set(ref _aiSuggestion, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public string HealthTracking { get => _healthTracking; set => Set(ref _healthTracking, value); }
    public string HealthHotkeys { get => _healthHotkeys; set => Set(ref _healthHotkeys, value); }
    public string HealthStartup { get => _healthStartup; set => Set(ref _healthStartup, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
