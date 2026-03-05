using Microsoft.EntityFrameworkCore;
using TodoBot.Models;

namespace TodoBot.Data;

public class AppDbContext : DbContext
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();
    public DbSet<BotConfig> Configs => Set<BotConfig>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=todobot.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ChatId);
            entity.HasIndex(e => new { e.ChatId, e.IsCompleted });
        });

        modelBuilder.Entity<BotConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
        });
    }

    public async Task<string?> GetConfigAsync(string key)
    {
        var config = await Configs.FirstOrDefaultAsync(c => c.Key == key);
        return config?.Value;
    }

    public async Task SetConfigAsync(string key, string value)
    {
        var config = await Configs.FirstOrDefaultAsync(c => c.Key == key);
        if (config != null)
        {
            config.Value = value;
        }
        else
        {
            Configs.Add(new BotConfig { Key = key, Value = value });
        }
        await SaveChangesAsync();
    }
}
