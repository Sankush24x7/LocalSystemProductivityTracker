using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Windows.Threading;
using ProductivityTracker.App.Models;

namespace ProductivityTracker.App.Services;

public sealed class ScreenshotService
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;

    public ScreenshotService(AppSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Capture();
        RefreshInterval();
    }

    public void Start()
    {
        if (_settings.ScreenshotEnabled)
        {
            _timer.Start();
        }
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public void RefreshInterval()
    {
        int interval = Math.Clamp(_settings.ScreenshotIntervalMinutes, 1, 120);
        _timer.Interval = TimeSpan.FromMinutes(interval);
    }

    public void Capture()
    {
        if (!_settings.ScreenshotEnabled)
        {
            return;
        }

        try
        {
            var date = DateOnly.FromDateTime(DateTime.Now);
            string folder = LocalPaths.GetScreenshotDayFolder(date);
            Directory.CreateDirectory(folder);

            Rectangle bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1280, 720);
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);

            string file = Path.Combine(folder, $"capture-{DateTime.Now:HHmmss}.png");
            bitmap.Save(file, ImageFormat.Png);
        }
        catch
        {
            // Ignore screenshot failures to keep tracker resilient.
        }
    }
}
