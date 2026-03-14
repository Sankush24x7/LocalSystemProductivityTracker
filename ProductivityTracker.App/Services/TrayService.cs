using System.Drawing;
using Forms = System.Windows.Forms;

namespace ProductivityTracker.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _pauseItem;

    public TrayService(
        Action onOpenDashboard,
        Action onQuickAddTask,
        Action onShowShortcuts,
        Action onGenerateReport,
        Action onSendReport,
        Action onOpenReport,
        Action onManageReports,
        Action onOpenTrends,
        Action onBackupNow,
        Action onRestoreBackup,
        Action onCheckUpdate,
        Action onTogglePause,
        Action onLockNow,
        Action onSettings,
        Action onExit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Dashboard", null, (_, _) => onOpenDashboard());
        menu.Items.Add("Quick Add Task", null, (_, _) => onQuickAddTask());
        menu.Items.Add("Show App Shortcuts", null, (_, _) => onShowShortcuts());
        menu.Items.Add("Generate Report", null, (_, _) => onGenerateReport());
        menu.Items.Add("Send Report", null, (_, _) => onSendReport());
        menu.Items.Add("Open Report", null, (_, _) => onOpenReport());
        menu.Items.Add("Manage Reports", null, (_, _) => onManageReports());
        menu.Items.Add("Open Trends", null, (_, _) => onOpenTrends());
        menu.Items.Add("Backup Now", null, (_, _) => onBackupNow());
        menu.Items.Add("Restore Backup", null, (_, _) => onRestoreBackup());
        menu.Items.Add("Check Offline Update", null, (_, _) => onCheckUpdate());

        _pauseItem = new Forms.ToolStripMenuItem("Pause Tracking", null, (_, _) => onTogglePause());
        menu.Items.Add(_pauseItem);

        menu.Items.Add("Lock Now", null, (_, _) => onLockNow());
        menu.Items.Add("Settings", null, (_, _) => onSettings());
        menu.Items.Add("Exit", null, (_, _) => onExit());

        _icon = new Forms.NotifyIcon
        {
            Icon = ResolveTrayIcon(),
            Text = "AI Productivity Tracker",
            Visible = true,
            ContextMenuStrip = menu
        };

        _icon.DoubleClick += (_, _) => onOpenDashboard();
    }

    public void SetPauseState(bool paused)
    {
        _pauseItem.Text = paused ? "Resume Tracking" : "Pause Tracking";
    }

    public void ShowDueAlertBalloon(string taskTitle)
    {
        try
        {
            _icon.BalloonTipTitle = "Task Due Alert";
            _icon.BalloonTipText = $"Task due now: {taskTitle}";
            _icon.BalloonTipIcon = Forms.ToolTipIcon.Warning;
            _icon.ShowBalloonTip(8000);
        }
        catch
        {
            // Keep tracking resilient even if balloon notifications fail.
        }
    }

    public void ShowReminderBalloon(string taskTitle, int minutesLeft)
    {
        try
        {
            _icon.BalloonTipTitle = "Task Reminder";
            _icon.BalloonTipText = $"{taskTitle} is due in {minutesLeft} min.";
            _icon.BalloonTipIcon = Forms.ToolTipIcon.Info;
            _icon.ShowBalloonTip(8000);
        }
        catch
        {
            // Keep tracking resilient even if balloon notifications fail.
        }
    }

    private static Icon ResolveTrayIcon()
    {
        try
        {
            string? path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                Icon? exeIcon = Icon.ExtractAssociatedIcon(path);
                if (exeIcon is not null)
                {
                    return exeIcon;
                }
            }
        }
        catch
        {
            // Fallback below.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
