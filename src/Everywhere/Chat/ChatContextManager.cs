using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Enums;
using Everywhere.Models;
using Everywhere.Utilities;
using Microsoft.SemanticKernel.ChatCompletion;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Chat;

public partial class ChatContextManager : ObservableObject, IChatContextManager, IAsyncInitializer
{
    public NotifyCollectionChangedSynchronizedViewList<ChatMessageNode> ChatMessageNodes =>
        Current.ToNotifyCollectionChanged(
            v => v.AttachFilter(m => m.Message.Role != AuthorRole.System),
            SynchronizationContextCollectionEventDispatcher.Current);

    public ChatContext Current
    {
        get
        {
            if (_current is null) CreateNew();
            _current.Changed += HandleChatContextChanged;
            return _current;
        }
        set
        {
            if (value.Metadata.Id == Guid.Empty)
                throw new ArgumentException("The provided chat context does not have a valid ID.", nameof(value));

            if (!_history.ContainsKey(value.Metadata.Id))
                throw new ArgumentException("The provided chat context is not part of the history.", nameof(value));

            var previous = _current;
            if (!SetProperty(ref _current, value)) return;

            if (previous is not null) previous.Changed -= HandleChatContextChanged;
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
            return _history.Values.GroupBy(c => (currentDate - c.Metadata.DateModified).TotalDays switch
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
        field ??= new RelayCommand(CreateNew, () => !IsEmptyContext(_current));

    private ChatContext? _current;

    private readonly Dictionary<Guid, ChatContext> _history = [];
    private readonly HashSet<ChatContext> _saveBuffer = [];
    private readonly Settings _settings;
    private readonly IChatContextStorage _chatContextStorage;
    private readonly DebounceExecutor<ChatContextManager> _saveDebounceExecutor;

    public ChatContextManager(Settings settings, IChatContextStorage chatContextStorage)
    {
        _settings = settings;
        _chatContextStorage = chatContextStorage;
        _saveDebounceExecutor = new DebounceExecutor<ChatContextManager>(
            () => this,
            static that =>
            {
                ChatContext[] toSave;
                lock (that._saveBuffer)
                {
                    toSave = that._saveBuffer.ToArray();
                    that._saveBuffer.Clear();
                }
                Task.WhenAll(toSave.AsValueEnumerable().Select(c => that._chatContextStorage.SaveChatContextAsync(c)).ToArray()).Detach();
            },
            TimeSpan.FromSeconds(0.5)
        );
    }

    private void HandleChatContextChanged(ChatContext sender)
    {
        lock (_saveBuffer) _saveBuffer.Add(sender);
        _saveDebounceExecutor.Trigger();
    }

    [MemberNotNull(nameof(_current))]
    private void CreateNew()
    {
        if (IsEmptyContext(_current)) return;

        var renderedSystemPrompt = Prompts.RenderPrompt(
            Prompts.DefaultSystemPrompt,
            new Dictionary<string, Func<string>>
            {
                { "OS", () => Environment.OSVersion.ToString() },
                { "Time", () => DateTime.Now.ToString("F") },
                { "SystemLanguage", () => _settings.Common.Language },
            });

        _current = new ChatContext(renderedSystemPrompt);
        _history.Add(_current.Metadata.Id, _current);
        Task.Run(() => _chatContextStorage.AddChatContextAsync(_current)).Detach();

        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(ChatMessageNodes));
    }

    [RelayCommand]
    private void Remove(ChatContext chatContext)
    {
        if (!_history.Remove(chatContext.Metadata.Id)) return;

        Task.Run(() => _chatContextStorage.DeleteChatContextAsync(chatContext.Metadata.Id)).Detach();

        // If the current chat context is being removed, we need to set a new current context
        if (ReferenceEquals(chatContext, _current))
        {
            if (_history.Values.OrderByDescending(c => c.Metadata.DateModified).FirstOrDefault() is { } historyItem)
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

    [RelayCommand]
    private void Rename(ChatContext chatContext)
    {
        foreach (var other in _history.Values) other.IsRenamingMetadataTitle = false;
        chatContext.IsRenamingMetadataTitle = true;
    }

    public void UpdateHistory()
    {
        UpdateHistoryAsync(int.MaxValue).Detach();
    }

    private Task UpdateHistoryAsync(int count) => Task.Run(async () =>
    {
        await foreach (var metadata in _chatContextStorage.QueryChatContextsAsync(count, ChatContextOrderBy.UpdatedAt, true))
        {
            if (_history.ContainsKey(metadata.Id))
            {
                continue;
            }

            var chatContext = await _chatContextStorage.GetChatContextAsync(metadata.Id);
            _current ??= chatContext;
            _history.Add(metadata.Id, chatContext);
        }

        OnPropertyChanged(nameof(History));
    });

    public int Priority => 10;

    public Task InitializeAsync() => UpdateHistoryAsync(8);

    private static bool IsEmptyContext([NotNullWhen(true)] ChatContext? chatContext) => chatContext is { Count: 1 };
}