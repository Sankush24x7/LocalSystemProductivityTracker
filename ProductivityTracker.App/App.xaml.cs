using System.Threading.Tasks;
using System.Windows;
using ProductivityTracker.App.Models;
using ProductivityTracker.App.Services;

namespace ProductivityTracker.App;

public partial class App : System.Windows.Application
{
    private ActivityTrackerService? _activity;
    private ScreenshotService? _screenshots;

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var storage = new StorageService();
            AppSettings settings = storage.LoadSettings();

            var startup = new StartupService();
            _ = startup.EnsureAutoStartWithHealth();

            var tasks = new TaskService(storage);
            _activity = new ActivityTrackerService(storage, settings);
            var report = new ReportService(storage, tasks, _activity);
            _screenshots = new ScreenshotService(settings);

            var mainWindow = new MainWindow(tasks, _activity, report, storage, _screenshots, settings);
            MainWindow = mainWindow;

            _activity.Start();
            _screenshots.Start();

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            string logPath = ErrorLogger.LogException("OnStartup", ex);
            System.Windows.MessageBox.Show(
                $"Productivity Tracker failed to start.\n\nError log:\n{logPath}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _screenshots?.Stop();
        _activity?.Stop();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        string logPath = ErrorLogger.LogException("DispatcherUnhandledException", e.Exception);
        System.Windows.MessageBox.Show(
            $"Unexpected error occurred.\n\nError log:\n{logPath}",
            "Application Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(-1);
    }

    private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Exception ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception object.");
        ErrorLogger.LogException("CurrentDomain.UnhandledException", ex);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ErrorLogger.LogException("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}
