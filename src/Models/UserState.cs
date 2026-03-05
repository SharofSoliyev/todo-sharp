namespace TodoBot.Models;

public class UserState
{
    public long UserId { get; set; }
    public long ChatId { get; set; }
    public BotState State { get; set; } = BotState.Idle;
    public int? EditingTaskId { get; set; }
    public string? TempData { get; set; }
}

public enum BotState
{
    Idle,
    WaitingForTaskTitle,
    WaitingForTaskDescription,
    WaitingForPriority,
    WaitingForDueDate,
    EditingTitle,
    EditingDescription,
    EditingDueDate
}
