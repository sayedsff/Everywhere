using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.AI;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Storage;
using Everywhere.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            if (_current is not null) return _current;

            CreateNew();
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

            // Update active state
            _current?.VisualElements.IsActive = true;
            previous?.VisualElements.IsActive = false;

            // BUG:
            // IDK why if I remove the previous context immediately,
            // Avalonia will fuck up and crash immediately with IndexOutOfRangeException.
            // The whole call stack is inside Avalonia, so I can't do anything about it.
            // The only workaround is to invoke the removal on the UI thread with a delay.
            Dispatcher.UIThread.InvokeAsync(
                () =>
                {
                    RemoveCommand.NotifyCanExecuteChanged();
                    CreateNewCommand.NotifyCanExecuteChanged();

                    if (IsEmptyContext(previous)) Remove(previous);
                },
                DispatcherPriority.Background);

            OnPropertyChanged(nameof(ChatMessageNodes));
        }
    }

    public IReadOnlyList<ChatContextHistory> History
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
            ).ToReadOnlyList();
        }
    }

    public IReadOnlyDictionary<string, Func<string>> SystemPromptVariables =>
        ImmutableDictionary.CreateRange(
            new KeyValuePair<string, Func<string>>[]
            {
                new("Time", () => DateTime.Now.ToString("F")),
                new("OS", () => Environment.OSVersion.ToString()),
                new("SystemLanguage", () => _settings.Common.Language == "default" ? "en-US" : _settings.Common.Language),
                new("WorkingDirectory", () => _runtimeConstantProvider.EnsureWritableDataFolderPath($"plugins/{DateTime.Now:yyyy-MM-dd}"))
            });

    [field: AllowNull, MaybeNull]
    public IRelayCommand CreateNewCommand =>
        field ??= new RelayCommand(CreateNew, () => !IsEmptyContext(_current));

    private ChatContext? _current;

    private readonly Dictionary<Guid, ChatContext> _history = [];
    private readonly HashSet<ChatContext> _saveBuffer = [];
    private readonly Settings _settings;
    private readonly IChatContextStorage _chatContextStorage;
    private readonly IRuntimeConstantProvider _runtimeConstantProvider;
    private readonly ILogger<ChatContextManager> _logger;
    private readonly DebounceExecutor<ChatContextManager> _saveDebounceExecutor;

    public ChatContextManager(
        Settings settings,
        IChatContextStorage chatContextStorage,
        IRuntimeConstantProvider runtimeConstantProvider,
        ILogger<ChatContextManager> logger)
    {
        _settings = settings;
        _chatContextStorage = chatContextStorage;
        _runtimeConstantProvider = runtimeConstantProvider;
        _logger = logger;
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
                Task.WhenAll(toSave.AsValueEnumerable().Select(c => that._chatContextStorage.SaveChatContextAsync(c)).ToList())
                    .Detach(that._logger.ToExceptionHandler());
            },
            TimeSpan.FromSeconds(0.5)
        );
    }

    private void HandleChatContextChanged(ChatContext sender)
    {
        lock (_saveBuffer) _saveBuffer.Add(sender);
        _saveDebounceExecutor.Trigger();

        Dispatcher.UIThread.InvokeOnDemand(CreateNewCommand.NotifyCanExecuteChanged);
    }

    [MemberNotNull(nameof(_current))]
    private void CreateNew()
    {
        if (IsEmptyContext(_current)) return;

        var renderedSystemPrompt = Prompts.RenderPrompt(
            _settings.Model.SelectedCustomAssistant?.SystemPrompt ?? Prompts.DefaultSystemPrompt,
            SystemPromptVariables
        );

        _current = new ChatContext(renderedSystemPrompt);
        _current.Changed += HandleChatContextChanged;
        _history.Add(_current.Metadata.Id, _current);
        Task.Run(() => _chatContextStorage.AddChatContextAsync(_current)).Detach(_logger.ToExceptionHandler());

        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(ChatMessageNodes));
        RemoveCommand.NotifyCanExecuteChanged();
        CreateNewCommand.NotifyCanExecuteChanged();
    }

    private bool CanRemove => _history.Count > 1 || !IsEmptyContext(_current);

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove(ChatContext chatContext)
    {
        if (!_history.Remove(chatContext.Metadata.Id)) return;

        chatContext.Changed -= HandleChatContextChanged;
        Task.Run(() => _chatContextStorage.DeleteChatContextAsync(chatContext.Metadata.Id)).Detach(_logger.ToExceptionHandler());

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
        RemoveCommand.NotifyCanExecuteChanged();
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
        var newItems = new HashSet<Guid>();
        await foreach (var metadata in _chatContextStorage.QueryChatContextsAsync(count, ChatContextOrderBy.UpdatedAt, true))
        {
            newItems.Add(metadata.Id);

            if (_history.ContainsKey(metadata.Id))
            {
                continue;
            }

            try
            {
                var chatContext = await _chatContextStorage.GetChatContextAsync(metadata.Id).ConfigureAwait(false);
                chatContext.Changed += HandleChatContextChanged;
                _current ??= chatContext;
                _history.Add(metadata.Id, chatContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load chat context {ChatContextId}, auto delete", metadata.Id);

                await _chatContextStorage.DeleteChatContextAsync(metadata.Id).ConfigureAwait(false);
            }
        }

        // Remove any chat contexts that are no longer in the storage
        foreach (var (_, oldItem) in _history.AsValueEnumerable().Where(kv => !newItems.Contains(kv.Key)).ToList())
        {
            Remove(oldItem);
        }

        OnPropertyChanged(nameof(History));
        RemoveCommand.NotifyCanExecuteChanged();
    });

    public AsyncInitializerPriority Priority => AsyncInitializerPriority.Startup;

    public Task InitializeAsync() => UpdateHistoryAsync(8);

    private static bool IsEmptyContext([NotNullWhen(true)] ChatContext? chatContext) => chatContext is { Count: 1 };
}

public static class ChatContextManagerExtension
{
    public static IServiceCollection AddChatContextManager(this IServiceCollection services)
    {
        services.AddSingleton<ChatContextManager>();
        services.AddSingleton<IChatContextManager>(x => x.GetRequiredService<ChatContextManager>());
        services.AddTransient<IAsyncInitializer>(x => x.GetRequiredService<ChatContextManager>());
        return services;
    }
}