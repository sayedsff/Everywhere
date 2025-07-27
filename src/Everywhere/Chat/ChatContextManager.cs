using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Database;
using Everywhere.Enums;
using Everywhere.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Chat;

public class ChatContextManager(Settings settings, IChatDatabase chatDatabase) : ObservableObject, IChatContextManager, IAsyncInitializer
{
    public NotifyCollectionChangedSynchronizedViewList<ChatMessageNode> ChatMessageNodes =>
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
            Debug.Assert(history.ContainsKey(value), "The value must be part of the history.");

            var previous = current;
            if (!SetProperty(ref current, value)) return;
            if (IsEmptyContext(previous)) Remove(previous);

            OnPropertyChanged();
            OnPropertyChanged(nameof(ChatMessageNodes));
        }
    }

    public IEnumerable<ChatContextHistory> History
    {
        get
        {
            var currentDate = DateTimeOffset.UtcNow;
            return history.Keys.GroupBy(c => (currentDate - c.Metadata.DateModified).TotalDays switch
            {
                < 1 => HumanizedDate.Today,
                < 2 => HumanizedDate.Yesterday,
                < 7 => HumanizedDate.LastWeek,
                < 30 => HumanizedDate.LastMonth,
                < 365 => HumanizedDate.LastYear,
                _ => HumanizedDate.Earlier
            }).Select(g => new ChatContextHistory(
                g.Key,
                g.AsValueEnumerable().OrderByDescending(c => c.Metadata.DateModified).ToImmutableArray())
            );
        }
    }

    [field: AllowNull, MaybeNull]
    public IRelayCommand CreateNewCommand =>
        field ??= new RelayCommand(CreateNew, () => !IsEmptyContext(current));

    private readonly Dictionary<ChatContext, ChatContextDbItem> history = [];

    private ChatContext? current;

    [MemberNotNull(nameof(current))]
    private void CreateNew()
    {
        if (IsEmptyContext(current)) return;

        var renderedSystemPrompt = Prompts.RenderPrompt(
            Prompts.DefaultSystemPrompt,
            new Dictionary<string, Func<string>>
            {
                { "OS", () => Environment.OSVersion.ToString() },
                { "Time", () => DateTime.Now.ToString("F") },
                { "SystemLanguage", () => settings.Common.Language },
            });

        current = new ChatContext(renderedSystemPrompt);
        var dbItem = new ChatContextDbItem(current);
        history.Add(current, dbItem);
        Task.Run(() => chatDatabase.AddChatContext(dbItem)).Detach();

        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(ChatMessageNodes));
    }

    public void UpdateHistory()
    {
        OnPropertyChanged(nameof(History));
    }

    public void Remove(ChatContext chatContext)
    {
        if (!history.Remove(chatContext, out var dbItem)) return;

        Task.Run(() => chatDatabase.RemoveChatContext(dbItem)).Detach();

        // If the current chat context is being removed, we need to set a new current context
        if (ReferenceEquals(chatContext, current))
        {
            if (history.Keys.OrderByDescending(c => c.Metadata.DateModified).FirstOrDefault() is { } historyItem)
            {
                Current = historyItem;
            }
            else
            {
                // If no other chat context exists, create a new one
                CreateNew();
            }
        }

        OnPropertyChanged(nameof(History));
    }

    public int Priority => 10;

    public Task InitializeAsync() => Task.Run(() =>
    {
        foreach (var chatContext in chatDatabase.QueryChatContexts(q => q.OrderByDescending(c => c.DateModified)))
        {
            current ??= chatContext.Value;
            history.Add(chatContext.Value, chatContext);
        }
    });

    private static bool IsEmptyContext([NotNullWhen(true)] ChatContext? chatContext) => chatContext is { MessageCount: 1 };
}