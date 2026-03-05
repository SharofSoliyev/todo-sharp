namespace TodoBot.Models;

public class TodoItem
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public long CreatedByUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public Priority Priority { get; set; } = Priority.Normal;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FileId { get; set; }
    public string? FileType { get; set; }
    public bool IsForwarded { get; set; }
    public string? ForwardedFrom { get; set; }
    public DateTime? TimerStartedAt { get; set; }
    public long TimeSpentSeconds { get; set; }
}

public enum Priority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}
