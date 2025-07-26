using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Everywhere.Models;
using Everywhere.Utils;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace Everywhere.Database;

public interface IChatDatabase
{
    /// <summary>
    /// Adds a new chat context to the database and tracks it for changes.
    /// </summary>
    /// <param name="chatContext"></param>
    void AddChatContext(ChatContextDbItem chatContext);

    /// <summary>
    /// Removes a chat context from the database. This will also remove the context from tracking.
    /// </summary>
    /// <param name="chatContext"></param>
    void RemoveChatContext(ChatContextDbItem chatContext);

    IEnumerable<ChatContextDbItem> QueryChatContexts(Func<IQueryable<ChatContextDbItem>, IQueryable<ChatContextDbItem>> queryBuilder);
}

public class ChatDbContext(IRuntimeConstantProvider runtimeConstantProvider) : DbContext, IChatDatabase, IAsyncInitializer
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
            entity.Property(e => e.SerializedData).HasColumnType("BLOB").IsRequired();
            entity.Property(e => e.DateModified).HasConversion<long>().IsRequired();
            entity.HasIndex(e => e.DateModified);
        });
    }

    public void AddChatContext(ChatContextDbItem chatContext)
    {
        ChatContextDbSet.Add(chatContext);
        SaveChanges();
    }

    public void RemoveChatContext(ChatContextDbItem chatContext)
    {
        ChatContextDbSet.Remove(chatContext);
        SaveChanges();
    }

    public IEnumerable<ChatContextDbItem> QueryChatContexts(Func<IQueryable<ChatContextDbItem>, IQueryable<ChatContextDbItem>> queryBuilder)
    {
        return [..queryBuilder(ChatContextDbSet)];
    }

    public int Priority => 100;

    public Task InitializeAsync() => Task.Run(async () =>
    {
        await Database.EnsureCreatedAsync();

        TrackableObject<ChatContextDbItem>.AddPropertyChangedEventHandler(
            (sender, _) => debounceHelper.Execute(() => Task.Run(() =>
            {
                sender.Value = sender.Value; // re-serialize the value to update the database
                SaveChanges();
            })));
    });
}

public class ChatContextDbItem : TrackableObject<ChatContextDbItem>
{
    [System.ComponentModel.DataAnnotations.Key]
    public int Id { get; init; }

    public byte[] SerializedData { get; set; }

    [NotMapped]
    [field: AllowNull, MaybeNull]
    public required ChatContext Value
    {
        get
        {
            if (field is not null) return field;

            Subscribe(field = MessagePackSerializer.Deserialize<ChatContext>(SerializedData));
            return field;
        }

        [MemberNotNull(nameof(SerializedData))]
        set
        {
            if (field is null)
            {
                Subscribe(field = value);
            }
            else if (!ReferenceEquals(value, field))
            {
                throw new InvalidOperationException("Cannot change the value of an already initialized ChatContextDbItem.");
            }

            DateModified = value.Metadata.DateModified;
            SerializedData = MessagePackSerializer.Serialize(value);
        }
    }

    public DateTimeOffset DateModified { get; set; }

    public ChatContextDbItem() { }

    [SetsRequiredMembers]
    public ChatContextDbItem(ChatContext value) : this()
    {
        Value = value;
    }

    private void Subscribe(ChatContext value)
    {
        Debug.Assert(
            isTrackingEnabled == false,
            "Tracking should not be enabled when subscribing. This indicates a duplicate subscription.");

        // Subscribe to property changes of the ChatContext.Metadata
        // This actually tracks changes to the DateModified property
        value.Metadata.PropertyChanged += HandleValueChanged;
        isTrackingEnabled = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void HandleValueChanged(object? sender, PropertyChangedEventArgs e)
    {
        NotifyHandlers(e);
    }
}