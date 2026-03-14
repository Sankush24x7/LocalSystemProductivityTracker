using System.Windows;
using ProductivityTracker.App.Models;

namespace ProductivityTracker.App;

public partial class QuickAddTaskWindow : Window
{
    public TaskItem? Result { get; private set; }

    public QuickAddTaskWindow()
    {
        InitializeComponent();
        PriorityComboBox.ItemsSource = Enum.GetValues<TaskPriority>();
        StatusComboBox.ItemsSource = Enum.GetValues<ProductivityTracker.App.Models.TaskStatus>();
        PriorityComboBox.SelectedItem = TaskPriority.Medium;
        StatusComboBox.SelectedItem = ProductivityTracker.App.Models.TaskStatus.Pending;
        DueDatePicker.SelectedDate = DateTime.Today;
        Loaded += (_, _) => TitleTextBox.Focus();
    }

    private void NoDueCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        bool noDue = NoDueCheckBox.IsChecked == true;
        DueDatePicker.IsEnabled = !noDue;
        DueTimeTextBox.IsEnabled = !noDue;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            StatusText.Text = "Title is required.";
            return;
        }

        DateTime? due = null;
        if (NoDueCheckBox.IsChecked != true)
        {
            if (DueDatePicker.SelectedDate is null)
            {
                StatusText.Text = "Please select due date or mark No Due.";
                return;
            }

            if (!TimeSpan.TryParse(DueTimeTextBox.Text.Trim(), out TimeSpan time))
            {
                time = new TimeSpan(18, 0, 0);
            }

            due = DueDatePicker.SelectedDate.Value.Date + time;
        }

        Result = new TaskItem
        {
            Title = title,
            Description = DescriptionTextBox.Text.Trim(),
            Priority = PriorityComboBox.SelectedItem is TaskPriority p ? p : TaskPriority.Medium,
            Status = StatusComboBox.SelectedItem is ProductivityTracker.App.Models.TaskStatus s ? s : ProductivityTracker.App.Models.TaskStatus.Pending,
            CreatedTime = DateTime.Now,
            DueTime = due
        };

        if (Result.Status == ProductivityTracker.App.Models.TaskStatus.Completed)
        {
            Result.CompletedTime = DateTime.Now;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
