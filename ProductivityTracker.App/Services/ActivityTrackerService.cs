using System.Diagnostics;
using System.Text;
using System.Windows.Threading;
using ProductivityTracker.App.Helpers;
using ProductivityTracker.App.Models;

namespace ProductivityTracker.App.Services;

public sealed class ActivityTrackerService
{
    private readonly StorageService _storage;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;
    private readonly object _sync = new();

    private DailyActivityData _currentDay;
    private DateTime _segmentStart;
    private string _lastActivity = "Unknown";
    private bool _lastWasIdle;
    private IdleSession? _activeIdleSession;

    private static readonly HashSet<string> CodingApps =
    [
        "devenv", "code", "idea64", "pycharm64", "rider64", "cmd", "powershell", "pwsh", "windowsterminal", "ssms"
    ];

    private static readonly HashSet<string> BrowsingApps = ["chrome", "msedge", "firefox", "brave"];

    private List<(string Token, string Category)> _customRules = new();

    public ActivityTrackerService(StorageService storage, AppSettings settings)
    {
        _storage = storage;
        _settings = settings;
        _currentDay = _storage.LoadActivity(DateOnly.FromDateTime(DateTime.Today));
        _segmentStart = DateTime.Now;

        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => TrackTick();

        RefreshRules();
        RefreshSamplingInterval();
    }

    public event Action? ActivityUpdated;

    public bool IsPaused => _settings.TrackingPaused;

    public DailyActivityData Snapshot()
    {
        lock (_sync)
        {
            return Clone(_currentDay);
        }
    }

    public void Start() => _timer.Start();

    public void Stop()
    {
        _timer.Stop();
        FlushCurrentSegment(DateTime.Now);
        Persist();
    }

    public void RefreshSamplingInterval()
    {
        int seconds = NormalizeSamplingSeconds(_settings.ActivityTrackingIntervalSeconds);
        _settings.ActivityTrackingIntervalSeconds = seconds;
        _timer.Interval = TimeSpan.FromSeconds(seconds);
    }

    public void RefreshRules()
    {
        _customRules = ParseRules(_settings.AppCategoryRulesText);
    }

    public void SetPaused(bool paused)
    {
        DateTime now = DateTime.Now;
        FlushCurrentSegment(now);
        _settings.TrackingPaused = paused;
        _segmentStart = now;
        _lastActivity = paused ? "Paused" : _lastActivity;
        _lastWasIdle = false;
        Persist();
        ActivityUpdated?.Invoke();
    }

