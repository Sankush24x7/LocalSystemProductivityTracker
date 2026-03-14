using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using ProductivityTracker.App.Services;

namespace ProductivityTracker.App;

public partial class ReportManagerWindow : Window
{
    private readonly ObservableCollection<ReportDayItem> _items = new();

    public ReportManagerWindow()
    {
        InitializeComponent();
        ReportGrid.ItemsSource = _items;
        LoadItems();
    }

    private void LoadItems()
    {
        _items.Clear();

        var map = new Dictionary<DateOnly, ReportDayItem>();

        foreach (string jsonPath in Directory.EnumerateFiles(LocalPaths.ActivityDirectory, "activity-*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(jsonPath);
            if (!TryParseDate(name.Replace("activity-", string.Empty, StringComparison.Ordinal), out DateOnly date))
            {
                continue;
            }

            map[date] = new ReportDayItem(date)
            {
                JsonPath = jsonPath,
                JsonExists = true,
                JsonName = Path.GetFileName(jsonPath)
            };
        }

        foreach (string htmlPath in Directory.EnumerateFiles(LocalPaths.ReportsDirectory, "productivity-report-*.html"))
        {
            string name = Path.GetFileNameWithoutExtension(htmlPath);
            if (!TryParseDateFromReportName(name, out DateOnly date))
            {
                continue;
            }

            if (!map.TryGetValue(date, out ReportDayItem? existing))
            {
                existing = new ReportDayItem(date);
                map[date] = existing;
            }

            existing.HtmlPath = htmlPath;
            existing.HtmlExists = true;
            existing.HtmlName = Path.GetFileName(htmlPath);
        }

        foreach (ReportDayItem item in map.Values.OrderByDescending(x => x.Date))
        {
            _items.Add(item);
        }

        StatusText.Text = _items.Count == 0 ? "No day-wise JSON/HTML files found." : $"Loaded {_items.Count} day entries.";
    }

    private static bool TryParseDate(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
    }

    private static bool TryParseDateFromReportName(string reportFileNameWithoutExtension, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(reportFileNameWithoutExtension))
        {
            return false;
        }

        Match match = Regex.Match(reportFileNameWithoutExtension, @"(\d{4}-\d{2}-\d{2})$");
        if (!match.Success)
        {
            return false;
        }

        return TryParseDate(match.Groups[1].Value, out date);
    }

    private ReportDayItem? SelectedItem => ReportGrid.SelectedItem as ReportDayItem;

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadItems();

    private void OpenJson_Click(object sender, RoutedEventArgs e)
    {
        ReportDayItem? item = SelectedItem;
        if (item is null || !item.JsonExists || string.IsNullOrWhiteSpace(item.JsonPath))
        {
            StatusText.Text = "Selected day has no JSON file.";
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = item.JsonPath, UseShellExecute = true });
    }

    private void OpenHtml_Click(object sender, RoutedEventArgs e)
    {
        ReportDayItem? item = SelectedItem;
        if (item is null || !item.HtmlExists || string.IsNullOrWhiteSpace(item.HtmlPath))
        {
            StatusText.Text = "Selected day has no HTML report file.";
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = item.HtmlPath, UseShellExecute = true });
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        ReportDayItem? item = SelectedItem;
        if (item is null)
        {
            StatusText.Text = "Select a day row first.";
            return;
        }

        System.Windows.MessageBoxResult confirm = System.Windows.MessageBox.Show(
            $"Delete JSON and HTML files for {item.DateLabel}?",
            "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        int deleted = 0;

        if (item.JsonExists && !string.IsNullOrWhiteSpace(item.JsonPath) && File.Exists(item.JsonPath))
        {
            File.Delete(item.JsonPath);
            deleted++;
        }

        if (item.HtmlExists && !string.IsNullOrWhiteSpace(item.HtmlPath) && File.Exists(item.HtmlPath))
        {
            File.Delete(item.HtmlPath);
            deleted++;
        }

        LoadItems();
        StatusText.Text = deleted > 0
            ? $"Deleted {deleted} file(s) for {item.DateLabel}."
            : "No files were deleted for selected day.";
    }

    private sealed class ReportDayItem
    {
        public ReportDayItem(DateOnly date)
        {
            Date = date;
        }

        public DateOnly Date { get; }
        public string DateLabel => Date.ToString("yyyy-MM-dd");

        public bool JsonExists { get; set; }
        public bool HtmlExists { get; set; }

        public string JsonName { get; set; } = "-";
        public string HtmlName { get; set; } = "-";

        public string? JsonPath { get; set; }
        public string? HtmlPath { get; set; }
    }
}



