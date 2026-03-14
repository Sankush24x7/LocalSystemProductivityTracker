using System.Windows;
using ProductivityTracker.App.Models;

namespace ProductivityTracker.App;

public partial class DueAlertWindow : Window
{
    public DueAlertWindow(TaskItem task)
    {
        InitializeComponent();
        TaskTitleText.Text = task.Title;
        TaskDetailText.Text = string.IsNullOrWhiteSpace(task.Description) ? "No description" : task.Description;
        DueText.Text = task.DueTime is null
            ? "No due time"
            : $"Due: {task.DueTime:dd-MMM-yyyy hh:mm tt}  |  {task.TatDisplay}";
    }

    private void Acknowledge_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
