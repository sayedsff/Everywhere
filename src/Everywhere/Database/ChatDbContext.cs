using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Everywhere.Models;
using Everywhere.Utils;
using Microsoft.EntityFrameworkCore;

namespace Everywhere.Database;

public class ChatDbContext(IRuntimeConstantProvider runtimeConstantProvider) : DbContext, IAsyncInitializer
{
    public required DbSet<ChatContextDbItem> ChatContextDbSet { get; init; }

    private readonly DebounceHelper debounceHelper = new(TimeSpan.FromSeconds(0.5));

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var dbPath = runtimeConstantProvider.GetDatabasePath("ChatContext.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatContextDbItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SerializedData).IsRequired();
            entity.Property(e => e.DateModified).HasConversion<long>().IsRequired();
            entity.HasIndex(e => e.DateModified);
        });
    }

    public int Priority => 100;

    public Task InitializeAsync() => Task.Run(async () =>
    {
        await Database.EnsureCreatedAsync();

        TrackableObject.AddPropertyChangedEventHandler(
            nameof(ChatContext),
            (sender, _) => debounceHelper.Execute(() => Task.Run(() =>
            {
                Debug.Assert(sender is ChatContext, "Sender must be of type ChatContext");
                if (sender is not ChatContext chatContext) return;

                var dbItem = ChatContextDbSet.FirstOrDefault(c => c.Id == chatContext.DbId);
                if (dbItem is null)
                {
                    dbItem = new ChatContextDbItem(chatContext);
                    ChatContextDbSet.Add(dbItem);
                }
                else
                {
                    dbItem.Item = chatContext;
                }
                SaveChanges();
            })));
    });
}

public class ChatContextDbItem : MessagePackDbItem<ChatContext>
{
    [NotMapped]
    public sealed override ChatContext Item
    {
        get
        {
            var result = base.Item;
            result.DbId = Id;
            return result;
        }
        set
        {
            base.Item = value;
            DateModified = value.Metadata.DateModified;
        }
    }

    public DateTimeOffset DateModified { get; set; }

    public ChatContextDbItem() { }

    [SetsRequiredMembers]
    public ChatContextDbItem(ChatContext item) : base(item) { }
}