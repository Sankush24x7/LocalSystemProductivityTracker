using ProductivityTracker.App.Models;

namespace ProductivityTracker.App.Services;

public sealed class TaskService
{
    private readonly StorageService _storage;
    private readonly List<TaskItem> _tasks;

    public TaskService(StorageService storage)
    {
        _storage = storage;
        _tasks = _storage.LoadTasks();
    }

    public event Action? TasksChanged;

    public IReadOnlyList<TaskItem> GetAll() => _tasks.OrderByDescending(t => t.CreatedTime).ToList();

    public void ReloadFromStorage()
    {
        _tasks.Clear();
        _tasks.AddRange(_storage.LoadTasks());
        TasksChanged?.Invoke();
    }

    public void Add(TaskItem task)
    {
        _tasks.Add(task);
        Persist();
    }

    public void Update(TaskItem task)
    {
        int index = _tasks.FindIndex(t => t.Id == task.Id);
        if (index >= 0)
        {
            _tasks[index] = task;
            Persist();
        }
    }

    public void Delete(Guid id)
    {
        _tasks.RemoveAll(t => t.Id == id);
        Persist();
    }

    public void MarkCompleted(Guid id)
    {
        TaskItem? task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task is null)
        {
            return;
        }

        task.Status = ProductivityTracker.App.Models.TaskStatus.Completed;
        task.CompletedTime ??= DateTime.Now;
        Persist();
    }

    public (int Total, int Completed, int Pending, int Overdue, int CreatedToday) GetSummary()
    {
        int total = _tasks.Count;
        int completed = _tasks.Count(t => t.Status == ProductivityTracker.App.Models.TaskStatus.Completed);
        int pending = _tasks.Count(t => t.Status != ProductivityTracker.App.Models.TaskStatus.Completed);
        int overdue = _tasks.Count(t => t.Status != ProductivityTracker.App.Models.TaskStatus.Completed && t.DueTime is not null && t.DueTime < DateTime.Now);
        int createdToday = _tasks.Count(t => t.CreatedTime.Date == DateTime.Today);

        return (total, completed, pending, overdue, createdToday);
    }

    public (int Points, int GoodCount, int NeutralCount, int BadCount) GetPointSummary()
    {
        int good = 0;
        int neutral = 0;
        int bad = 0;

        foreach (TaskItem task in _tasks)
        {
            if (task.IsCompletedOnTime)
            {
                good++;
                continue;
            }

            bool overdueOpen = task.Status != ProductivityTracker.App.Models.TaskStatus.Completed && task.DueTime is not null && task.DueTime < DateTime.Now;
            if (overdueOpen || task.IsCompletedLate)
            {
                bad++;
                continue;
            }

            neutral++;
        }

        int points = (good * 10) + (neutral * 3) - (bad * 8);
        return (points, good, neutral, bad);
    }

    public TaskItem? GetFirstPending()
    {
        return _tasks
            .Where(t => t.Status != ProductivityTracker.App.Models.TaskStatus.Completed)
            .OrderBy(t => t.DueTime ?? DateTime.MaxValue)
            .FirstOrDefault();
    }

    private void Persist()
    {
        _storage.SaveTasks(_tasks);
        TasksChanged?.Invoke();
    }
}
