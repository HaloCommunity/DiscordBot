using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DiscordBot.Core.Data;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// </summary>
public class HaloCommunityBotContextFactory : IDesignTimeDbContextFactory<HaloCommunityBotContext>
{
    public HaloCommunityBotContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HaloCommunityBotContext>();
        optionsBuilder.UseSqlite("Data Source=./halocommunitybot.db");
        return new HaloCommunityBotContext(optionsBuilder.Options);
    }
}
