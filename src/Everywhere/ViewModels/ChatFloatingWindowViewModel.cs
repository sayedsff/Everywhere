using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Enums;
using Everywhere.Models;
using Everywhere.Utilities;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using ObservableCollections;
using ZLinq;
using ChatMessage = Everywhere.Models.ChatMessage;

namespace Everywhere.ViewModels;

public partial class ChatFloatingWindowViewModel : BusyViewModelBase
{
    public Settings Settings { get; }

    public bool IsOpened
    {
        get;
        set
        {
            field = value;
            // notify property changed even if the value is the same
            // so that the view can update its visibility and topmost
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    public partial PixelRect TargetBoundingRect { get; private set; }

    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<ChatAttachment> ChatAttachments =>
        field ??= _chatAttachments.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    [ObservableProperty]
    public partial IReadOnlyList<DynamicNamedCommand>? QuickActions { get; private set; }

    public IChatContextManager ChatContextManager { get; }

    public IChatService ChatService { get; }

    private readonly IVisualElementContext _visualElementContext;
    private readonly INativeHelper _nativeHelper;
    private readonly ILogger<ChatFloatingWindowViewModel> _logger;

    private readonly ObservableList<ChatAttachment> _chatAttachments = [];
    private readonly ReusableCancellationTokenSource _cancellationTokenSource = new();

    public ChatFloatingWindowViewModel(
        IChatContextManager chatContextManager,
        IChatService chatService,
        Settings settings,
        IVisualElementContext visualElementContext,
        INativeHelper nativeHelper,
        ILogger<ChatFloatingWindowViewModel> logger)
    {
        ChatContextManager = chatContextManager;
        ChatService = chatService;
        Settings = settings;

        _visualElementContext = visualElementContext;
        _nativeHelper = nativeHelper;
        _logger = logger;

        InitializeCommands();
    }

    private void InitializeCommands()
    {
        DynamicNamedCommand[] textEditActions =
        [
            new(
                LucideIconKind.Languages,
                LocaleKey.ChatFloatingWindowViewModel_TextEditActions_Translate,
                null,
                SendMessageCommand,
                $"Translate the content in focused element to {GetLanguageDisplayName()}. " +
                $"If it's already in target language, translate it to English. " +
                $"You MUST only reply with the translated content, without any other text or explanation"
            ),
            new(
                LucideIconKind.StepForward,
                LocaleKey.ChatFloatingWindowViewModel_TextEditActions_ContinueWriting,
                null,
                SendMessageCommand,
                "I have already written a beginning as the content of the focused element. " +
                "You MUST imitate my writing style and tone, then continue writing in my perspective. " +
                "You MUST only reply with the continue written content, without any other text or explanation"
            ),
            new(
                LucideIconKind.ScrollText,
                LocaleKey.ChatFloatingWindowViewModel_TextEditActions_Summarize,
                null,
                SendMessageCommand,
                "Please summarize the content in focused element. " +
                "You MUST only reply with the summarize content, without any other text or explanation"
            )
        ];

        string GetLanguageDisplayName()
        {
            try
            {
                return Settings.Common.Language switch
                {
                    "default" => "English (United States)",
                    _ => new CultureInfo(Settings.Common.Language).DisplayName
                };
            }
            catch
            {
                return "English (United States)";
            }
        }

        void HandleChatAttachmentsCollectionChanged(in NotifyCollectionChangedEventArgs<ChatAttachment> x)
        {
            QuickActions = _chatAttachments switch
            {
                [ChatVisualElementAttachment { Element.Type: VisualElementType.TextEdit }] => textEditActions,
                _ => null
            };
        }

        _chatAttachments.CollectionChanged += HandleChatAttachmentsCollectionChanged;
    }

    private CancellationTokenSource? _targetElementChangedTokenSource;

    public async Task TryFloatToTargetElementAsync(IVisualElement? targetElement)
    {
        // debouncing
        if (_targetElementChangedTokenSource is not null) await _targetElementChangedTokenSource.CancelAsync();
        _targetElementChangedTokenSource = new CancellationTokenSource();
        var cancellationToken = _targetElementChangedTokenSource.Token;
        try
        {
            await Task.Delay(100, cancellationToken);
        }
        catch (OperationCanceledException) { }

        await ExecuteBusyTaskAsync(
            async token =>
            {
                if (_chatAttachments.Any(a => a is ChatVisualElementAttachment vea && vea.Element.Equals(targetElement)))
                {
                    IsOpened = true;
                    return;
                }

                Reset();

                if (targetElement == null)
                {
                    return;
                }

                TargetBoundingRect = targetElement.BoundingRectangle;
                _chatAttachments.Clear();
                _chatAttachments.Add(await Task.Run(() => CreateFromVisualElement(targetElement), token));
                IsOpened = true;
            },
            _logger.ToExceptionHandler(),
            ExecutionFlags.EnqueueIfBusy,
            cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task AddElementAsync(PickElementMode mode) => ExecuteBusyTaskAsync(
        async cancellationToken =>
        {
            if (_chatAttachments.Count >= Settings.Internal.MaxChatAttachmentCount) return;

            if (await _visualElementContext.PickElementAsync(mode) is not { } element) return;
            if (_chatAttachments.OfType<ChatVisualElementAttachment>().Any(a => a.Element.Id == element.Id)) return;
            _chatAttachments.Add(await Task.Run(() => CreateFromVisualElement(element), cancellationToken));
        },
        _logger.ToExceptionHandler());

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task AddClipboardAsync() => ExecuteBusyTaskAsync(
        async _ =>
        {
            if (_chatAttachments.Count >= Settings.Internal.MaxChatAttachmentCount) return;

            var formats = await Clipboard.GetFormatsAsync();
            if (formats.Length == 0)
            {
                _logger.LogInformation("Clipboard is empty.");
                return;
            }

            if (formats.Contains(DataFormats.Files))
            {
                var files = await Clipboard.GetDataAsync(DataFormats.Files);
                if (files is IEnumerable enumerable)
                {
                    foreach (var storageItem in enumerable.OfType<IStorageItem>())
                    {
                        var uri = storageItem.Path;
                        if (!uri.IsFile) break;
                        await AddFileUncheckAsync(uri.AbsolutePath);
                        if (_chatAttachments.Count >= Settings.Internal.MaxChatAttachmentCount) break;
                    }
                }
            }
            else if (Settings.Model.SelectedModelDefinition?.IsImageInputSupported.ActualValue is true)
            {
                if (await _nativeHelper.GetClipboardBitmapAsync() is not { } bitmap) return;

                _chatAttachments.Add(new ChatImageAttachment(DynamicResourceKey.Empty, await ResizeImageOnDemandAsync(bitmap)));
            }

            // TODO: add as text attachment when text is too long
            // else if (formats.Contains(DataFormats.Text))
            // {
            //     var text = await Clipboard.GetTextAsync();
            //     if (text.IsNullOrEmpty()) return;
            //
            //     chatAttachments.Add(new ChatTextAttachment(new DirectResourceKey(text.SafeSubstring(0, 10)), text));
            // }
        },
        _logger.ToExceptionHandler());

    private async static ValueTask<Bitmap> ResizeImageOnDemandAsync(Bitmap image, int maxWidth = 2560, int maxHeight = 2560)
    {
        if (image.PixelSize.Width <= maxWidth && image.PixelSize.Height <= maxHeight)
        {
            return image;
        }

        var scale = Math.Min(maxWidth / (double)image.PixelSize.Width, maxHeight / (double)image.PixelSize.Height);
        var newWidth = (int)(image.PixelSize.Width * scale);
        var newHeight = (int)(image.PixelSize.Height * scale);

        return await Task.Run(() => image.CreateScaledBitmap(new PixelSize(newWidth, newHeight)));
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private async Task AddFileAsync()
    {
        if (_chatAttachments.Count >= Settings.Internal.MaxChatAttachmentCount) return;

        IReadOnlyList<IStorageFile> files;
        IsOpened = false;
        try
        {
            files = await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = true,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Images")
                        {
                            Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"]
                        },
                        new FilePickerFileType("All Files")
                        {
                            Patterns = ["*"]
                        }
                    ]
                });
        }
        finally
        {
            IsOpened = true;
        }

        if (files.Count <= 0) return;
        if (files[0].TryGetLocalPath() is not { } filePath)
        {
            _logger.LogInformation("File path is not available.");
            return;
        }

        await AddFileUncheckAsync(filePath);
    }

    /// <summary>
    /// Add a file to the chat attachments without checking the attachment count limit.
    /// </summary>
    /// <param name="filePath"></param>
    private async ValueTask AddFileUncheckAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        if (!File.Exists(filePath))
        {
            _logger.LogInformation("File not found: {FilePath}", filePath);
            return;
        }

        var ext = Path.GetExtension(filePath).ToLower();
        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp")
        {
            if (Settings.Model.SelectedModelDefinition?.IsImageInputSupported.ActualValue is true)
            {
                try
                {
                    var bitmap = await Task.Run(() => new Bitmap(filePath));
                    _chatAttachments.Add(
                        new ChatImageAttachment(
                            new DirectResourceKey(Path.GetFileName(filePath)),
                            await ResizeImageOnDemandAsync(bitmap)));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load image from file: {FilePath}", filePath);
                }
            }
        }

        // TODO: 0.3.0
        // chatAttachments.Add(new ChatFileAttachment(new DirectResourceKey(Path.GetFileName(filePath)), filePath));
    }

    private static ChatVisualElementAttachment CreateFromVisualElement(IVisualElement element)
    {
        DynamicResourceKey headerKey;
        var elementTypeKey = new DynamicResourceKey($"VisualElementType_{element.Type}");
        if (element.ProcessId != 0)
        {
            using var process = Process.GetProcessById(element.ProcessId);
            headerKey = new FormattedDynamicResourceKey("{0} - {1}", new DirectResourceKey(process.ProcessName), elementTypeKey);
        }
        else
        {
            headerKey = elementTypeKey;
        }

        return new ChatVisualElementAttachment(
            headerKey,
            element.Type switch
            {
                VisualElementType.Label => LucideIconKind.Type,
                VisualElementType.TextEdit => LucideIconKind.TextCursorInput,
                VisualElementType.Document => LucideIconKind.FileText,
                VisualElementType.Image => LucideIconKind.Image,
                VisualElementType.CheckBox => LucideIconKind.SquareCheck,
                VisualElementType.RadioButton => LucideIconKind.CircleCheckBig,
                VisualElementType.ComboBox => LucideIconKind.ChevronDown,
                VisualElementType.ListView => LucideIconKind.List,
                VisualElementType.ListViewItem => LucideIconKind.List,
                VisualElementType.TreeView => LucideIconKind.ListTree,
                VisualElementType.TreeViewItem => LucideIconKind.ListTree,
                VisualElementType.DataGrid => LucideIconKind.Table,
                VisualElementType.DataGridItem => LucideIconKind.Table,
                VisualElementType.TabControl => LucideIconKind.LayoutPanelTop,
                VisualElementType.TabItem => LucideIconKind.LayoutPanelTop,
                VisualElementType.Table => LucideIconKind.Table,
                VisualElementType.TableRow => LucideIconKind.Table,
                VisualElementType.Menu => LucideIconKind.Menu,
                VisualElementType.MenuItem => LucideIconKind.Menu,
                VisualElementType.Slider => LucideIconKind.SlidersHorizontal,
                VisualElementType.ScrollBar => LucideIconKind.Settings2,
                VisualElementType.ProgressBar => LucideIconKind.Percent,
                VisualElementType.Panel => LucideIconKind.Group,
                VisualElementType.TopLevel => LucideIconKind.AppWindow,
                VisualElementType.Screen => LucideIconKind.Monitor,
                _ => LucideIconKind.Component
            },
            element);
    }

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task SendMessage(string message) => ExecuteBusyTaskAsync(
        async cancellationToken =>
        {
            message = message.Trim();
            if (message.Length == 0) return;

            var userMessage = new UserChatMessage(message, _chatAttachments.AsValueEnumerable().ToImmutableArray())
            {
                Inlines = { message }
            };
            _chatAttachments.Clear();

            await ChatService.SendMessageAsync(userMessage, cancellationToken);
        },
        _logger.ToExceptionHandler(),
        cancellationToken: _cancellationTokenSource.Token);

    [RelayCommand(CanExecute = nameof(IsNotBusy))]
    private Task RetryAsync(ChatMessageNode chatMessageNode) => ExecuteBusyTaskAsync(
        cancellationToken => ChatService.RetryAsync(chatMessageNode, cancellationToken),
        _logger.ToExceptionHandler(),
        cancellationToken: _cancellationTokenSource.Token);

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }

    [RelayCommand]
    private Task CopyAsync(ChatMessage chatMessage) => Clipboard.SetTextAsync(chatMessage.ToString());

    [RelayCommand]
    private void Close()
    {
        IsOpened = false;
    }

    private void Reset()
    {
        _cancellationTokenSource.Cancel();
        TargetBoundingRect = default;
        QuickActions = [];
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsBusy))
        {
            AddElementCommand.NotifyCanExecuteChanged();
            AddClipboardCommand.NotifyCanExecuteChanged();
            AddFileCommand.NotifyCanExecuteChanged();
            SendMessageCommand.NotifyCanExecuteChanged();
            RetryCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }
}