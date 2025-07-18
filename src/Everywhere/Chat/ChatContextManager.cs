using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Database;
using Everywhere.Enums;
using Everywhere.Models;
using MessagePack;
using Microsoft.SemanticKernel.ChatCompletion;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Chat;

public class ChatContextManager(Settings settings, ChatDbContext dbContext) : ObservableObject, IChatContextManager, IAsyncInitializer
{
    public INotifyCollectionChangedSynchronizedViewList<ChatMessageNode> ChatMessageNodes =>
        Current.ToNotifyCollectionChanged(
            v => v.AttachFilter(m => m.Message.Role != AuthorRole.System),
            SynchronizationContextCollectionEventDispatcher.Current);

    public ChatContext Current
    {
        get
        {
            if (current is not null) return current;
            CreateNew();
            return current;
        }
        set
        {
            if (!SetProperty(ref current, value)) return;
            history.Add(value);
            OnPropertyChanged(nameof(ChatMessageNodes));
        }
    }

    public IEnumerable<ChatContextHistory> History
    {
        get
        {
            var currentDate = DateTimeOffset.UtcNow;
            return history.GroupBy(c => (currentDate - c.Metadata.DateModified).TotalDays switch
            {
                < 1 => HumanizedDate.Today,
                < 2 => HumanizedDate.Yesterday,
                < 7 => HumanizedDate.LastWeek,
                < 30 => HumanizedDate.LastMonth,
                < 365 => HumanizedDate.LastYear,
                _ => HumanizedDate.Earlier
            }).Select(
                g => new ChatContextHistory(
                    g.Key,
                    g.AsValueEnumerable().OrderByDescending(c => c.Metadata.DateModified).ToImmutableArray())
            );
        }
    }

    private readonly HashSet<ChatContext> history = [];

    private ChatContext? current;

    [MemberNotNull(nameof(current))]
    public void CreateNew()
    {
        if (current is { MessageCount: 1 }) return;

        var renderedSystemPrompt = Prompts.RenderPrompt(
            Prompts.DefaultSystemPrompt,
            new Dictionary<string, Func<string>>
            {
                { "OS", () => Environment.OSVersion.ToString() },
                { "Time", () => DateTime.Now.ToString("F") },
                { "SystemLanguage", () => settings.Common.Language },
            });

        current = new ChatContext(renderedSystemPrompt);
        history.Add(current);
        OnPropertyChanged(nameof(ChatMessageNodes));

        Task.Run(() =>
        {
            dbContext.ChatContextDbSet.Add(new ChatContextDbItem(current));
            dbContext.SaveChanges();
        }).Detach();
    }

    public void UpdateHistory()
    {
        OnPropertyChanged(nameof(History));
    }

    public int Priority => 10;

    public Task InitializeAsync() => Task.Run(() =>
    {
        foreach (var chatContextDbItem in dbContext.ChatContextDbSet.OrderByDescending(c => c.DateModified).ToImmutableArray())
        {
            try
            {
                var item = chatContextDbItem.Item;
                current ??= item;
                history.Add(item);
            }
            catch (MessagePackSerializationException)
            {
                dbContext.ChatContextDbSet.Remove(chatContextDbItem);
            }
        }

        dbContext.SaveChanges();
    });
}