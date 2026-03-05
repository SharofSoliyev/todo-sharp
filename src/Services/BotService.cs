using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TodoBot.Data;
using TodoBot.Models;

namespace TodoBot.Services;

public class BotService
{
    private readonly ITelegramBotClient _bot;
    private readonly ConcurrentDictionary<string, UserState> _states = new();

    public BotService(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    private string StateKey(long userId, long chatId) => $"{userId}_{chatId}";

    private UserState GetState(long userId, long chatId)
    {
        var key = StateKey(userId, chatId);
        return _states.GetOrAdd(key, _ => new UserState
        {
            UserId = userId,
            ChatId = chatId
        });
    }

    private void ResetState(long userId, long chatId)
    {
        var key = StateKey(userId, chatId);
        _states.TryRemove(key, out _);
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken ct)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    if (update.Message is { } msg)
                        await HandleMessage(msg, ct);
                    break;
                case UpdateType.CallbackQuery:
                    if (update.CallbackQuery is { } cb)
                        await HandleCallback(cb, ct);
                    break;
                case UpdateType.ChannelPost:
                    if (update.ChannelPost is { } chPost)
                        await HandleMessage(chPost, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private async Task HandleMessage(Message msg, CancellationToken ct)
    {
        var chatId = msg.Chat.Id;
        var userId = msg.From?.Id ?? chatId;

        // Handle forwarded messages — auto-add as task
        if (msg.ForwardOrigin is not null)
        {
            await HandleForwardedMessage(chatId, userId, msg, ct);
            return;
        }

        // Handle media without text (audio, voice, video, document, photo) — auto-add as task
        if (msg.Text is null && HasMedia(msg))
        {
            await HandleMediaMessage(chatId, userId, msg, ct);
            return;
        }

        if (msg.Text is not { } text) return;

        // Handle commands
        if (text.StartsWith('/'))
        {
            var command = text.Split(' ')[0].Split('@')[0].ToLower();
            switch (command)
            {
                case "/start":
                    await SendMainMenu(chatId, ct);
                    return;
                case "/menu":
                    await SendMainMenu(chatId, ct);
                    return;
                case "/add":
                    var titlePart = text.Length > 5 ? text[5..].Trim() : null;
                    if (!string.IsNullOrEmpty(titlePart))
                    {
                        await QuickAddTask(chatId, userId, titlePart, ct);
                    }
                    else
                    {
                        var state = GetState(userId, chatId);
                        state.State = BotState.WaitingForTaskTitle;
                        await _bot.SendMessage(chatId,
                            "📝 *Vazifa sarlavhasini yozing:*",
                            parseMode: ParseMode.Markdown,
                            cancellationToken: ct);
                    }
                    return;
                case "/list":
                    await SendTaskList(chatId, false, ct);
                    return;
                case "/done":
                    await SendTaskList(chatId, true, ct);
                    return;
                case "/stats":
                    await SendStats(chatId, ct);
                    return;
                case "/help":
                    await SendHelp(chatId, ct);
                    return;
            }
        }

        // Handle state-based input
        var currentState = GetState(userId, chatId);
        switch (currentState.State)
        {
            case BotState.WaitingForTaskTitle:
                await HandleTaskTitle(chatId, userId, text, ct);
                break;
            case BotState.WaitingForTaskDescription:
                await HandleTaskDescription(chatId, userId, text, ct);
                break;
            case BotState.WaitingForDueDate:
                await HandleDueDate(chatId, userId, text, ct);
                break;
            case BotState.EditingTitle:
                await HandleEditTitle(chatId, userId, text, ct);
                break;
            case BotState.EditingDescription:
                await HandleEditDescription(chatId, userId, text, ct);
                break;
            case BotState.EditingDueDate:
                await HandleEditDueDate(chatId, userId, text, ct);
                break;
        }
    }

    private static bool HasMedia(Message msg)
    {
        return msg.Audio is not null || msg.Voice is not null || msg.Video is not null
            || msg.VideoNote is not null || msg.Document is not null || msg.Photo is not null;
    }

    private static (string? fileId, string fileType) ExtractMedia(Message msg)
    {
        if (msg.Audio is { } audio)
            return (audio.FileId, "audio");
        if (msg.Voice is { } voice)
            return (voice.FileId, "voice");
        if (msg.Video is { } video)
            return (video.FileId, "video");
        if (msg.VideoNote is { } vnote)
            return (vnote.FileId, "video_note");
        if (msg.Document is { } doc)
            return (doc.FileId, "document");
        if (msg.Photo is { } photos && photos.Length > 0)
            return (photos[^1].FileId, "photo");
        return (null, "unknown");
    }

    private async Task HandleForwardedMessage(long chatId, long userId, Message msg, CancellationToken ct)
    {
        var forwardFrom = msg.ForwardOrigin switch
        {
            Telegram.Bot.Types.MessageOriginUser u => u.SenderUser.FirstName + (u.SenderUser.LastName != null ? " " + u.SenderUser.LastName : ""),
            Telegram.Bot.Types.MessageOriginChat c => c.SenderChat.Title ?? "Chat",
            Telegram.Bot.Types.MessageOriginChannel ch => ch.Chat.Title ?? "Kanal",
            Telegram.Bot.Types.MessageOriginHiddenUser h => h.SenderUserName,
            _ => "Noma'lum"
        };

        // Build title from message content
        var title = "📨 Forward: ";
        var description = "";
        string? fileId = null;
        string? fileType = null;

        if (!string.IsNullOrEmpty(msg.Text))
        {
            title += msg.Text.Length > 50 ? msg.Text[..50] + "..." : msg.Text;
            description = msg.Text;
        }
        else if (!string.IsNullOrEmpty(msg.Caption))
        {
            title += msg.Caption.Length > 50 ? msg.Caption[..50] + "..." : msg.Caption;
            description = msg.Caption;
        }
        else
        {
            title += GetMediaTypeName(msg);
        }

        if (HasMedia(msg))
        {
            var media = ExtractMedia(msg);
            fileId = media.fileId;
            fileType = media.fileType;
        }

        using var db = new AppDbContext();
        var task = new TodoItem
        {
            ChatId = chatId,
            CreatedByUserId = userId,
            Title = title,
            Description = !string.IsNullOrEmpty(description) ? $"[{forwardFrom}] dan forward qilingan:\n{description}" : $"[{forwardFrom}] dan forward qilingan",
            FileId = fileId,
            FileType = fileType,
            IsForwarded = true,
            ForwardedFrom = forwardFrom
        };
        db.Todos.Add(task);
        await db.SaveChangesAsync(ct);

        var icon = fileType switch
        {
            "audio" => "🎵",
            "voice" => "🎤",
            "video" => "🎬",
            "video_note" => "⏺",
            "document" => "📎",
            "photo" => "🖼",
            _ => "📨"
        };

        await _bot.SendMessage(chatId,
            $"✅ Forward vazifaga qo'shildi!\n\n{icon} *{EscapeMd(task.Title)}*\n📤 Kimdan: _{EscapeMd(forwardFrom)}_",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.BackToMenu(),
            cancellationToken: ct);
    }

    private async Task HandleMediaMessage(long chatId, long userId, Message msg, CancellationToken ct)
    {
        var (fileId, fileType) = ExtractMedia(msg);

        var typeName = GetMediaTypeName(msg);
        var title = !string.IsNullOrEmpty(msg.Caption)
            ? msg.Caption.Length > 50 ? msg.Caption[..50] + "..." : msg.Caption
            : typeName;

        var description = msg.Caption;

        // Extra info for audio
        if (msg.Audio is { } audio)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(audio.Title)) parts.Add(audio.Title);
            if (!string.IsNullOrEmpty(audio.Performer)) parts.Add(audio.Performer);
            if (parts.Count > 0)
            {
                title = string.Join(" — ", parts);
                if (audio.Duration > 0)
                    description = $"{description}\nDavomiyligi: {TimeSpan.FromSeconds(audio.Duration):mm\\:ss}".Trim();
            }
        }

        using var db = new AppDbContext();
        var task = new TodoItem
        {
            ChatId = chatId,
            CreatedByUserId = userId,
            Title = $"🎵 {title}",
            Description = description,
            FileId = fileId,
            FileType = fileType
        };
        db.Todos.Add(task);
        await db.SaveChangesAsync(ct);

        var icon = fileType switch
        {
            "audio" => "🎵",
            "voice" => "🎤",
            "video" => "🎬",
            "video_note" => "⏺",
            "document" => "📎",
            "photo" => "🖼",
            _ => "📋"
        };

        await _bot.SendMessage(chatId,
            $"✅ {icon} *{EscapeMd(title)}* — vazifaga qo'shildi!",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.BackToMenu(),
            cancellationToken: ct);
    }