    private static int NormalizeSamplingSeconds(int value)
    {
        return value switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            <= 10 => 10,
            <= 30 => 30,
            _ => 60
        };
    }

    private void TrackTick()
    {
        DateTime now = DateTime.Now;
        EnsureDay(now);

        if (_settings.TrackingPaused)
        {
            _segmentStart = now;
            _lastActivity = "Paused";
            _lastWasIdle = false;
            ActivityUpdated?.Invoke();
            return;
        }

        (bool isIdle, int idleSeconds) = GetIdleState();
        string activity = isIdle ? "Idle" : GetActiveApplicationLabel();

        FlushCurrentSegment(now);

        _currentDay.Timeline.Add(new TimelineEntry
        {
            Timestamp = now,
            ActivityName = activity
        });

        if (_currentDay.Timeline.Count > 20000)
        {
            _currentDay.Timeline.RemoveRange(0, _currentDay.Timeline.Count - 20000);
        }

        if (isIdle)
        {
            if (_activeIdleSession is null)
            {
                _activeIdleSession = new IdleSession { Start = now.AddSeconds(-Math.Min(idleSeconds, _timer.Interval.TotalSeconds)), End = now };
                _currentDay.IdleSessions.Add(_activeIdleSession);
            }
            else
            {
                _activeIdleSession.End = now;
            }
        }
        else
        {
            _activeIdleSession = null;
        }

        _segmentStart = now;
        _lastActivity = activity;
        _lastWasIdle = isIdle;

        Persist();
        ActivityUpdated?.Invoke();
    }

    private void EnsureDay(DateTime now)
    {
        DateOnly currentDate = DateOnly.FromDateTime(now);
        if (_currentDay.Date == currentDate)
        {
            return;
        }

        FlushCurrentSegment(now);
        Persist();

        _currentDay = _storage.LoadActivity(currentDate);
        _segmentStart = now;
        _lastActivity = "Unknown";
        _lastWasIdle = false;
        _activeIdleSession = null;
    }

    private void FlushCurrentSegment(DateTime now)
    {
        int seconds = (int)Math.Max(0, (now - _segmentStart).TotalSeconds);
        if (seconds <= 0)
        {
            return;
        }

        if (_settings.TrackingPaused)
        {
            return;
        }

        if (_lastWasIdle)
        {
            _currentDay.IdleSeconds += seconds;
            return;
        }

        _currentDay.ActiveSeconds += seconds;

        if (string.IsNullOrWhiteSpace(_lastActivity) || _lastActivity.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            _currentDay.OtherSeconds += seconds;
            return;
        }

        AppUsageEntry? existing = _currentDay.ApplicationUsage.FirstOrDefault(a => a.ApplicationName == _lastActivity);
        if (existing is null)
        {
            _currentDay.ApplicationUsage.Add(new AppUsageEntry { ApplicationName = _lastActivity, DurationSeconds = seconds });
        }
        else
        {
            existing.DurationSeconds += seconds;
        }

        Categorize(_lastActivity, seconds);
    }

    private void Categorize(string activity, int seconds)
    {
        string normalized = activity.ToLowerInvariant();

        foreach ((string token, string category) in _customRules)
        {
            if (!normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ApplyCategory(category, seconds);
            return;
        }

        if (CodingApps.Any(normalized.Contains))
        {
            _currentDay.CodingSeconds += seconds;
            return;
        }

        if (BrowsingApps.Any(normalized.Contains))
        {
            _currentDay.BrowsingSeconds += seconds;
            return;
        }

        _currentDay.OtherSeconds += seconds;
    }

    private void ApplyCategory(string category, int seconds)
    {
        if (category.Equals("coding", StringComparison.OrdinalIgnoreCase))
        {
            _currentDay.CodingSeconds += seconds;
            return;
        }

        if (category.Equals("browsing", StringComparison.OrdinalIgnoreCase))
        {
            _currentDay.BrowsingSeconds += seconds;
            return;
        }

        _currentDay.OtherSeconds += seconds;
    }

    private static List<(string Token, string Category)> ParseRules(string text)
    {
        var rules = new List<(string Token, string Category)>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return rules;
        }

        string[] lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int idx = line.IndexOf('=');
            if (idx <= 0 || idx >= line.Length - 1)
            {
                continue;
            }

            string token = line[..idx].Trim().ToLowerInvariant();
            string category = line[(idx + 1)..].Trim();
            if (token.Length == 0)
            {
                continue;
            }

            rules.Add((token, category));
        }

        return rules;
    }

    private (bool IsIdle, int IdleSeconds) GetIdleState()
    {
        var info = new NativeMethods.LastInputInfo
        {
            CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.LastInputInfo>()
        };

        if (!NativeMethods.GetLastInputInfo(ref info))
        {
            return (false, 0);
        }

        int millis = Environment.TickCount - unchecked((int)info.DwTime);
        int idleSeconds = Math.Max(0, millis / 1000);
        int threshold = Math.Max(1, _settings.IdleThresholdMinutes) * 60;
        return (idleSeconds >= threshold, idleSeconds);
    }

    private static string GetActiveApplicationLabel()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return "Unknown";
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        string processName = "Unknown";

        try
        {
            using Process process = Process.GetProcessById((int)pid);
            processName = process.ProcessName;
        }
        catch
        {
            // Keep fallback process name.
        }

        var titleBuilder = new StringBuilder(512);
        NativeMethods.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
        string title = titleBuilder.ToString();
        return string.IsNullOrWhiteSpace(title) ? processName : $"{processName} - {title}";
    }

    private void Persist()
    {
        lock (_sync)
        {
            _storage.SaveActivity(_currentDay);
        }
    }

    private static DailyActivityData Clone(DailyActivityData source)
    {
        return new DailyActivityData
        {
            Date = source.Date,
            ActiveSeconds = source.ActiveSeconds,
            IdleSeconds = source.IdleSeconds,
            CodingSeconds = source.CodingSeconds,
            BrowsingSeconds = source.BrowsingSeconds,
            OtherSeconds = source.OtherSeconds,
            ApplicationUsage = source.ApplicationUsage.Select(a => new AppUsageEntry
            {
                ApplicationName = a.ApplicationName,
                DurationSeconds = a.DurationSeconds
            }).ToList(),
            Timeline = source.Timeline.Select(t => new TimelineEntry
            {
                Timestamp = t.Timestamp,
                ActivityName = t.ActivityName
            }).ToList(),
            IdleSessions = source.IdleSessions.Select(i => new IdleSession
            {
                Start = i.Start,
                End = i.End
            }).ToList()
        };
    }
}
