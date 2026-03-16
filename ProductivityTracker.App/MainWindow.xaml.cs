using System.Diagnostics;
using System.Media;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ProductivityTracker.App.Helpers;
using ProductivityTracker.App.Models;
using ProductivityTracker.App.Services;
using ProductivityTracker.App.ViewModels;

namespace ProductivityTracker.App;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyOpenDashboard = 9001;
    private const int HotkeyGenerateReport = 9002;
    private const int HotkeyNewTask = 9003;
    private const int HotkeyQuickAdd = 9004;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;

    private readonly DashboardViewModel _vm = new();
    private readonly TaskService _taskService;
    private readonly ActivityTrackerService _activityService;
    private readonly ReportService _reportService;
    private readonly StorageService _storage;
    private readonly ScreenshotService _screenshotService;
    private readonly AppSettings _settings;

    private readonly TrayService _trayService;
    private readonly BackupRestoreService _backupService = new();
    private readonly RetentionService _retentionService = new();
    private readonly UpdateService _updateService = new();
    private readonly EmailService _emailService = new();

    private readonly System.Windows.Threading.DispatcherTimer _uiTimer;
    private readonly System.Windows.Threading.DispatcherTimer _dueAlertTimer;
    private readonly System.Windows.Threading.DispatcherTimer _hotkeyRecoveryTimer;

    private readonly HashSet<Guid> _dueAlertedTaskIds = new();
    private readonly HashSet<string> _reminderAlertedKeys = new();

    private Guid? _selectedTaskId;
    private bool _allowRealClose;
    private bool _sessionUnlocked;
    private IReadOnlyList<TaskItem> _latestTasks = Array.Empty<TaskItem>();
    private bool _hotkeysHealthy;
    private bool _startupHealthy;

    public MainWindow(TaskService taskService, ActivityTrackerService activityService, ReportService reportService, StorageService storage, ScreenshotService screenshotService, AppSettings settings)
    {
        _taskService = taskService;
        _activityService = activityService;
        _reportService = reportService;
        _storage = storage;
        _screenshotService = screenshotService;
        _settings = settings;

        _sessionUnlocked = !_settings.PinLockEnabled;

        InitializeComponent();
        DataContext = _vm;

        PriorityComboBox.ItemsSource = Enum.GetValues<TaskPriority>();
        StatusComboBox.ItemsSource = Enum.GetValues<ProductivityTracker.App.Models.TaskStatus>();
        PriorityComboBox.SelectedItem = TaskPriority.Medium;
        StatusComboBox.SelectedItem = ProductivityTracker.App.Models.TaskStatus.Pending;
        DueDatePicker.SelectedDate = DateTime.Today;
        FilterStatusComboBox.SelectedIndex = 0;
        FilterPriorityComboBox.SelectedIndex = 0;

        _trayService = new TrayService(
            onOpenDashboard: () => Dispatcher.Invoke(OpenDashboard),
            onQuickAddTask: () => Dispatcher.Invoke(OpenQuickAddTask),
            onShowShortcuts: () => Dispatcher.Invoke(ShowShortcutHelp),
            onGenerateReport: () => Dispatcher.Invoke(GenerateAndOpenReport),
            onSendReport: () => Dispatcher.Invoke(SendReport),
            onOpenReport: () => Dispatcher.Invoke(OpenReport),
            onManageReports: () => Dispatcher.Invoke(OpenReportManager),
            onOpenTrends: () => Dispatcher.Invoke(OpenTrends),
            onBackupNow: () => Dispatcher.Invoke(BackupNow),
            onRestoreBackup: () => Dispatcher.Invoke(RestoreBackup),
            onCheckUpdate: () => Dispatcher.Invoke(CheckOfflineUpdate),
            onTogglePause: () => Dispatcher.Invoke(ToggleTrackingPause),
            onLockNow: () => Dispatcher.Invoke(LockNow),
            onSettings: () => Dispatcher.Invoke(OpenSettings),
            onExit: () => Dispatcher.Invoke(ExitApplication));

        _taskService.TasksChanged += RefreshDashboard;
        _activityService.ActivityUpdated += RefreshDashboard;

        _uiTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _uiTimer.Tick += (_, _) => RefreshDashboard();
        _uiTimer.Start();

        _dueAlertTimer = new System.Windows.Threading.DispatcherTimer();
        _dueAlertTimer.Tick += (_, _) => CheckDueAlerts();
        UpdateDueAlertTimerInterval();
        _dueAlertTimer.Start();

        _hotkeyRecoveryTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _hotkeyRecoveryTimer.Tick += (_, _) => RecoverHotkeys();
        _hotkeyRecoveryTimer.Start();

        Loaded += (_, _) =>
        {
            StartupHealthResult startup = new StartupService().EnsureAutoStartWithHealth();
            _startupHealthy = startup.RegistryRunConfigured || startup.StartupFolderConfigured;
            int cleaned = _retentionService.Apply(_settings);

            _activityService.RefreshTrackingWindow();
            _trayService.SetPauseState(_settings.TrackingPaused);
            UpdatePauseButtonText();
            RefreshDashboard();
            CheckDueAlerts();
            _vm.StatusText = $"{startup.Message} Cleanup removed {cleaned} old item(s).";

            if (_settings.OfflineUpdateCheckEnabled)
            {
                CheckOfflineUpdate(silent: true);
            }
        };
    }

    public void OpenDashboard()
    {
        if (!EnsureUnlocked("Open dashboard"))
        {
            return;
        }

        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        _vm.StatusText = "Dashboard opened via tray/shortcut.";
    }

    public void OpenAddTask()
    {
        OpenDashboard();
        ClearTaskForm();
        TitleTextBox.Focus();
    }

    public void OpenQuickAddTask()
    {
        if (!EnsureUnlocked("Quick add task")) return;

        var quick = new QuickAddTaskWindow { Owner = this };
        if (quick.ShowDialog() == true && quick.Result is not null)
        {
            _taskService.Add(quick.Result);
            _vm.StatusText = "Task added quickly.";
            OpenDashboard();
            RefreshDashboard();
        }
    }

    private void ShowShortcutHelp()
    {
        const string shortcuts = "Global Shortcuts:\nCtrl + Shift + T  -> Open Dashboard\nCtrl + Shift + R  -> Generate + Open Report\nCtrl + Shift + N  -> Quick Add Task\nCtrl + Shift + Q  -> Quick Add Task (Mini Window)";
        System.Windows.MessageBox.Show(shortcuts, "App Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
        _vm.StatusText = "Shortcut help opened.";
    }

    private void GenerateReport()
    {
        _reportService.GenerateDailyReport();
        _vm.StatusText = "Report generated successfully.";
    }

    private void GenerateAndOpenReport()
    {
        GenerateReport();
        OpenReport();
    }

    private void SendReport()
    {
        try
        {
            string htmlPath = _reportService.GenerateDailyReport();
            string jsonPath = LocalPaths.GetActivityFile(DateOnly.FromDateTime(DateTime.Today));
            _emailService.SendReport(_settings, htmlPath, jsonPath);
            _vm.StatusText = "Report email sent successfully.";
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"Send report failed: {ex.Message}";
        }
    }

    private void OpenReport()
    {
        if (!EnsureUnlocked("Open report")) return;

        string todayReport = LocalPaths.GetDailyReportFile(DateOnly.FromDateTime(DateTime.Today));
        if (!File.Exists(todayReport))
        {
            GenerateReport();
        }

        Process.Start(new ProcessStartInfo { FileName = todayReport, UseShellExecute = true });
    }

    private void OpenReportManager()
    {
        if (!EnsureUnlocked("Open report manager")) return;
        var window = new ReportManagerWindow { Owner = this };
        window.ShowDialog();
    }

    private void OpenTrends()
    {
        if (!EnsureUnlocked("Open trends")) return;
        var window = new TrendWindow(_storage, _taskService, _settings) { Owner = this };
        window.ShowDialog();
    }

    private void BackupNow()
    {
        try
        {
            string file = _backupService.CreateBackupEncrypted(_settings.BackupPassword);
            _vm.StatusText = $"Backup created successfully ({Path.GetFileName(file)}).";
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"Backup failed: {ex.Message}";
        }
    }

    private void RestoreBackup()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Encrypted backup (*.ptbackup)|*.ptbackup|Zip backup (*.zip)|*.zip|All files (*.*)|*.*",
            Multiselect = false,
            InitialDirectory = LocalPaths.BackupsDirectory
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            _backupService.RestoreBackup(dialog.FileName, _settings.BackupPassword);
            _taskService.ReloadFromStorage();
            _activityService.RefreshRules();
            _vm.StatusText = "Backup restored successfully.";
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"Restore failed: {ex.Message}";
        }
    }

    private void CheckOfflineUpdate(bool silent = false)
    {
        UpdateCheckResult result = _updateService.CheckOfflineUpdate(_settings.OfflineUpdatePath);
        if (!result.HasUpdate)
        {
            if (!silent)
            {
                _vm.StatusText = result.Message;
            }
            return;
        }

        _vm.StatusText = result.Message;
        if (!string.IsNullOrWhiteSpace(result.PackagePath) && File.Exists(result.PackagePath))
        {
            MessageBoxResult open = System.Windows.MessageBox.Show($"{result.Message}\n\nNotes: {result.Notes}\n\nOpen update package now?", "Offline Update", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo { FileName = result.PackagePath, UseShellExecute = true });
            }
        }
    }

    private void ToggleTrackingPause()
    {
        bool pause = !_settings.TrackingPaused;
        _settings.TrackingPaused = pause;
        _storage.SaveSettings(_settings);
        _activityService.SetPaused(pause);
        _trayService.SetPauseState(pause);
        UpdatePauseButtonText();
        _vm.StatusText = pause ? "Tracking paused for privacy." : "Tracking resumed.";
    }

    private void UpdatePauseButtonText() => PauseResumeButton.Content = _settings.TrackingPaused ? "Resume Tracking" : "Pause Tracking";

    private void LockNow()
    {
        _sessionUnlocked = false;
        HideToTray();
        _vm.StatusText = "Dashboard locked. Enter PIN to open.";
    }

    private bool EnsureUnlocked(string reason)
    {
        if (!_settings.PinLockEnabled) return true;
        if (string.IsNullOrWhiteSpace(_settings.PinHash)) return true;
        if (_sessionUnlocked) return true;

        var pin = new PinPromptWindow(_settings.PinHash, $"{reason} requires PIN unlock.") { Owner = this };
        bool ok = pin.ShowDialog() == true && pin.IsVerified;
        _sessionUnlocked = ok;
        return ok;
    }

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settings) { Owner = this };
        if (window.ShowDialog() != true || window.Result is null) return;

        _settings.ScreenshotEnabled = window.Result.ScreenshotEnabled;
        _settings.ScreenshotIntervalMinutes = window.Result.ScreenshotIntervalMinutes;
        _settings.IdleThresholdMinutes = window.Result.IdleThresholdMinutes;
        _settings.DueSoundEnabled = window.Result.DueSoundEnabled;
        _settings.DueTrayBalloonEnabled = window.Result.DueTrayBalloonEnabled;
        _settings.DueReminder30MinEnabled = window.Result.DueReminder30MinEnabled;
        _settings.DueReminder15MinEnabled = window.Result.DueReminder15MinEnabled;
        _settings.DueReminder5MinEnabled = window.Result.DueReminder5MinEnabled;
        _settings.DueCheckIntervalSeconds = window.Result.DueCheckIntervalSeconds;
        _settings.ActivityTrackingIntervalSeconds = window.Result.ActivityTrackingIntervalSeconds;
        _settings.BackgroundStartTime = window.Result.BackgroundStartTime;
        _settings.BackgroundStopTime = window.Result.BackgroundStopTime;
        _settings.AppCategoryRulesText = window.Result.AppCategoryRulesText;
        _settings.RetentionEnabled = window.Result.RetentionEnabled;
        _settings.RetentionActivityDays = window.Result.RetentionActivityDays;
        _settings.RetentionScreenshotsDays = window.Result.RetentionScreenshotsDays;
        _settings.RetentionReportsDays = window.Result.RetentionReportsDays;
        _settings.PinLockEnabled = window.Result.PinLockEnabled;
        _settings.PinHash = window.Result.PinHash;
        _settings.DailyActiveGoalHours = window.Result.DailyActiveGoalHours;
        _settings.DailyTaskGoal = window.Result.DailyTaskGoal;
        _settings.OfflineUpdateCheckEnabled = window.Result.OfflineUpdateCheckEnabled;
        _settings.OfflineUpdatePath = window.Result.OfflineUpdatePath;
        _settings.BackupPassword = window.Result.BackupPassword;
        _settings.MailFrom = window.Result.MailFrom;
        _settings.MailTo = window.Result.MailTo;
        _settings.MailPassword = window.Result.MailPassword;
        _settings.SmtpHost = window.Result.SmtpHost;
        _settings.SmtpPort = window.Result.SmtpPort;
        _settings.SmtpUseSsl = window.Result.SmtpUseSsl;

        _storage.SaveSettings(_settings);
        _screenshotService.RefreshInterval();
        _activityService.RefreshSamplingInterval();
        _activityService.RefreshTrackingWindow();
        _activityService.RefreshRules();
        UpdateDueAlertTimerInterval();
        _trayService.SetPauseState(_settings.TrackingPaused);

        int cleaned = _retentionService.Apply(_settings);

        if (_settings.ScreenshotEnabled) _screenshotService.Start(); else _screenshotService.Stop();
        if (!_settings.PinLockEnabled) _sessionUnlocked = true;

        _vm.StatusText = $"Settings updated. Cleanup removed {cleaned} old item(s).";
    }

    private void ExitApplication()
    {
        _allowRealClose = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        HwndSource source = HwndSource.FromHwnd(handle);
        source.AddHook(WndProc);
        RegisterHotkeys(handle);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowRealClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        UnregisterHotkeys(new WindowInteropHelper(this).Handle);
        _trayService.Dispose();
        _uiTimer.Stop();
        _dueAlertTimer.Stop();
        _hotkeyRecoveryTimer.Stop();
        base.OnClosing(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero;

        int id = wParam.ToInt32();
        if (id == HotkeyOpenDashboard) { OpenDashboard(); handled = true; }
        else if (id == HotkeyGenerateReport) { GenerateAndOpenReport(); handled = true; }
        else if (id == HotkeyNewTask) { OpenQuickAddTask(); handled = true; }
        else if (id == HotkeyQuickAdd) { OpenQuickAddTask(); handled = true; }
        return IntPtr.Zero;
    }

    private void RegisterHotkeys(IntPtr handle)
    {
        uint mod = ModControl | ModShift;
        bool okDashboard = NativeMethods.RegisterHotKey(handle, HotkeyOpenDashboard, mod, (uint)KeyInterop.VirtualKeyFromKey(Key.T));
        bool okReport = NativeMethods.RegisterHotKey(handle, HotkeyGenerateReport, mod, (uint)KeyInterop.VirtualKeyFromKey(Key.R));
        bool okNew = NativeMethods.RegisterHotKey(handle, HotkeyNewTask, mod, (uint)KeyInterop.VirtualKeyFromKey(Key.N));
        bool okQuick = NativeMethods.RegisterHotKey(handle, HotkeyQuickAdd, mod, (uint)KeyInterop.VirtualKeyFromKey(Key.Q));

        if (!okReport)
        {
            NativeMethods.UnregisterHotKey(handle, HotkeyGenerateReport);
            okReport = NativeMethods.RegisterHotKey(handle, HotkeyGenerateReport, mod, (uint)KeyInterop.VirtualKeyFromKey(Key.R));
        }

        _hotkeysHealthy = okDashboard && okReport && okNew && okQuick;
        if (!_hotkeysHealthy)
        {
            _vm.StatusText = "Some hotkeys failed to register. Auto-recovery is active; tray menu works as fallback.";
        }
    }

    private void RecoverHotkeys()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        UnregisterHotkeys(handle);
        RegisterHotkeys(handle);
    }

    private static void UnregisterHotkeys(IntPtr handle)
    {
        NativeMethods.UnregisterHotKey(handle, HotkeyOpenDashboard);
        NativeMethods.UnregisterHotKey(handle, HotkeyGenerateReport);
        NativeMethods.UnregisterHotKey(handle, HotkeyNewTask);
        NativeMethods.UnregisterHotKey(handle, HotkeyQuickAdd);
    }

    private void RefreshDashboard()
    {
        IReadOnlyList<TaskItem> tasks = _taskService.GetAll();
        var summary = _taskService.GetSummary();
        var pointSummary = _taskService.GetPointSummary();
        DailyActivityData activity = _activityService.Snapshot();

        _latestTasks = tasks;
        ApplyTaskFilters();

        _vm.TopApps.Clear();
        foreach (AppUsageEntry app in activity.ApplicationUsage.OrderByDescending(a => a.DurationSeconds).Take(5))
        {
            _vm.TopApps.Add(new AppUsageEntry { ApplicationName = app.ApplicationName, DurationSeconds = app.DurationSeconds });
        }

        _vm.TotalTasks = summary.Total;
        _vm.CompletedTasks = summary.Completed;
        _vm.PendingTasks = summary.Pending;
        _vm.ActiveTime = ReportService.FormatDuration(activity.ActiveSeconds);
        _vm.IdleTime = ReportService.FormatDuration(activity.IdleSeconds);
        _vm.TaskPoints = pointSummary.Points;
        _vm.ProductivityScore = ComputeTaskBasedScore(summary.Total, pointSummary.GoodCount, pointSummary.NeutralCount, pointSummary.BadCount);
        _vm.AiSuggestion = BuildSuggestion(activity);

        _vm.HealthTracking = _activityService.IsPaused ? "Tracking: Paused" : "Tracking: Running";
        _vm.HealthHotkeys = _hotkeysHealthy ? "Hotkeys: OK" : "Hotkeys: Recovering";
        _vm.HealthStartup = _startupHealthy ? "Startup: OK" : "Startup: Needs attention";

        HashSet<Guid> activeTaskIds = tasks.Where(t => t.DueTime is not null && t.Status != ProductivityTracker.App.Models.TaskStatus.Completed).Select(t => t.Id).ToHashSet();
        _dueAlertedTaskIds.RemoveWhere(id => !activeTaskIds.Contains(id));

        HashSet<string> activeReminderKeys = tasks.Where(t => t.DueTime is not null && t.Status != ProductivityTracker.App.Models.TaskStatus.Completed).SelectMany(t => new[] { 5, 15, 30 }.Select(m => $"{t.Id}:{m}")).ToHashSet();
        _reminderAlertedKeys.RemoveWhere(k => !activeReminderKeys.Contains(k));
    }

    private void ApplyTaskFilters()
    {
        IEnumerable<TaskItem> filtered = _latestTasks;
        string search = TaskSearchTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(search)) filtered = filtered.Where(t => t.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || t.Description.Contains(search, StringComparison.OrdinalIgnoreCase));

        string statusFilter = GetComboText(FilterStatusComboBox);
        if (!string.Equals(statusFilter, "All Status", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<ProductivityTracker.App.Models.TaskStatus>(statusFilter, out var statusValue)) filtered = filtered.Where(t => t.Status == statusValue);

        string priorityFilter = GetComboText(FilterPriorityComboBox);
        if (!string.Equals(priorityFilter, "All Priority", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<TaskPriority>(priorityFilter, out var priorityValue)) filtered = filtered.Where(t => t.Priority == priorityValue);

        if (FilterDueTodayCheckBox.IsChecked == true) filtered = filtered.Where(t => t.DueTime?.Date == DateTime.Today);
        if (FilterOverdueCheckBox.IsChecked == true) filtered = filtered.Where(t => t.IsOverdue);

        _vm.Tasks.Clear();
        foreach (TaskItem task in filtered) _vm.Tasks.Add(task);

        if (_vm.Tasks.All(t => t.Id != _selectedTaskId))
        {
            TasksDataGrid.SelectedItem = null;
            _selectedTaskId = null;
        }
    }

    private static string GetComboText(System.Windows.Controls.ComboBox combo) => combo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Content is not null ? item.Content.ToString() ?? string.Empty : string.Empty;

    private void TaskSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyTaskFilters();
    private void FilterControl_Changed(object sender, RoutedEventArgs e) => ApplyTaskFilters();

    private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        TaskSearchTextBox.Text = string.Empty;
        FilterStatusComboBox.SelectedIndex = 0;
        FilterPriorityComboBox.SelectedIndex = 0;
        FilterDueTodayCheckBox.IsChecked = false;
        FilterOverdueCheckBox.IsChecked = false;
        ApplyTaskFilters();
        _vm.StatusText = "Task filters cleared.";
    }

    private void CheckDueAlerts()
    {
        DateTime now = DateTime.Now;
        List<TaskItem> watchTasks = _taskService.GetAll().Where(t => t.DueTime is not null && t.Status != ProductivityTracker.App.Models.TaskStatus.Completed).OrderBy(t => t.DueTime).ToList();

        foreach (TaskItem task in watchTasks)
        {
            TriggerReminderIfNeeded(task, 30, _settings.DueReminder30MinEnabled, now);
            TriggerReminderIfNeeded(task, 15, _settings.DueReminder15MinEnabled, now);
            TriggerReminderIfNeeded(task, 5, _settings.DueReminder5MinEnabled, now);
        }

        foreach (TaskItem task in watchTasks.Where(t => t.DueTime <= now))
        {
            if (_dueAlertedTaskIds.Contains(task.Id)) continue;
            _dueAlertedTaskIds.Add(task.Id);

            if (_settings.DueSoundEnabled) SystemSounds.Exclamation.Play();
            if (_settings.DueTrayBalloonEnabled) _trayService.ShowDueAlertBalloon(task.Title);

            OpenDashboard();
            var popup = new DueAlertWindow(task) { Owner = this };
            popup.Show();
            _vm.StatusText = $"Due alert triggered for task: {task.Title}";
        }
    }

    private void TriggerReminderIfNeeded(TaskItem task, int minutesBefore, bool enabled, DateTime now)
    {
        if (!enabled || task.DueTime is null) return;

        TimeSpan left = task.DueTime.Value - now;
        if (left.TotalMinutes <= 0) return;

        int remainingWholeMinutes = (int)Math.Ceiling(left.TotalMinutes);
        if (remainingWholeMinutes > minutesBefore) return;

        string key = $"{task.Id}:{minutesBefore}";
        if (_reminderAlertedKeys.Contains(key)) return;

        _reminderAlertedKeys.Add(key);
        if (_settings.DueSoundEnabled) SystemSounds.Asterisk.Play();
        if (_settings.DueTrayBalloonEnabled) _trayService.ShowReminderBalloon(task.Title, minutesBefore);
        _vm.StatusText = $"Reminder: '{task.Title}' is due in {minutesBefore} minute(s).";
    }

    private void UpdateDueAlertTimerInterval()
    {
        int intervalSeconds = _settings.DueCheckIntervalSeconds;
        if (intervalSeconds is not (10 or 30 or 60))
        {
            intervalSeconds = 30;
            _settings.DueCheckIntervalSeconds = 30;
        }
        _dueAlertTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    private string BuildSuggestion(DailyActivityData activity)
    {
        if (_settings.TrackingPaused) return "Tracking is paused. Resume to continue productivity analytics.";

        TaskItem? pending = _taskService.GetFirstPending();
        AppUsageEntry? top = activity.ApplicationUsage.OrderByDescending(a => a.DurationSeconds).FirstOrDefault();
        if (top is not null && top.ApplicationName.Contains("code", StringComparison.OrdinalIgnoreCase) && top.DurationSeconds >= 3 * 3600 && pending is not null) return $"You have been using {top.ApplicationName.Split('-')[0].Trim()} for {ReportService.FormatDuration(top.DurationSeconds)}. Suggested task: \"{pending.Title}\"";
        if (activity.IdleSeconds >= 45 * 60 && pending is not null) return $"Idle time is high today ({ReportService.FormatDuration(activity.IdleSeconds)}). Suggested focus task: \"{pending.Title}\".";
        if (pending is not null) return $"Next recommended task: \"{pending.Title}\" (Priority: {pending.Priority}, Status: {pending.Status}).";
        return "No pending tasks. Great momentum, consider planning tomorrow's top 3 tasks.";
    }

    private static int ComputeTaskBasedScore(int totalTasks, int goodCount, int neutralCount, int badCount)
    {
        if (totalTasks <= 0) return 0;
        double weighted = (goodCount * 100.0) + (neutralCount * 50.0) + (badCount * 0.0);
        return (int)Math.Round(Math.Clamp(weighted / totalTasks, 0, 100));
    }

    private void SaveTaskButton_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            _vm.StatusText = "Task title is required.";
            return;
        }

        DateTime? due = BuildDueDateTime();
        TaskPriority priority = PriorityComboBox.SelectedItem is TaskPriority p ? p : TaskPriority.Medium;
        ProductivityTracker.App.Models.TaskStatus status = StatusComboBox.SelectedItem is ProductivityTracker.App.Models.TaskStatus s ? s : ProductivityTracker.App.Models.TaskStatus.Pending;

        if (_selectedTaskId is Guid selectedId)
        {
            TaskItem? existingTask = _latestTasks.FirstOrDefault(t => t.Id == selectedId);
            DateTime createdTime = existingTask?.CreatedTime ?? DateTime.Now;
            DateTime? completedTime = existingTask?.CompletedTime;
            if (status == ProductivityTracker.App.Models.TaskStatus.Completed) completedTime ??= DateTime.Now; else completedTime = null;

            _taskService.Update(new TaskItem { Id = selectedId, Title = title, Description = DescriptionTextBox.Text.Trim(), Priority = priority, Status = status, CreatedTime = createdTime, DueTime = due, CompletedTime = completedTime });
            _vm.StatusText = "Task updated.";
            _dueAlertedTaskIds.Remove(selectedId);
        }
        else
        {
            _taskService.Add(new TaskItem { Title = title, Description = DescriptionTextBox.Text.Trim(), Priority = priority, Status = status, CreatedTime = DateTime.Now, DueTime = due, CompletedTime = status == ProductivityTracker.App.Models.TaskStatus.Completed ? DateTime.Now : null });
            _vm.StatusText = "Task created.";
        }

        ClearTaskForm();
    }

    private DateTime? BuildDueDateTime()
    {
        if (DueDatePicker.SelectedDate is null) return null;
        if (!TimeSpan.TryParse(DueTimeTextBox.Text.Trim(), out TimeSpan time)) time = new TimeSpan(18, 0, 0);
        return DueDatePicker.SelectedDate.Value.Date + time;
    }

    private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTaskId is not Guid id)
        {
            _vm.StatusText = "Select a task to delete.";
            return;
        }

        _taskService.Delete(id);
        _dueAlertedTaskIds.Remove(id);
        _vm.StatusText = "Task deleted.";
        ClearTaskForm();
    }

    private void MarkCompletedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTaskId is not Guid id)
        {
            _vm.StatusText = "Select a task to mark complete.";
            return;
        }

        _taskService.MarkCompleted(id);
        _dueAlertedTaskIds.Remove(id);
        _vm.StatusText = "Task marked as completed.";
        ClearTaskForm();
    }

    private void ClearFormButton_Click(object sender, RoutedEventArgs e) => ClearTaskForm();

    private void TasksDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TasksDataGrid.SelectedItem is not TaskItem task) return;

        _selectedTaskId = task.Id;
        TitleTextBox.Text = task.Title;
        DescriptionTextBox.Text = task.Description;
        PriorityComboBox.SelectedItem = task.Priority;
        StatusComboBox.SelectedItem = task.Status;
        DueDatePicker.SelectedDate = task.DueTime?.Date;
        DueTimeTextBox.Text = task.DueTime?.ToString("HH:mm") ?? "18:00";
    }

    private void ClearTaskForm()
    {
        _selectedTaskId = null;
        TasksDataGrid.SelectedItem = null;
        TitleTextBox.Text = string.Empty;
        DescriptionTextBox.Text = string.Empty;
        PriorityComboBox.SelectedItem = TaskPriority.Medium;
        StatusComboBox.SelectedItem = ProductivityTracker.App.Models.TaskStatus.Pending;
        DueDatePicker.SelectedDate = DateTime.Today;
        DueTimeTextBox.Text = "18:00";
    }

    private void NewTaskButton_Click(object sender, RoutedEventArgs e) => OpenAddTask();
    private void RefreshButton_Click(object sender, RoutedEventArgs e) { RefreshDashboard(); _vm.StatusText = "Dashboard refreshed."; }
    private void GenerateReportButton_Click(object sender, RoutedEventArgs e) => GenerateAndOpenReport();
    private void SendReportButton_Click(object sender, RoutedEventArgs e) => SendReport();
    private void ManageReportsButton_Click(object sender, RoutedEventArgs e) => OpenReportManager();
    private void TrendsButton_Click(object sender, RoutedEventArgs e) => OpenTrends();
    private void PauseResumeButton_Click(object sender, RoutedEventArgs e) => ToggleTrackingPause();
    private void BackupButton_Click(object sender, RoutedEventArgs e) => BackupNow();
    private void RestoreButton_Click(object sender, RoutedEventArgs e) => RestoreBackup();

    private void HideToTray()
    {
        ShowInTaskbar = false;
        WindowState = WindowState.Minimized;
        Hide();
        _vm.StatusText = "Dashboard closed. Tracker is running in background (system tray).";
    }
}





