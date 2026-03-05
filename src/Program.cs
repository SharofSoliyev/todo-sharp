using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TodoBot.Data;
using TodoBot.Services;

Console.WriteLine("🤖 To-Do Bot ishga tushmoqda...");

// Database yaratish
using (var db = new AppDbContext())
{
    await db.Database.EnsureCreatedAsync();
    Console.WriteLine("✅ Database tayyor");
}

// Token olish: 1) env variable, 2) bazadan, 3) qo'lda kiritish
var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");

if (string.IsNullOrEmpty(token))
{
    using var db = new AppDbContext();
    token = await db.GetConfigAsync("bot_token");
    if (!string.IsNullOrEmpty(token))
    {
        Console.WriteLine("✅ Token bazadan yuklandi");
    }
}
else
{
    // Env variable dan kelgan tokenni ham bazaga saqlash
    using var db = new AppDbContext();
    var existing = await db.GetConfigAsync("bot_token");
    if (existing != token)
    {
        await db.SetConfigAsync("bot_token", token);
        Console.WriteLine("✅ Token bazaga saqlandi");
    }
}

if (string.IsNullOrEmpty(token))
{
    Console.Write("Bot tokenini kiriting: ");
    token = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(token))
    {
        Console.WriteLine("❌ Token kiritilmadi! Dastur to'xtatildi.");
        return;
    }

    // Tokenni bazaga saqlash
    using var db = new AppDbContext();
    await db.SetConfigAsync("bot_token", token);
    Console.WriteLine("✅ Token bazaga saqlandi — keyingi safar avtomatik yuklanadi");
}

var bot = new TelegramBotClient(token);
var botService = new BotService(bot);

try
{
    var me = await bot.GetMe();
    Console.WriteLine($"✅ Bot ishga tushdi: @{me.Username}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Token noto'g'ri yoki ulanish xatosi: {ex.Message}");
    Console.Write("Yangi token kiritasizmi? (ha/yo'q): ");
    var answer = Console.ReadLine()?.Trim().ToLower();
    if (answer == "ha" || answer == "h")
    {
        Console.Write("Yangi tokenni kiriting: ");
        token = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            Console.WriteLine("❌ Token kiritilmadi!");
            return;
        }
        using var db = new AppDbContext();
        await db.SetConfigAsync("bot_token", token);
        Console.WriteLine("✅ Yangi token saqlandi");
        bot = new TelegramBotClient(token);
        botService = new BotService(bot);
        var me = await bot.GetMe();
        Console.WriteLine($"✅ Bot ishga tushdi: @{me.Username}");
    }
    else
    {
        return;
    }
}

Console.WriteLine("📋 To-Do Bot tayyor! Ctrl+C bilan to'xtatish mumkin.\n");

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n🛑 Bot to'xtatilmoqda...");
};

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = new[]
    {
        UpdateType.Message,
        UpdateType.CallbackQuery,
        UpdateType.ChannelPost
    },
    DropPendingUpdates = true
};

bot.StartReceiving(
    updateHandler: async (client, update, ct) =>
    {
        await botService.HandleUpdateAsync(update, ct);
    },
    errorHandler: async (client, exception, source, ct) =>
    {
        Console.WriteLine($"❌ Xato: {exception.Message}");
        await Task.CompletedTask;
    },
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("👋 Bot to'xtatildi. Xayr!");
}