    private static string GetMediaTypeName(Message msg)
    {
        if (msg.Audio is not null) return "Audio fayl";
        if (msg.Voice is not null) return "Ovozli xabar";
        if (msg.Video is not null) return "Video";
        if (msg.VideoNote is not null) return "Video xabar";
        if (msg.Document is not null) return msg.Document.FileName ?? "Hujjat";
        if (msg.Photo is not null) return "Rasm";
        return "Media fayl";
    }

    private async Task HandleCallback(CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Data is not { } data || cb.Message is not { } msg) return;

        var chatId = msg.Chat.Id;
        var userId = cb.From.Id;

        await _bot.AnswerCallbackQuery(cb.Id, cancellationToken: ct);

        // Main menu
        if (data == "main_menu")
        {
            ResetState(userId, chatId);
            await EditToMainMenu(chatId, msg.MessageId, ct);
            return;
        }

        // Add task
        if (data == "add_task")
        {
            var state = GetState(userId, chatId);
            state.State = BotState.WaitingForTaskTitle;
            await _bot.EditMessageText(chatId, msg.MessageId,
                "📝 *Vazifa sarlavhasini yozing:*",
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
            return;
        }

        // List tasks
        if (data == "list_active")
        {
            ResetState(userId, chatId);
            await EditTaskList(chatId, msg.MessageId, false, ct);
            return;
        }
        if (data == "list_done")
        {
            ResetState(userId, chatId);
            await EditTaskList(chatId, msg.MessageId, true, ct);
            return;
        }

        // Stats
        if (data == "stats")
        {
            await EditStatsMenu(chatId, msg.MessageId, ct);
            return;
        }
        if (data == "stats_general")
        {
            await EditStats(chatId, msg.MessageId, ct);
            return;
        }
        if (data == "stats_daily")
        {
            await EditPeriodTasks(chatId, msg.MessageId, "daily", ct);
            return;
        }
        if (data == "stats_weekly")
        {
            await EditPeriodTasks(chatId, msg.MessageId, "weekly", ct);
            return;
        }
        if (data == "stats_monthly")
        {
            await EditPeriodTasks(chatId, msg.MessageId, "monthly", ct);
            return;
        }

        // View task
        if (data.StartsWith("view_"))
        {
            var taskId = int.Parse(data[5..]);
            await EditTaskDetail(chatId, msg.MessageId, taskId, ct);
            return;
        }

        // Complete task
        if (data.StartsWith("complete_"))
        {
            var taskId = int.Parse(data[9..]);
            await CompleteTask(chatId, msg.MessageId, taskId, true, ct);
            return;
        }
        if (data.StartsWith("uncomplete_"))
        {
            var taskId = int.Parse(data[11..]);
            await CompleteTask(chatId, msg.MessageId, taskId, false, ct);
            return;
        }

        // Timer
        if (data.StartsWith("timer_start_"))
        {
            var taskId = int.Parse(data[12..]);
            await StartTimer(chatId, msg.MessageId, taskId, ct);
            return;
        }
        if (data.StartsWith("timer_stop_"))
        {
            var taskId = int.Parse(data[11..]);
            await StopTimer(chatId, msg.MessageId, taskId, ct);
            return;
        }

        // Priority
        if (data.StartsWith("priority_"))
        {
            var taskId = int.Parse(data[9..]);
            await _bot.EditMessageText(chatId, msg.MessageId,
                "🔼 *Muhimlik darajasini tanlang:*",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.PrioritySelect(taskId),
                cancellationToken: ct);
            return;
        }
        if (data.StartsWith("setpri_"))
        {
            var parts = data.Split('_');
            var taskId = int.Parse(parts[1]);
            var priority = (Priority)int.Parse(parts[2]);
            await SetPriority(chatId, msg.MessageId, taskId, priority, ct);
            return;
        }

        // Due date
        if (data.StartsWith("duedate_"))
        {
            var taskId = int.Parse(data[8..]);
            var state = GetState(userId, chatId);
            state.State = BotState.WaitingForDueDate;
            state.EditingTaskId = taskId;
            await _bot.EditMessageText(chatId, msg.MessageId,
                "📅 *Muddatni tanlang:*\n_Yoki qo'lda yozing:_ `25.12.2025` / `25.12.2025 14:00`",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.Calendar(DateTime.UtcNow),
                cancellationToken: ct);
            return;
        }
        if (data == "skip_due")
        {
            ResetState(userId, chatId);
            await EditToMainMenu(chatId, msg.MessageId, ct);
            return;
        }

        // Calendar navigation
        if (data == "cal_noop") return;

        if (data.StartsWith("calprev_") || data.StartsWith("calnext_"))
        {
            var monthStr = data[8..]; // yyyy-MM
            if (DateTime.TryParseExact(monthStr, "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var calMonth))
            {
                await _bot.EditMessageReplyMarkup(chatId, msg.MessageId,
                    replyMarkup: KeyboardBuilder.Calendar(calMonth),
                    cancellationToken: ct);
            }
            return;
        }

        if (data.StartsWith("calpick_"))
        {
            var dateStr = data[8..]; // yyyy-MM-dd
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var pickedDate))
            {
                await _bot.EditMessageText(chatId, msg.MessageId,
                    $"📅 *Sana:* `{pickedDate:dd.MM.yyyy}`\n\n🕐 *Vaqtni tanlang:*",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: KeyboardBuilder.TimePicker(dateStr),
                    cancellationToken: ct);
            }
            return;
        }

        if (data.StartsWith("caltime_"))
        {
            // caltime_yyyy-MM-dd_HHMM or caltime_yyyy-MM-dd_none
            var payload = data[8..];
            var lastUnderscore = payload.LastIndexOf('_');
            var dateStr = payload[..lastUnderscore];
            var timeStr = payload[(lastUnderscore + 1)..];

            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var finalDate))
            {
                if (timeStr != "none")
                {
                    var hour = int.Parse(timeStr[..2]);
                    var minute = int.Parse(timeStr[2..]);
                    finalDate = finalDate.AddHours(hour).AddMinutes(minute);
                }

                var state = GetState(userId, chatId);
                if (state.EditingTaskId.HasValue)
                {
                    using var db = new AppDbContext();
                    var task = await db.Todos.FindAsync(new object[] { state.EditingTaskId.Value }, ct);
                    if (task != null)
                    {
                        task.DueDate = finalDate;
                        await db.SaveChangesAsync(ct);
                    }
                }

                ResetState(userId, chatId);
                var dateDisplay = timeStr == "none"
                    ? finalDate.ToString("dd.MM.yyyy")
                    : finalDate.ToString("dd.MM.yyyy HH:mm");

                await _bot.EditMessageText(chatId, msg.MessageId,
                    $"✅ Muddat belgilandi: *{dateDisplay}*",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: KeyboardBuilder.BackToMenu(),
                    cancellationToken: ct);
            }
            return;
        }

        // Edit task
        if (data.StartsWith("edit_"))
        {
            var taskId = int.Parse(data[5..]);
            await _bot.EditMessageText(chatId, msg.MessageId,
                "✏️ *Nimani tahrirlaysiz?*",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.EditMenu(taskId),
                cancellationToken: ct);
            return;
        }
        if (data.StartsWith("edittitle_"))
        {
            var taskId = int.Parse(data[10..]);
            var state = GetState(userId, chatId);
            state.State = BotState.EditingTitle;
            state.EditingTaskId = taskId;
            await _bot.EditMessageText(chatId, msg.MessageId,
                "📝 *Yangi sarlavhani yozing:*",
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
            return;
        }
        if (data.StartsWith("editdesc_"))
        {
            var taskId = int.Parse(data[9..]);
            var state = GetState(userId, chatId);
            state.State = BotState.EditingDescription;
            state.EditingTaskId = taskId;
            await _bot.EditMessageText(chatId, msg.MessageId,
                "📄 *Yangi tavsifni yozing:*",
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
            return;
        }

        // Delete
        if (data.StartsWith("delete_") && !data.StartsWith("delete_all"))
        {
            var taskId = int.Parse(data[7..]);
            await _bot.EditMessageText(chatId, msg.MessageId,
                "⚠️ *Bu vazifani o'chirishni xohlaysizmi?*",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.ConfirmDelete(taskId),
                cancellationToken: ct);
            return;
        }
        if (data.StartsWith("confirmdelete_"))
        {
            var taskId = int.Parse(data[14..]);
            await DeleteTask(chatId, msg.MessageId, taskId, ct);
            return;
        }

        // Delete all
        if (data == "delete_all_confirm")
        {
            await _bot.EditMessageText(chatId, msg.MessageId,
                "⚠️ *BARCHA vazifalarni o'chirishni xohlaysizmi?*\nBu amalni qaytarib bo'lmaydi!",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.ConfirmDeleteAll(),
                cancellationToken: ct);
            return;
        }
        if (data == "delete_all_yes")
        {
            await DeleteAllTasks(chatId, msg.MessageId, ct);
            return;
        }

        // Skip description → go to priority
        if (data == "skip_desc")
        {
            var state = GetState(userId, chatId);
            if (state.EditingTaskId.HasValue)
            {
                state.State = BotState.WaitingForPriority;
                await _bot.SendMessage(chatId,
                    "🔼 *Muhimlik darajasini tanlang:*\nBermasa oddiy (O'rta) bo'lib qoladi.",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: KeyboardBuilder.NewTaskPriority(state.EditingTaskId.Value),
                    cancellationToken: ct);
            }
            return;
        }

        // New task priority selection
        if (data.StartsWith("newpri_"))
        {
            var parts = data.Split('_');
            var taskId = int.Parse(parts[1]);
            var state = GetState(userId, chatId);

            if (parts[2] != "skip")
            {
                var priority = (Priority)int.Parse(parts[2]);
                using var db = new AppDbContext();
                var task = await db.Todos.FindAsync(new object[] { taskId }, ct);
                if (task != null && task.ChatId == chatId)
                {
                    task.Priority = priority;
                    await db.SaveChangesAsync(ct);
                }
            }

            state.State = BotState.WaitingForDueDate;
            state.EditingTaskId = taskId;
            await _bot.SendMessage(chatId,
                "📅 *Muddatni tanlang:*\n_Yoki qo'lda yozing:_ `25.12.2025` / `25.12.2025 14:00`",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.Calendar(DateTime.UtcNow),
                cancellationToken: ct);
            return;
        }
    }

    // ---- Task creation flow ----

    private async Task HandleTaskTitle(long chatId, long userId, string title, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var task = new TodoItem
        {
            ChatId = chatId,
            CreatedByUserId = userId,
            Title = title.Trim()
        };
        db.Todos.Add(task);
        await db.SaveChangesAsync(ct);

        var state = GetState(userId, chatId);
        state.State = BotState.WaitingForTaskDescription;
        state.EditingTaskId = task.Id;

        await _bot.SendMessage(chatId,
            $"✅ Vazifa qo'shildi: *{EscapeMd(task.Title)}*\n\n📄 Tavsif yozing yoki o'tkazib yuboring:",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.SkipDescription(),
            cancellationToken: ct);
    }

    private async Task HandleTaskDescription(long chatId, long userId, string description, CancellationToken ct)
    {
        var state = GetState(userId, chatId);
        if (!state.EditingTaskId.HasValue) return;

        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { state.EditingTaskId.Value }, ct);
        if (task != null)
        {
            task.Description = description.Trim();
            await db.SaveChangesAsync(ct);
        }

        state.State = BotState.WaitingForPriority;

        await _bot.SendMessage(chatId,
            "🔼 *Muhimlik darajasini tanlang:*\nBermasa oddiy (O'rta) bo'lib qoladi.",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.NewTaskPriority(state.EditingTaskId.Value),
            cancellationToken: ct);
    }

    private async Task HandleDueDate(long chatId, long userId, string dateText, CancellationToken ct)
    {
        var state = GetState(userId, chatId);
        if (!state.EditingTaskId.HasValue) return;

        var formats = new[] { "dd.MM.yyyy", "dd.MM.yyyy HH:mm", "dd/MM/yyyy", "dd/MM/yyyy HH:mm" };
        if (DateTime.TryParseExact(dateText.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dueDate))
        {
            using var db = new AppDbContext();
            var task = await db.Todos.FindAsync(new object[] { state.EditingTaskId.Value }, ct);
            if (task != null)
            {
                task.DueDate = dueDate;
                await db.SaveChangesAsync(ct);
            }

            ResetState(userId, chatId);
            await _bot.SendMessage(chatId,
                $"✅ Muddat belgilandi: *{dueDate:dd.MM.yyyy HH:mm}*",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToMenu(),
                cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(chatId,
                "❌ Noto'g'ri format\\. Kalendardan tanlang yoki `25.12.2025` formatida yozing\\.",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.Calendar(DateTime.UtcNow),
                cancellationToken: ct);
        }
    }

    private async Task QuickAddTask(long chatId, long userId, string title, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var task = new TodoItem
        {
            ChatId = chatId,
            CreatedByUserId = userId,
            Title = title.Trim()
        };
        db.Todos.Add(task);
        await db.SaveChangesAsync(ct);

        await _bot.SendMessage(chatId,
            $"✅ Vazifa qo'shildi: *{EscapeMd(task.Title)}*",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.BackToMenu(),
            cancellationToken: ct);
    }

    // ---- Edit handlers ----

    private async Task HandleEditTitle(long chatId, long userId, string newTitle, CancellationToken ct)
    {
        var state = GetState(userId, chatId);
        if (!state.EditingTaskId.HasValue) return;

        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { state.EditingTaskId.Value }, ct);
        if (task != null)
        {
            task.Title = newTitle.Trim();
            await db.SaveChangesAsync(ct);
        }

        ResetState(userId, chatId);
        await _bot.SendMessage(chatId,
            "✅ Sarlavha yangilandi!",
            replyMarkup: KeyboardBuilder.BackToMenu(),
            cancellationToken: ct);
    }

    private async Task HandleEditDescription(long chatId, long userId, string newDesc, CancellationToken ct)
    {
        var state = GetState(userId, chatId);
        if (!state.EditingTaskId.HasValue) return;

        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { state.EditingTaskId.Value }, ct);
        if (task != null)
        {
            task.Description = newDesc.Trim();
            await db.SaveChangesAsync(ct);
        }

        ResetState(userId, chatId);
        await _bot.SendMessage(chatId,
            "✅ Tavsif yangilandi!",
            replyMarkup: KeyboardBuilder.BackToMenu(),
            cancellationToken: ct);
    }

    private async Task HandleEditDueDate(long chatId, long userId, string dateText, CancellationToken ct)
    {
        var state = GetState(userId, chatId);
        if (!state.EditingTaskId.HasValue) return;

        var formats = new[] { "dd.MM.yyyy", "dd.MM.yyyy HH:mm", "dd/MM/yyyy", "dd/MM/yyyy HH:mm" };
        if (DateTime.TryParseExact(dateText.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dueDate))
        {
            using var db = new AppDbContext();
            var task = await db.Todos.FindAsync(new object[] { state.EditingTaskId.Value }, ct);
            if (task != null)
            {
                task.DueDate = dueDate;
                await db.SaveChangesAsync(ct);
            }

            ResetState(userId, chatId);
            await _bot.SendMessage(chatId,
                $"✅ Muddat yangilandi: *{dueDate:dd.MM.yyyy HH:mm}*",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToMenu(),
                cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(chatId,
                "❌ Noto'g'ri format. `25.12.2025` yoki `25.12.2025 14:00`",
                parseMode: ParseMode.Markdown,
                cancellationToken: ct);
        }
    }

    // ---- Task operations ----

    private async Task CompleteTask(long chatId, int messageId, int taskId, bool completed, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { taskId }, ct);
        if (task != null && task.ChatId == chatId)
        {
            // Auto-stop timer if running
            if (completed && task.TimerStartedAt.HasValue)
            {
                var elapsed = (long)(DateTime.UtcNow - task.TimerStartedAt.Value).TotalSeconds;
                task.TimeSpentSeconds += elapsed;
                task.TimerStartedAt = null;
            }

            task.IsCompleted = completed;
            task.CompletedAt = completed ? DateTime.UtcNow : null;
            await db.SaveChangesAsync(ct);

            var timeText = task.TimeSpentSeconds > 0
                ? $"\n⏱ Sarflangan vaqt: {FormatTimeSpan(task.TimeSpentSeconds)}"
                : "";
            var text = completed
                ? $"✅ *{EscapeMd(task.Title)}* — bajarildi!{timeText}"
                : $"↩️ *{EscapeMd(task.Title)}* — qaytarildi";
            await _bot.EditMessageText(chatId, messageId, text,
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToMenu(),
                cancellationToken: ct);
        }
    }

    private async Task StartTimer(long chatId, int messageId, int taskId, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { taskId }, ct);
        if (task != null && task.ChatId == chatId && !task.IsCompleted)
        {
            task.TimerStartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await EditTaskDetail(chatId, messageId, taskId, ct);
        }
    }

    private async Task StopTimer(long chatId, int messageId, int taskId, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { taskId }, ct);
        if (task != null && task.ChatId == chatId && task.TimerStartedAt.HasValue)
        {
            var elapsed = (long)(DateTime.UtcNow - task.TimerStartedAt.Value).TotalSeconds;
            task.TimeSpentSeconds += elapsed;
            task.TimerStartedAt = null;
            await db.SaveChangesAsync(ct);
            await EditTaskDetail(chatId, messageId, taskId, ct);
        }
    }

    private static string FormatTimeSpan(long totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}s {ts.Minutes:D2}d {ts.Seconds:D2}s";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}d {ts.Seconds:D2}s";
        return $"{ts.Seconds}s";
    }

    private async Task SetPriority(long chatId, int messageId, int taskId, Priority priority, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { taskId }, ct);
        if (task != null && task.ChatId == chatId)
        {
            task.Priority = priority;
            await db.SaveChangesAsync(ct);
            await EditTaskDetail(chatId, messageId, taskId, ct);
        }
    }

    private async Task DeleteTask(long chatId, int messageId, int taskId, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { taskId }, ct);
        if (task != null && task.ChatId == chatId)
        {
            db.Todos.Remove(task);
            await db.SaveChangesAsync(ct);
            await _bot.EditMessageText(chatId, messageId,
                $"🗑 *{EscapeMd(task.Title)}* — o'chirildi",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToMenu(),
                cancellationToken: ct);
        }
    }

    private async Task DeleteAllTasks(long chatId, int messageId, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var tasks = await db.Todos.Where(t => t.ChatId == chatId).ToListAsync(ct);
        db.Todos.RemoveRange(tasks);
        await db.SaveChangesAsync(ct);

        await _bot.EditMessageText(chatId, messageId,
            $"🗑 *{tasks.Count} ta vazifa o'chirildi*",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.BackToMenu(),
            cancellationToken: ct);
    }

    // ---- Display methods ----

    private async Task SendMainMenu(long chatId, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var activeCount = await db.Todos.CountAsync(t => t.ChatId == chatId && !t.IsCompleted, ct);
        var doneCount = await db.Todos.CountAsync(t => t.ChatId == chatId && t.IsCompleted, ct);

        var text = "📋 *To\\-Do Bot*\n\n" +
                   $"⬜ Faol vazifalar: *{activeCount}*\n" +
                   $"✅ Bajarilgan: *{doneCount}*\n\n" +
                   "Quyidagi tugmalardan birini tanlang:";

        await _bot.SendMessage(chatId, text,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: KeyboardBuilder.MainMenu(),
            cancellationToken: ct);
    }

    private async Task EditToMainMenu(long chatId, int messageId, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var activeCount = await db.Todos.CountAsync(t => t.ChatId == chatId && !t.IsCompleted, ct);
        var doneCount = await db.Todos.CountAsync(t => t.ChatId == chatId && t.IsCompleted, ct);

        var text = "📋 *To\\-Do Bot*\n\n" +
                   $"⬜ Faol vazifalar: *{activeCount}*\n" +
                   $"✅ Bajarilgan: *{doneCount}*\n\n" +
                   "Quyidagi tugmalardan birini tanlang:";

        await _bot.EditMessageText(chatId, messageId, text,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: KeyboardBuilder.MainMenu(),
            cancellationToken: ct);
    }

    private async Task SendTaskList(long chatId, bool showCompleted, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var tasks = await db.Todos
            .Where(t => t.ChatId == chatId && t.IsCompleted == showCompleted)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var title = showCompleted ? "✅ *Bajarilgan vazifalar:*" : "📋 *Faol vazifalar:*";

        if (!tasks.Any())
        {
            var emptyText = showCompleted ? "Hali bajarilgan vazifa yo'q." : "Hali vazifa yo'q. ➕ Yangi vazifa qo'shing!";
            await _bot.SendMessage(chatId, $"{title}\n\n{emptyText}",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToMenu(),
                cancellationToken: ct);
            return;
        }

        await _bot.SendMessage(chatId, $"{title}\n\nVazifani tanlang:",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.TaskList(tasks, showCompleted),
            cancellationToken: ct);
    }

    private async Task EditTaskList(long chatId, int messageId, bool showCompleted, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var tasks = await db.Todos
            .Where(t => t.ChatId == chatId && t.IsCompleted == showCompleted)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var title = showCompleted ? "✅ *Bajarilgan vazifalar:*" : "📋 *Faol vazifalar:*";

        if (!tasks.Any())
        {
            var emptyText = showCompleted ? "Hali bajarilgan vazifa yo'q." : "Hali vazifa yo'q. ➕ Yangi vazifa qo'shing!";
            await _bot.EditMessageText(chatId, messageId, $"{title}\n\n{emptyText}",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToMenu(),
                cancellationToken: ct);
            return;
        }

        await _bot.EditMessageText(chatId, messageId, $"{title}\n\nVazifani tanlang:",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.TaskList(tasks, showCompleted),
            cancellationToken: ct);
    }

    private async Task EditTaskDetail(long chatId, int messageId, int taskId, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var task = await db.Todos.FindAsync(new object[] { taskId }, ct);
        if (task == null || task.ChatId != chatId) return;

        var priorityText = task.Priority switch
        {
            Priority.Urgent => "🔴 Shoshilinch",
            Priority.High => "🟠 Yuqori",
            Priority.Normal => "🟡 O'rta",
            Priority.Low => "🟢 Past",
            _ => "⚪"
        };

        var status = task.IsCompleted ? "✅ Bajarilgan" : "⬜ Faol";
        var text = $"📌 *{EscapeMd(task.Title)}*\n\n" +
                   $"📊 Holat: {status}\n" +
                   $"🔼 Muhimlik: {priorityText}\n" +
                   $"📅 Yaratilgan: {task.CreatedAt:dd.MM.yyyy HH:mm}\n";

        if (task.DueDate.HasValue)
        {
            var isOverdue = !task.IsCompleted && task.DueDate.Value < DateTime.UtcNow;
            text += $"⏰ Muddat: {task.DueDate.Value:dd.MM.yyyy HH:mm}";
            if (isOverdue) text += " ⚠️ *MUDDATI O'TGAN*";
            text += "\n";
        }

        if (task.IsForwarded && !string.IsNullOrEmpty(task.ForwardedFrom))
        {
            text += $"📤 Forward: _{EscapeMd(task.ForwardedFrom)}_\n";
        }

        if (!string.IsNullOrEmpty(task.FileType))
        {
            var mediaIcon = task.FileType switch
            {
                "audio" => "🎵 Audio",
                "voice" => "🎤 Ovozli xabar",
                "video" => "🎬 Video",
                "video_note" => "⏺ Video xabar",
                "document" => "📎 Hujjat",
                "photo" => "🖼 Rasm",
                _ => "📁 Fayl"
            };
            text += $"📁 Media: {mediaIcon}\n";
        }

        // Timer info
        if (task.TimerStartedAt.HasValue)
        {
            var running = (long)(DateTime.UtcNow - task.TimerStartedAt.Value).TotalSeconds;
            var total = task.TimeSpentSeconds + running;
            text += $"\n⏱ *Timer ishlayapti:* {FormatTimeSpan(running)}";
            if (task.TimeSpentSeconds > 0)
                text += $"\n⏱ Jami vaqt: {FormatTimeSpan(total)}";
            text += "\n";
        }
        else if (task.TimeSpentSeconds > 0)
        {
            text += $"\n⏱ Sarflangan vaqt: {FormatTimeSpan(task.TimeSpentSeconds)}\n";
        }

        if (!string.IsNullOrEmpty(task.Description))
        {
            text += $"\n📄 {EscapeMd(task.Description)}";
        }

        if (task.CompletedAt.HasValue)
        {
            text += $"\n✅ Bajarilgan: {task.CompletedAt.Value:dd.MM.yyyy HH:mm}";
        }

        await _bot.EditMessageText(chatId, messageId, text,
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.TaskDetail(task),
            cancellationToken: ct);
    }

    private async Task SendStats(long chatId, CancellationToken ct)
    {
        var text = await BuildStatsText(chatId, ct);
        await _bot.SendMessage(chatId, text,
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.StatsMenu(),
            cancellationToken: ct);
    }

    private async Task EditStatsMenu(long chatId, int messageId, CancellationToken ct)
    {
        var text = await BuildStatsText(chatId, ct);
        await _bot.EditMessageText(chatId, messageId, text,
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.StatsMenu(),
            cancellationToken: ct);
    }

    private async Task EditStats(long chatId, int messageId, CancellationToken ct)
    {
        var text = await BuildStatsText(chatId, ct);
        await _bot.EditMessageText(chatId, messageId, text,
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.BackToStats(),
            cancellationToken: ct);
    }

    private async Task<string> BuildStatsText(long chatId, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var total = await db.Todos.CountAsync(t => t.ChatId == chatId, ct);
        var active = await db.Todos.CountAsync(t => t.ChatId == chatId && !t.IsCompleted, ct);
        var done = await db.Todos.CountAsync(t => t.ChatId == chatId && t.IsCompleted, ct);
        var overdue = await db.Todos.CountAsync(t =>
            t.ChatId == chatId && !t.IsCompleted && t.DueDate.HasValue && t.DueDate < DateTime.UtcNow, ct);
        var urgent = await db.Todos.CountAsync(t =>
            t.ChatId == chatId && !t.IsCompleted && t.Priority == Priority.Urgent, ct);

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
        if (now.DayOfWeek == DayOfWeek.Sunday) weekStart = weekStart.AddDays(-7);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var todayCreated = await db.Todos.CountAsync(t => t.ChatId == chatId && t.CreatedAt >= todayStart, ct);
        var todayDone = await db.Todos.CountAsync(t => t.ChatId == chatId && t.IsCompleted && t.CompletedAt >= todayStart, ct);
        var weekCreated = await db.Todos.CountAsync(t => t.ChatId == chatId && t.CreatedAt >= weekStart, ct);
        var weekDone = await db.Todos.CountAsync(t => t.ChatId == chatId && t.IsCompleted && t.CompletedAt >= weekStart, ct);
        var monthCreated = await db.Todos.CountAsync(t => t.ChatId == chatId && t.CreatedAt >= monthStart, ct);
        var monthDone = await db.Todos.CountAsync(t => t.ChatId == chatId && t.IsCompleted && t.CompletedAt >= monthStart, ct);

        var percent = total > 0 ? (done * 100 / total) : 0;
        var bar = GetProgressBar(percent);

        return $"📊 *Statistika*\n\n" +
               $"📋 Jami: *{total}* | ⬜ Faol: *{active}* | ✅ Bajarilgan: *{done}*\n" +
               $"⚠️ Muddati o'tgan: *{overdue}* | 🔴 Shoshilinch: *{urgent}*\n\n" +
               $"📅 *Bugun:* +{todayCreated} vazifa, ✅ {todayDone} bajarildi\n" +
               $"📆 *Hafta:* +{weekCreated} vazifa, ✅ {weekDone} bajarildi\n" +
               $"🗓 *Oy:* +{monthCreated} vazifa, ✅ {monthDone} bajarildi\n\n" +
               $"Bajarilish: {bar} {percent}%\n\n" +
               "Davrni tanlang — vazifalar ro'yxatini ko'ring:";
    }

    private async Task EditPeriodTasks(long chatId, int messageId, string period, CancellationToken ct)
    {
        using var db = new AppDbContext();
        var now = DateTime.UtcNow;

        DateTime fromDate;
        string title;

        switch (period)
        {
            case "daily":
                fromDate = now.Date;
                title = $"📅 *Bugungi vazifalar* ({now:dd.MM.yyyy})";
                break;
            case "weekly":
                fromDate = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
                if (now.DayOfWeek == DayOfWeek.Sunday) fromDate = fromDate.AddDays(-7);
                title = $"📆 *Haftalik vazifalar* ({fromDate:dd.MM} — {fromDate.AddDays(6):dd.MM})";
                break;
            case "monthly":
                fromDate = new DateTime(now.Year, now.Month, 1);
                title = $"🗓 *Oylik vazifalar* ({now:MMMM yyyy})";
                break;
            default:
                return;
        }

        var tasks = await db.Todos
            .Where(t => t.ChatId == chatId && t.CreatedAt >= fromDate)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.IsCompleted)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var active = tasks.Count(t => !t.IsCompleted);
        var done = tasks.Count(t => t.IsCompleted);

        var header = $"{title}\n\n" +
                     $"📋 Jami: *{tasks.Count}* | ⬜ Faol: *{active}* | ✅ Bajarilgan: *{done}*";

        if (!tasks.Any())
        {
            await _bot.EditMessageText(chatId, messageId,
                $"{header}\n\nBu davrda vazifa yo'q.",
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToStats(),
                cancellationToken: ct);
            return;
        }

        await _bot.EditMessageText(chatId, messageId,
            $"{header}\n\nVazifani tanlang:",
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.PeriodTaskList(tasks, period),
            cancellationToken: ct);
    }

    private async Task SendHelp(long chatId, CancellationToken ct)
    {
        var text = "📖 *Yordam*\n\n" +
                   "*Buyruqlar:*\n" +
                   "/start — Bosh menyu\n" +
                   "/menu — Bosh menyu\n" +
                   "/add — Yangi vazifa qo'shish\n" +
                   "/add Vazifa nomi — Tezkor qo'shish\n" +
                   "/list — Faol vazifalar\n" +
                   "/done — Bajarilganlar\n" +
                   "/stats — Statistika\n" +
                   "/help — Yordam\n\n" +
                   "*Xususiyatlar:*\n" +
                   "• Vazifa qo'shish, tahrirlash, o'chirish\n" +
                   "• Muhimlik darajasi (Past/O'rta/Yuqori/Shoshilinch)\n" +
                   "• Muddat belgilash\n" +
                   "• Statistika va progress\n" +
                   "• Kanalda ishlash (admin sifatida qo'shing)";

        await _bot.SendMessage(chatId, text,
            parseMode: ParseMode.Markdown,
            replyMarkup: KeyboardBuilder.BackToMenu(),
            cancellationToken: ct);
    }

    private static string GetProgressBar(int percent)
    {
        var filled = percent / 10;
        var empty = 10 - filled;
        return new string('▓', filled) + new string('░', empty);
    }

    private static string EscapeMd(string text)
    {
        return text
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("`", "\\`");
    }
}
