using System.Text.Json.Serialization;

namespace ProductivityTracker.App.Models;

public enum TaskPriority
{
    Low,
    Medium,
    High
}

public enum TaskStatus
{
    Pending,
    Running,
    Completed
}

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime? DueTime { get; set; }
    public DateTime? CompletedTime { get; set; }

    [JsonIgnore]
    public bool IsCompleted => Status == TaskStatus.Completed;

    [JsonIgnore]
    public bool IsRunning => Status == TaskStatus.Running;

    [JsonIgnore]
    public bool IsPending => Status == TaskStatus.Pending;

    [JsonIgnore]
    public bool IsOverdue => DueTime is not null && DueTime <= DateTime.Now && Status != TaskStatus.Completed;

    [JsonIgnore]
    public bool IsCompletedOnTime =>
        Status == TaskStatus.Completed && DueTime is not null && CompletedTime is not null && CompletedTime <= DueTime;

    [JsonIgnore]
    public bool IsCompletedLate =>
        Status == TaskStatus.Completed && DueTime is not null && CompletedTime is not null && CompletedTime > DueTime;

    [JsonIgnore]
    public string TatDisplay => DueTime is null ? "-" : DueTime.Value.ToString("dd-MMM-yyyy hh:mm tt");
}
