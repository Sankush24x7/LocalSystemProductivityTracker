using System.Collections.ObjectModel;
using System.Windows;
using ProductivityTracker.App.Models;
using ProductivityTracker.App.Services;

namespace ProductivityTracker.App;

public partial class TrendWindow : Window
{
    private readonly StorageService _storage;
    private readonly TaskService _tasks;
    private readonly AppSettings _settings;

    private readonly ObservableCollection<TrendRow> _rows = new();

    public TrendWindow(StorageService storage, TaskService tasks, AppSettings settings)
    {
        _storage = storage;
        _tasks = tasks;
        _settings = settings;

        InitializeComponent();
        TrendGrid.ItemsSource = _rows;
        LoadData();
    }

    private void LoadData()
    {
        _rows.Clear();

        List<DailyActivityData> month = _storage.LoadRecentActivity(30);
        List<DailyActivityData> week = month.TakeLast(7).ToList();

        int weekActive = week.Sum(d => d.ActiveSeconds);
        int monthActive = month.Sum(d => d.ActiveSeconds);

        WeekActiveText.Text = ReportService.FormatDuration(weekActive);
        MonthActiveText.Text = ReportService.FormatDuration(monthActive);

        int todayActive = month.LastOrDefault()?.ActiveSeconds ?? 0;
        int activeGoalSeconds = Math.Max(1, _settings.DailyActiveGoalHours) * 3600;
        int todayCompleted = _tasks.GetAll().Count(t => t.Status == ProductivityTracker.App.Models.TaskStatus.Completed && t.CompletedTime?.Date == DateTime.Today);

        GoalHoursText.Text = $"{Math.Round(todayActive / 3600d, 1)} / {_settings.DailyActiveGoalHours}h";
        GoalTasksText.Text = $"{todayCompleted} / {_settings.DailyTaskGoal}";

        HoursGoalProgress.Value = Math.Clamp((todayActive * 100d) / activeGoalSeconds, 0, 100);
        TasksGoalProgress.Value = Math.Clamp((todayCompleted * 100d) / Math.Max(1, _settings.DailyTaskGoal), 0, 100);
        HoursGoalPctText.Text = $"{HoursGoalProgress.Value:0}%";
        TasksGoalPctText.Text = $"{TasksGoalProgress.Value:0}%";

        foreach (DailyActivityData day in month.OrderByDescending(d => d.Date))
        {
            double score = day.ActiveSeconds == 0
                ? 0
                : Math.Clamp(((day.CodingSeconds + (0.4 * day.BrowsingSeconds)) / day.ActiveSeconds) * 100, 0, 100);

            _rows.Add(new TrendRow
            {
                Date = day.Date.ToString("yyyy-MM-dd"),
                Active = ReportService.FormatDuration(day.ActiveSeconds),
                Idle = ReportService.FormatDuration(day.IdleSeconds),
                Score = $"{score:0}%"
            });
        }

        GoalHintText.Text = HoursGoalProgress.Value >= 100 && TasksGoalProgress.Value >= 100
            ? "Great job. Daily goals are completed."
            : "Tip: Complete pending tasks before due time to improve score and points.";
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadData();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private sealed class TrendRow
    {
        public string Date { get; set; } = string.Empty;
        public string Active { get; set; } = string.Empty;
        public string Idle { get; set; } = string.Empty;
        public string Score { get; set; } = string.Empty;
    }
}
