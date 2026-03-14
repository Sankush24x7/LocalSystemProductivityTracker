using System.Text;
using ProductivityTracker.App.Models;

namespace ProductivityTracker.App.Services;

public sealed class ReportService
{
    private readonly StorageService _storage;
    private readonly TaskService _taskService;
    private readonly ActivityTrackerService _activity;

    public ReportService(StorageService storage, TaskService taskService, ActivityTrackerService activity)
    {
        _storage = storage;
        _taskService = taskService;
        _activity = activity;
    }

    public string GenerateDailyReport()
    {
        DailyActivityData today = _activity.Snapshot();
        var summary = _taskService.GetSummary();

        double productivityScore = CalculateProductivityScore(today, summary.Completed, summary.Total);
        List<AppUsageEntry> topApps = today.ApplicationUsage
            .OrderByDescending(a => a.DurationSeconds)
            .Take(6)
            .ToList();

        List<DailyActivityData> weekData = _storage.LoadRecentActivity(7);

        string html = BuildHtml(today, summary, productivityScore, topApps, weekData);
        string dailyReportPath = LocalPaths.GetDailyReportFile(today.Date);
        File.WriteAllText(dailyReportPath, html);
        return dailyReportPath;
    }

    private static double CalculateProductivityScore(DailyActivityData data, int completedTasks, int totalTasks)
    {
        if (data.ActiveSeconds <= 0)
        {
            return 0;
        }

        double activeProductive = (data.CodingSeconds + (0.4 * data.BrowsingSeconds));
        double baseScore = (activeProductive / data.ActiveSeconds) * 100;
        double taskFactor = totalTasks == 0 ? 0 : (double)completedTasks / totalTasks * 20;
        return Math.Clamp(baseScore + taskFactor, 0, 100);
    }

