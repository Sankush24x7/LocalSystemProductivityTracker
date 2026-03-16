using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using ProductivityTracker.App.Models;
using ProductivityTracker.App.Services;

namespace ProductivityTracker.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;

    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings settings)
    {
        _original = settings;
        InitializeComponent();

        ScreenshotEnabledCheckBox.IsChecked = settings.ScreenshotEnabled;
        ScreenshotIntervalTextBox.Text = settings.ScreenshotIntervalMinutes.ToString();
        IdleThresholdTextBox.Text = settings.IdleThresholdMinutes.ToString();

        DueSoundEnabledCheckBox.IsChecked = settings.DueSoundEnabled;
        DueTrayBalloonEnabledCheckBox.IsChecked = settings.DueTrayBalloonEnabled;
        Reminder30CheckBox.IsChecked = settings.DueReminder30MinEnabled;
        Reminder15CheckBox.IsChecked = settings.DueReminder15MinEnabled;
        Reminder5CheckBox.IsChecked = settings.DueReminder5MinEnabled;

        DailyGoalHoursTextBox.Text = settings.DailyActiveGoalHours.ToString();
        DailyTaskGoalTextBox.Text = settings.DailyTaskGoal.ToString();
        CategoryRulesTextBox.Text = settings.AppCategoryRulesText;

        RetentionEnabledCheckBox.IsChecked = settings.RetentionEnabled;
        RetentionActivityTextBox.Text = settings.RetentionActivityDays.ToString();
        RetentionScreenshotsTextBox.Text = settings.RetentionScreenshotsDays.ToString();
        RetentionReportsTextBox.Text = settings.RetentionReportsDays.ToString();

        PinEnabledCheckBox.IsChecked = settings.PinLockEnabled;
        OfflineUpdateCheckBox.IsChecked = settings.OfflineUpdateCheckEnabled;
        OfflineUpdatePathTextBox.Text = settings.OfflineUpdatePath;

        MailFromTextBox.Text = settings.MailFrom;
        MailToTextBox.Text = settings.MailTo;
        MailPasswordBox.Password = settings.MailPassword;
        SmtpHostTextBox.Text = settings.SmtpHost;
        SmtpPortTextBox.Text = settings.SmtpPort.ToString();
        SmtpSslCheckBox.IsChecked = settings.SmtpUseSsl;

        BackupPasswordBox.Password = settings.BackupPassword;
        BackgroundStartTimeTextBox.Text = NormalizeTimeText(settings.BackgroundStartTime, "09:00 AM");
        BackgroundStopTimeTextBox.Text = NormalizeTimeText(settings.BackgroundStopTime, "07:00 PM");

        SelectDueInterval(settings.DueCheckIntervalSeconds);
        SelectActivityInterval(settings.ActivityTrackingIntervalSeconds);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        int screenshotInterval = ParseOrDefault(ScreenshotIntervalTextBox.Text, 10, 1, 120);
        int idle = ParseOrDefault(IdleThresholdTextBox.Text, 3, 1, 30);
        int dueInterval = ParseOrDefault(GetSelectedComboValue(DueCheckIntervalComboBox), 30, 10, 60);
        int activityInterval = ParseOrDefault(GetSelectedComboValue(ActivityIntervalComboBox), 5, 1, 60);

        if (dueInterval is not (10 or 30 or 60))
        {
            dueInterval = 30;
        }

        if (activityInterval is not (1 or 2 or 5 or 10 or 30 or 60))
        {
            activityInterval = 5;
        }

        int goalHours = ParseOrDefault(DailyGoalHoursTextBox.Text, 6, 1, 24);
        int goalTasks = ParseOrDefault(DailyTaskGoalTextBox.Text, 5, 1, 200);

        int retentionActivity = ParseOrDefault(RetentionActivityTextBox.Text, 30, 1, 3650);
        int retentionShots = ParseOrDefault(RetentionScreenshotsTextBox.Text, 14, 1, 3650);
        int retentionReports = ParseOrDefault(RetentionReportsTextBox.Text, 30, 1, 3650);

        int smtpPort = ParseOrDefault(SmtpPortTextBox.Text, 587, 1, 65535);

        string pinHash = _original.PinHash;
        string enteredPin = PinPasswordBox.Password.Trim();
        if (!string.IsNullOrWhiteSpace(enteredPin))
        {
            pinHash = PinSecurity.HashPin(enteredPin);
        }

        Result = new AppSettings
        {
            ScreenshotEnabled = ScreenshotEnabledCheckBox.IsChecked == true,
            ScreenshotIntervalMinutes = screenshotInterval,
            IdleThresholdMinutes = idle,
            DueSoundEnabled = DueSoundEnabledCheckBox.IsChecked == true,
            DueTrayBalloonEnabled = DueTrayBalloonEnabledCheckBox.IsChecked == true,
            DueReminder30MinEnabled = Reminder30CheckBox.IsChecked == true,
            DueReminder15MinEnabled = Reminder15CheckBox.IsChecked == true,
            DueReminder5MinEnabled = Reminder5CheckBox.IsChecked == true,
            DueCheckIntervalSeconds = dueInterval,
            ActivityTrackingIntervalSeconds = activityInterval,
            TrackingPaused = _original.TrackingPaused,
            BackgroundStartTime = NormalizeTimeText(BackgroundStartTimeTextBox.Text, "09:00 AM"),
            BackgroundStopTime = NormalizeTimeText(BackgroundStopTimeTextBox.Text, "07:00 PM"),
            AppCategoryRulesText = CategoryRulesTextBox.Text,
            RetentionEnabled = RetentionEnabledCheckBox.IsChecked == true,
            RetentionActivityDays = retentionActivity,
            RetentionScreenshotsDays = retentionShots,
            RetentionReportsDays = retentionReports,
            PinLockEnabled = PinEnabledCheckBox.IsChecked == true,
            PinHash = pinHash,
            DailyActiveGoalHours = goalHours,
            DailyTaskGoal = goalTasks,
            OfflineUpdateCheckEnabled = OfflineUpdateCheckBox.IsChecked == true,
            OfflineUpdatePath = OfflineUpdatePathTextBox.Text.Trim(),
            BackupPassword = BackupPasswordBox.Password,
            MailFrom = MailFromTextBox.Text.Trim(),
            MailTo = MailToTextBox.Text.Trim(),
            MailPassword = MailPasswordBox.Password,
            SmtpHost = SmtpHostTextBox.Text.Trim(),
            SmtpPort = smtpPort,
            SmtpUseSsl = SmtpSslCheckBox.IsChecked == true
        };

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void SelectDueInterval(int value)
    {
        int normalized = value is 10 or 30 or 60 ? value : 30;
        SelectComboByValue(DueCheckIntervalComboBox, normalized.ToString(), 1);
    }

    private void SelectActivityInterval(int value)
    {
        int normalized = value is 1 or 2 or 5 or 10 or 30 or 60 ? value : 5;
        SelectComboByValue(ActivityIntervalComboBox, normalized.ToString(), 2);
    }

    private static void SelectComboByValue(System.Windows.Controls.ComboBox combo, string value, int fallbackIndex)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Content?.ToString() == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }

        combo.SelectedIndex = fallbackIndex;
    }

    private static string GetSelectedComboValue(System.Windows.Controls.ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item && item.Content is not null)
        {
            return item.Content.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string NormalizeTimeText(string? input, string fallback)
    {
        if (!TryParseTime(input, out DateTime value))
        {
            TryParseTime(fallback, out value);
        }

        return value.ToString("hh:mm tt", CultureInfo.InvariantCulture);
    }

    private static bool TryParseTime(string? input, out DateTime value)
    {
        string text = (input ?? string.Empty).Trim();
        if (DateTime.TryParseExact(text, new[] { "h:mm tt", "hh:mm tt", "H:mm", "HH:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    private static int ParseOrDefault(string? input, int fallback, int min, int max)
    {
        if (!int.TryParse(input, out int value))
        {
            value = fallback;
        }

        return Math.Clamp(value, min, max);
    }
}
