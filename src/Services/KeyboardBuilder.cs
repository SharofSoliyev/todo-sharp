using Telegram.Bot.Types.ReplyMarkups;
using TodoBot.Models;

namespace TodoBot.Services;

public static class KeyboardBuilder
{
    public static InlineKeyboardMarkup MainMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📋 Vazifalar ro'yxati", "list_active"),
                InlineKeyboardButton.WithCallbackData("➕ Yangi vazifa", "add_task"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Bajarilganlar", "list_done"),
                InlineKeyboardButton.WithCallbackData("📊 Statistika", "stats"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🗑 Hammasini o'chirish", "delete_all_confirm"),
            }
        });
    }

    public static InlineKeyboardMarkup TaskList(List<TodoItem> tasks, bool showCompleted = false)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var task in tasks)
        {
            var icon = task.Priority switch
            {
                Priority.Urgent => "🔴",
                Priority.High => "🟠",
                Priority.Normal => "🟡",
                Priority.Low => "🟢",
                _ => "⚪"
            };

            var status = task.IsCompleted ? "✅" : "⬜";
            var text = $"{status} {icon} {task.Title}";

            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(text, $"view_{task.Id}")
            });
        }

        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Ortga", "main_menu")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup TaskDetail(TodoItem task)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        if (!task.IsCompleted)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Bajarildi", $"complete_{task.Id}"),
                InlineKeyboardButton.WithCallbackData("✏️ Tahrirlash", $"edit_{task.Id}"),
            });
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("🔼 Muhimlik", $"priority_{task.Id}"),
                InlineKeyboardButton.WithCallbackData("📅 Muddat", $"duedate_{task.Id}"),
            });

            // Timer button
            if (task.TimerStartedAt.HasValue)
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("⏹ To'xtatish", $"timer_stop_{task.Id}"),
                    InlineKeyboardButton.WithCallbackData("🔄 Yangilash", $"view_{task.Id}"),
                });
            }
            else
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData("▶️ Boshlash (Timer)", $"timer_start_{task.Id}"),
                });
            }
        }
        else
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("↩️ Qaytarish", $"uncomplete_{task.Id}"),
            });
        }

        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🗑 O'chirish", $"delete_{task.Id}"),
            InlineKeyboardButton.WithCallbackData("🔙 Ortga", "list_active"),
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup PrioritySelect(int taskId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🟢 Past", $"setpri_{taskId}_0"),
                InlineKeyboardButton.WithCallbackData("🟡 O'rta", $"setpri_{taskId}_1"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🟠 Yuqori", $"setpri_{taskId}_2"),
                InlineKeyboardButton.WithCallbackData("🔴 Shoshilinch", $"setpri_{taskId}_3"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Ortga", $"view_{taskId}"),
            }
        });
    }

    public static InlineKeyboardMarkup EditMenu(int taskId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📝 Sarlavha", $"edittitle_{taskId}"),
                InlineKeyboardButton.WithCallbackData("📄 Tavsif", $"editdesc_{taskId}"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Ortga", $"view_{taskId}"),
            }
        });
    }

    public static InlineKeyboardMarkup ConfirmDelete(int taskId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Ha, o'chirish", $"confirmdelete_{taskId}"),
                InlineKeyboardButton.WithCallbackData("❌ Bekor qilish", $"view_{taskId}"),
            }
        });
    }

    public static InlineKeyboardMarkup ConfirmDeleteAll()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Ha, hammasini o'chirish", "delete_all_yes"),
                InlineKeyboardButton.WithCallbackData("❌ Bekor qilish", "main_menu"),
            }
        });
    }

    public static InlineKeyboardMarkup SkipDescription()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏭ O'tkazib yuborish", "skip_desc"),
            }
        });
    }

    public static InlineKeyboardMarkup NewTaskPriority(int taskId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🟢 Past", $"newpri_{taskId}_0"),
                InlineKeyboardButton.WithCallbackData("🟡 O'rta", $"newpri_{taskId}_1"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🟠 Yuqori", $"newpri_{taskId}_2"),
                InlineKeyboardButton.WithCallbackData("🔴 Shoshilinch", $"newpri_{taskId}_3"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏭ O'tkazib yuborish (O'rta)", $"newpri_{taskId}_skip"),
            }
        });
    }

    public static InlineKeyboardMarkup SkipDueDate()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏭ O'tkazib yuborish", "skip_due"),
            }
        });
    }

    public static InlineKeyboardMarkup StatsMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 Bugungi", "stats_daily"),
                InlineKeyboardButton.WithCallbackData("📆 Haftalik", "stats_weekly"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🗓 Oylik", "stats_monthly"),
                InlineKeyboardButton.WithCallbackData("📊 Umumiy", "stats_general"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Bosh menyu", "main_menu"),
            }
        });
    }

    public static InlineKeyboardMarkup PeriodTaskList(List<TodoItem> tasks, string period)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var task in tasks)
        {
            var icon = task.Priority switch
            {
                Priority.Urgent => "🔴",
                Priority.High => "🟠",
                Priority.Normal => "🟡",
                Priority.Low => "🟢",
                _ => "⚪"
            };

            var status = task.IsCompleted ? "✅" : "⬜";
            var text = $"{status} {icon} {task.Title}";

            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(text, $"view_{task.Id}")
            });
        }

        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("🔙 Statistika", "stats"),
            InlineKeyboardButton.WithCallbackData("🔙 Bosh menyu", "main_menu"),
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup BackToStats()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Statistika", "stats"),
                InlineKeyboardButton.WithCallbackData("🔙 Bosh menyu", "main_menu"),
            }
        });
    }

    public static InlineKeyboardMarkup BackToMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Bosh menyu", "main_menu"),
            }
        });
    }
}
