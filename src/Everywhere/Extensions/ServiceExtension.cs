using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Database;
using Everywhere.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Everywhere.Extensions;

public static class ServiceExtension
{
    public static IServiceCollection AddDatabaseAndStorage(this IServiceCollection services) =>
        services
            .AddDbContextFactory<ChatDbContext>((x, options) =>
            {
                var dbPath = x.GetRequiredService<IRuntimeConstantProvider>().GetDatabasePath("chat.db");
                options.UseSqlite($"Data Source={dbPath}");
            })
            .AddSingleton<IBlobStorage, BlobStorage>()
            .AddSingleton<IChatContextStorage, ChatContextStorage>()
            .AddTransient<IAsyncInitializer, ChatDbInitializer>();

    public static IServiceCollection AddTelemetry(this IServiceCollection services) =>
        services
            .AddOpenTelemetry()
            .WithLogging()
            .WithTracing()
            .WithMetrics()
            .Services;
}