    private static string BuildHtml(
        DailyActivityData activity,
        (int Total, int Completed, int Pending, int Overdue, int CreatedToday) summary,
        double score,
        List<AppUsageEntry> topApps,
        List<DailyActivityData> weekData)
    {
        int total = Math.Max(1, topApps.Sum(a => a.DurationSeconds));
        StringBuilder bars = new();
        foreach (AppUsageEntry app in topApps)
        {
            int pct = (int)Math.Round((double)app.DurationSeconds / total * 100);
            bars.Append($"<div class='bar-row'><span>{Escape(app.ApplicationName)}</span><div class='bar'><div style='width:{pct}%'></div></div><em>{FormatDuration(app.DurationSeconds)}</em></div>");
        }

        int coding = Math.Max(1, activity.CodingSeconds);
        int browsing = Math.Max(1, activity.BrowsingSeconds);
        int other = Math.Max(1, activity.OtherSeconds);
        int idle = Math.Max(1, activity.IdleSeconds);
        int totalPie = coding + browsing + other + idle;

        int codingPct = coding * 100 / totalPie;
        int browsingPct = browsing * 100 / totalPie;
        int otherPct = other * 100 / totalPie;

        // Newest first (DESC) and paginated in HTML via JavaScript.
        StringBuilder timeline = new();
        foreach (TimelineEntry item in activity.Timeline
                     .OrderByDescending(t => t.Timestamp)
                     .Take(3000))
        {
            timeline.Append($"<tr><td>{item.Timestamp:yyyy-MM-dd HH:mm:ss}</td><td>{Escape(item.ActivityName)}</td></tr>");
        }

        StringBuilder weekBars = new();
        foreach (DailyActivityData day in weekData)
        {
            double dayScore = day.ActiveSeconds == 0
                ? 0
                : Math.Clamp(((day.CodingSeconds + 0.4 * day.BrowsingSeconds) / day.ActiveSeconds) * 100, 0, 100);
            weekBars.Append($"<div class='week-item'><span>{day.Date:MM-dd}</span><div class='week-bar'><div style='height:{(int)Math.Round(dayScore)}%'></div></div><em>{dayScore:0}%</em></div>");
        }

        return $$"""
<!doctype html>
<html>
<head>
<meta charset='utf-8'>
<title>Daily Productivity Report</title>
<style>
body { font-family: Segoe UI, sans-serif; background:#0f141d; color:#e8edf5; margin:24px; }
.card { background:#171f2c; border-radius:14px; padding:18px; margin-bottom:16px; border:1px solid #26344a; }
h1,h2 { margin:0 0 10px 0; }
.grid { display:grid; grid-template-columns: repeat(3, minmax(160px,1fr)); gap:10px; }
.metric { background:#111826; padding:10px; border-radius:10px; }
.bar-row { display:grid; grid-template-columns: 2fr 4fr auto; align-items:center; gap:12px; margin:8px 0; }
.bar { height:10px; background:#223049; border-radius:999px; overflow:hidden; }
.bar div { background:#4ecdc4; height:100%; }
.pie { width:180px; height:180px; border-radius:50%; background: conic-gradient(#2ecc71 0 {{codingPct}}%, #f39c12 {{codingPct}}% {{codingPct + browsingPct}}%, #3498db {{codingPct + browsingPct}}% {{codingPct + browsingPct + otherPct}}%, #e74c3c {{codingPct + browsingPct + otherPct}}% 100%); }
.row { display:flex; gap:20px; align-items:center; }
.legend div { margin-bottom:6px; }
table { width:100%; border-collapse:collapse; }
td,th { border-bottom:1px solid #26344a; padding:8px; text-align:left; }
.week-wrap { display:flex; gap:10px; align-items:flex-end; min-height:160px; }
.week-item { width:60px; text-align:center; }
.week-bar { height:120px; border-radius:8px; background:#223049; display:flex; align-items:flex-end; overflow:hidden; }
.week-bar div { width:100%; background:#5dade2; }
.pager { display:flex; align-items:center; gap:10px; margin-top:10px; }
.pager button { background:#223049; color:#e8edf5; border:1px solid #334866; border-radius:8px; padding:6px 10px; cursor:pointer; }
.pager button:disabled { opacity:0.4; cursor:not-allowed; }
</style>
</head>
<body>
<div class='card'>
<h1>Daily Productivity Report - {{activity.Date:yyyy-MM-dd}}</h1>
<div class='grid'>
<div class='metric'><strong>Productivity Score</strong><div>{{score:0}}%</div></div>
<div class='metric'><strong>Total Active Time</strong><div>{{FormatDuration(activity.ActiveSeconds)}}</div></div>
<div class='metric'><strong>Total Idle Time</strong><div>{{FormatDuration(activity.IdleSeconds)}}</div></div>
<div class='metric'><strong>Tasks Completed</strong><div>{{summary.Completed}}</div></div>
<div class='metric'><strong>Pending Tasks</strong><div>{{summary.Pending}}</div></div>
<div class='metric'><strong>Overdue Tasks</strong><div>{{summary.Overdue}}</div></div>
<div class='metric'><strong>Activity Samples</strong><div>{{activity.Timeline.Count}}</div></div>
</div>
</div>

<div class='card'>
<h2>Application Usage</h2>
{{bars}}
</div>

<div class='card'>
<h2>Top Usage Mix</h2>
<div class='row'>
<div class='pie'></div>
<div class='legend'>
<div>Coding: {{FormatDuration(activity.CodingSeconds)}}</div>
<div>Browsing: {{FormatDuration(activity.BrowsingSeconds)}}</div>
<div>Other: {{FormatDuration(activity.OtherSeconds)}}</div>
<div>Idle: {{FormatDuration(activity.IdleSeconds)}}</div>
</div>
</div>
</div>

<div class='card'>
<h2>Activity Timeline (5-second activity samples, newest first)</h2>
<table>
<thead><tr><th>Timestamp</th><th>Activity</th></tr></thead>
<tbody id='timelineBody'>{{timeline}}</tbody>
</table>
<div class='pager'>
<button id='prevBtn' onclick='prevPage()'>Prev</button>
<span id='pageInfo'></span>
<button id='nextBtn' onclick='nextPage()'>Next</button>
</div>
</div>

<div class='card'>
<h2>Weekly Productivity Chart</h2>
<div class='week-wrap'>{{weekBars}}</div>
</div>

<div class='card'>
<h2>Idle Time Summary</h2>
<div>Total Idle Time: {{FormatDuration(activity.IdleSeconds)}} | Idle Sessions: {{activity.IdleSessions.Count}}</div>
</div>

<div class='card'>
<h2>Task Summary</h2>
<div>Tasks Created Today: {{summary.CreatedToday}}</div>
<div>Tasks Completed: {{summary.Completed}}</div>
<div>Pending Tasks: {{summary.Pending}}</div>
</div>

<script>
const pageSize = 30;
let page = 1;
const tbody = document.getElementById('timelineBody');
const rows = Array.from(tbody.querySelectorAll('tr'));
const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));

function renderPage() {
  const start = (page - 1) * pageSize;
  const end = start + pageSize;
  rows.forEach((row, idx) => {
    row.style.display = (idx >= start && idx < end) ? '' : 'none';
  });

  document.getElementById('pageInfo').textContent = `Page ${page} / ${totalPages} (${rows.length} rows)`;
  document.getElementById('prevBtn').disabled = page <= 1;
  document.getElementById('nextBtn').disabled = page >= totalPages;
}

function prevPage() {
  if (page > 1) {
    page--;
    renderPage();
  }
}

function nextPage() {
  if (page < totalPages) {
    page++;
    renderPage();
  }
}

renderPage();
</script>
</body>
</html>
""";
    }

    private static string Escape(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    public static string FormatDuration(int totalSeconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }

        return $"{span.Minutes}m {span.Seconds}s";
    }
}


