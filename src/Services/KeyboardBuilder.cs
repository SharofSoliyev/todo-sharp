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

    private static readonly string[] UzMonths = { "", "Yanvar", "Fevral", "Mart", "Aprel", "May", "Iyun",
                                                       "Iyul", "Avgust", "Sentabr", "Oktabr", "Noyabr", "Dekabr" };

    public static InlineKeyboardMarkup Calendar(DateTime month)
    {
        var buttons = new List<InlineKeyboardButton[]>();
        var today = DateTime.UtcNow.Date;

        var prevMonth = month.AddMonths(-1);
        var nextMonth = month.AddMonths(1);

        // Header: ◀️ Mart 2026 ▶️
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("◀️", $"calprev_{prevMonth:yyyy-MM}"),
            InlineKeyboardButton.WithCallbackData($"📅 {UzMonths[month.Month]} {month.Year}", "cal_noop"),
            InlineKeyboardButton.WithCallbackData("▶️", $"calnext_{nextMonth:yyyy-MM}"),
        });

        // Day-of-week headers (Uzbek week starts Monday)
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("Du", "cal_noop"),
            InlineKeyboardButton.WithCallbackData("Se", "cal_noop"),
            InlineKeyboardButton.WithCallbackData("Ch", "cal_noop"),
            InlineKeyboardButton.WithCallbackData("Pa", "cal_noop"),
            InlineKeyboardButton.WithCallbackData("Ju", "cal_noop"),
            InlineKeyboardButton.WithCallbackData("Sh", "cal_noop"),
            InlineKeyboardButton.WithCallbackData("Ya", "cal_noop"),
        });

        // Days grid
        var firstDay = new DateTime(month.Year, month.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        var startOffset = ((int)firstDay.DayOfWeek + 6) % 7; // Mon=0, Sun=6

        var row = new List<InlineKeyboardButton>();

        for (int i = 0; i < startOffset; i++)
            row.Add(InlineKeyboardButton.WithCallbackData(" ", "cal_noop"));

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(month.Year, month.Month, day);
            var dateStr = date.ToString("yyyy-MM-dd");

            if (date.Date == today)
                row.Add(InlineKeyboardButton.WithCallbackData($"•{day}•", $"calpick_{dateStr}"));
            else if (date < today)
                row.Add(InlineKeyboardButton.WithCallbackData($"  ", "cal_noop"));
            else
                row.Add(InlineKeyboardButton.WithCallbackData($"{day}", $"calpick_{dateStr}"));

            if (row.Count == 7)
            {
                buttons.Add(row.ToArray());
                row = new List<InlineKeyboardButton>();
            }
        }

        while (row.Count > 0 && row.Count < 7)
            row.Add(InlineKeyboardButton.WithCallbackData(" ", "cal_noop"));
        if (row.Count > 0)
            buttons.Add(row.ToArray());

        // Skip button
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("⏭ O'tkazib yuborish", "skip_due"),
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup TimePicker(string dateStr)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("09:00", $"caltime_{dateStr}_0900"),
                InlineKeyboardButton.WithCallbackData("12:00", $"caltime_{dateStr}_1200"),
                InlineKeyboardButton.WithCallbackData("15:00", $"caltime_{dateStr}_1500"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("18:00", $"caltime_{dateStr}_1800"),
                InlineKeyboardButton.WithCallbackData("21:00", $"caltime_{dateStr}_2100"),
                InlineKeyboardButton.WithCallbackData("23:59", $"caltime_{dateStr}_2359"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏭ Vaqtsiz", $"caltime_{dateStr}_none"),
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
