#if DEBUG

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Everywhere.Database;

public class ChatDbContextFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    public ChatDbContext CreateDbContext(string[] args)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Everywhere",
            "db");
        Directory.CreateDirectory(dbPath);

        var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
        optionsBuilder.UseSqlite($"Data Source=${Path.Combine(dbPath, "chat.db")}");

        return new ChatDbContext(optionsBuilder.Options);
    }
}

#endif