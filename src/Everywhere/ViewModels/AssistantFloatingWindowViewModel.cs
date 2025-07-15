using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Assistant;
using Everywhere.Models;
using Everywhere.Utils;
using Lucide.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web.Brave;
using Microsoft.SemanticKernel.TextGeneration;
using ObservableCollections;
using ZLinq;
using ChatMessage = Everywhere.Models.ChatMessage;
using Prompts = Everywhere.Assistant.Prompts;

namespace Everywhere.ViewModels;

public partial class AssistantFloatingWindowViewModel : BusyViewModelBase
{
    public Settings Settings { get; }

    [ObservableProperty]
    public partial bool IsOpened { get; set; }

    [ObservableProperty]
    public partial PixelRect TargetBoundingRect { get; private set; }

    [ObservableProperty]
    public partial DynamicResourceKey? Title { get; private set; }

    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<ChatAttachment> ChatAttachments =>
        field ??= chatAttachments.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    [ObservableProperty]
    public partial IReadOnlyList<DynamicNamedCommand>? QuickActions { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ChatCommand>? ChatCommands { get; private set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public NotifyCollectionChangedSynchronizedViewList<ChatMessageNode>? ChatMessageNodes =>
        ChatContext?.ToNotifyCollectionChanged(
            v => v.AttachFilter(m => m.Message.Role != AuthorRole.System),
            SynchronizationContextCollectionEventDispatcher.Current);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChatMessageNodes))]
    public partial ChatContext? ChatContext { get; private set; }

    private readonly IVisualElementContext visualElementContext;
    private readonly IClipboard clipboard;
    private readonly IStorageProvider storageProvider;
    private readonly IKernelMixinFactory kernelMixinFactory;

    private readonly ObservableList<ChatAttachment> chatAttachments = [];
    private readonly ReusableCancellationTokenSource cancellationTokenSource = new();

    public AssistantFloatingWindowViewModel(
        IVisualElementContext visualElementContext,
        IClipboard clipboard,
        IStorageProvider storageProvider,
        IKernelMixinFactory kernelMixinFactory,
        Settings settings)
    {
        this.visualElementContext = visualElementContext;
        this.clipboard = clipboard;
        this.storageProvider = storageProvider;
        this.kernelMixinFactory = kernelMixinFactory;

        Settings = settings;

        InitializeCommands();
    }

    private void InitializeCommands()
    {
        DynamicNamedCommand[] textEditActions =
        [
            new(
                LucideIconKind.Languages,
                "AssistantFloatingWindowViewModel_TextEditActions_Translate",
                null,
                ProcessChatMessageSentCommand,
                $"Translate the content in focused element to {new CultureInfo(Settings.Common.Language).Name}. " +
                $"If it's already in target language, translate it to English. " +
                $"You MUST only reply with the translated content, without any other text or explanation"
            ),
            new(
                LucideIconKind.StepForward,
                "AssistantFloatingWindowViewModel_TextEditActions_ContinueWriting",
                null,
                ProcessChatMessageSentCommand,
                "I have already written a beginning as the content of the focused element. " +
                "You MUST imitate my writing style and tone, then continue writing in my perspective. " +
                "You MUST only reply with the continue written content, without any other text or explanation"
            ),
            new(
                LucideIconKind.ScrollText,
                "AssistantFloatingWindowViewModel_TextEditActions_Summarize",
                null,
                ProcessChatMessageSentCommand,
                "Please summarize the content in focused element. " +
                "You MUST only reply with the summarize content, without any other text or explanation"
            )
        ];

        ChatCommand[] textEditCommands =
        [
            new(
                "/translate",
                "AssistantCommand_Translate_Description",
                "Based on context, translate the content of focused element into {0}",
                () => Settings.Common.Language),
            new(
                "/rewrite",
                "AssistantCommand_Rewrite_Description",
                "Based on context, rewrite the content of focused element"),
        ];

        void HandleChatAttachmentsCollectionChanged(in NotifyCollectionChangedEventArgs<ChatAttachment> x)
        {
            QuickActions = null;
            ChatCommands = null;

            if (chatAttachments.Count <= 0) return;

            switch (chatAttachments[0])
            {
                case ChatVisualElementAttachment { Element.Type: VisualElementType.TextEdit }:
                {
                    QuickActions = textEditActions;
                    ChatCommands = textEditCommands;
                    break;
                }
            }
        }

        chatAttachments.CollectionChanged += HandleChatAttachmentsCollectionChanged;
    }

    private CancellationTokenSource? targetElementChangedTokenSource;

    public async Task TryFloatToTargetElementAsync(IVisualElement? targetElement, bool showExpanded = false)
    {
        // debouncing
        if (targetElementChangedTokenSource is not null) await targetElementChangedTokenSource.CancelAsync();
        targetElementChangedTokenSource = new CancellationTokenSource();
        var cancellationToken = targetElementChangedTokenSource.Token;
        try
        {
            await Task.Delay(100, cancellationToken);
        }
        catch (OperationCanceledException) { }

        await ExecuteBusyTaskAsync(
            async token =>
            {
                if (chatAttachments.Any(a => a is ChatVisualElementAttachment vea && vea.Element.Equals(targetElement)))
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
                Title = "AssistantFloatingWindow_Title";
                chatAttachments.Clear();
                chatAttachments.Add(await Task.Run(() => CreateFromVisualElement(targetElement), token));
                IsOpened = true;
                IsExpanded = showExpanded;
            },
            flags: ExecutionFlags.EnqueueIfBusy,
            cancellationToken: cancellationToken);
    }

    [RelayCommand]
    private async Task AddElementAsync(PickElementMode mode)
    {
        if (await visualElementContext.PickElementAsync(mode) is not { } element) return;
        if (chatAttachments.OfType<ChatVisualElementAttachment>().Any(a => a.Element.Id == element.Id)) return;
        chatAttachments.Add(await Task.Run(() => CreateFromVisualElement(element)));
    }

    [RelayCommand]
    private async Task AddClipboardAsync()
    {
        var formats = await clipboard.GetFormatsAsync();
        if (formats.Length == 0)
        {
            Console.WriteLine("Clipboard is empty."); // TODO: logging
            return;
        }

        if (formats[0] == DataFormats.Text)
        {

        }
    }

    [RelayCommand]
    private async Task AddFileAsync()
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions());
        if (files.Count <= 0) return;
        if (files[0].TryGetLocalPath() is not { } filePath)
        {
            Console.WriteLine("File path is not available."); // TODO: logging
            return;
        }

        AddFile(filePath);
    }

    private void AddFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File not found: {filePath}"); // TODO: logging
            return;
        }

        ChatAttachments.Add(new ChatFileAttachment(new DirectResourceKey(Path.GetFileName(filePath)), filePath));
    }

    private static ChatVisualElementAttachment CreateFromVisualElement(IVisualElement element)
    {
        DynamicResourceKey headerKey;

        if (element.ProcessId != 0)
        {
            using var process = Process.GetProcessById(element.ProcessId);
            headerKey = new FormattedDynamicResourceKey($"AssistantVisualElementAttachment_Header_WithProcess_{element.Type}", process.ProcessName);
        }
        else
        {
            headerKey = new DynamicResourceKey($"AssistantVisualElementAttachment_Header_{element.Type}");
        }

        return new ChatVisualElementAttachment(
            headerKey,
            element.Type switch
            {
                VisualElementType.Label => LucideIconKind.Type,
                VisualElementType.TextEdit => LucideIconKind.TextCursorInput,
                VisualElementType.Document => LucideIconKind.FileText,
                VisualElementType.Image => LucideIconKind.Image,
                VisualElementType.Screen => LucideIconKind.Monitor,
                VisualElementType.TopLevel => LucideIconKind.AppWindow,
                _ => LucideIconKind.Component
            },
            element);
    }

    [RelayCommand]
    private Task ProcessChatMessageSentAsync(string message) => ExecuteBusyTaskAsync(
        async cancellationToken =>
        {
            message = message.Trim();
            if (message.Length == 0) return;

            CanCancel = true;
            try
            {
                if (ChatContext is not { } chatContext) // new chat, add system prompt
                {
                    var renderedSystemPrompt = Prompts.RenderPrompt(
                        Prompts.DefaultSystemPrompt,
                        new Dictionary<string, Func<string>>
                        {
                            { "OS", () => Environment.OSVersion.ToString() },
                            { "Time", () => DateTime.Now.ToString("F") },
                            { "SystemLanguage", () => new CultureInfo(Settings.Common.Language).DisplayName },
                        });

                    ChatContext = chatContext = new ChatContext(renderedSystemPrompt);
                }

                UserChatMessage? userMessage = null;
                if (message[0] == '/')
                {
                    var commandString = message.IndexOf(' ') is var index and > 0 ? message[..index] : message;
                    if (ChatCommands?.FirstOrDefault(c => c.Command.Equals(commandString, StringComparison.OrdinalIgnoreCase)) is { } command)
                    {
                        var commandArgument = message[commandString.Length..].Trim();
                        if (commandArgument.Length == 0)
                        {
                            commandArgument = command.DefaultValueFactory?.Invoke() ?? string.Empty;
                        }
                        var userPrompt = string.Format(command.UserPrompt, commandArgument);
                        userMessage = new UserChatMessage(userPrompt, chatAttachments.AsValueEnumerable().ToList())
                        {
                            Inlines =
                            {
                                new Run(commandString) { TextDecorations = TextDecorations.Underline },
                                new Run(' ' + commandArgument)
                            }
                        };
                    }
                }

                userMessage ??= new UserChatMessage(message, chatAttachments.AsValueEnumerable().ToList())
                {
                    Inlines = { message }
                };
                chatContext.Add(userMessage);
                chatAttachments.Clear();

                var kernelMixin = kernelMixinFactory.Create();
                await ProcessUserChatMessageAsync(userMessage, kernelMixin, cancellationToken);
                var assistantChatMessage = new AssistantChatMessage { IsBusy = true };
                chatContext.Add(assistantChatMessage);
                await GenerateAsync(kernelMixin, chatContext, assistantChatMessage, cancellationToken);
            }
            finally
            {
                CanCancel = false;
            }
        },
        cancellationToken: cancellationTokenSource.Token);

    [RelayCommand]
    private Task RetryAsync(ChatMessageNode chatMessageNode)
    {
        if (chatMessageNode.Message.Role != AuthorRole.Assistant)
        {
            return Task.FromException(new InvalidOperationException("Only assistant messages can be retried."));
        }
        if (ChatContext is not { } chatContext)
        {
            return Task.FromException(new InvalidOperationException("ChatContext is not initialized."));
        }

        return ExecuteBusyTaskAsync(
            cancellationToken =>
            {
                var assistantChatMessage = new AssistantChatMessage { IsBusy = true };
                chatContext.CreateBranchOn(chatMessageNode, assistantChatMessage);
                return GenerateAsync(kernelMixinFactory.Create(), chatContext, assistantChatMessage, cancellationToken);
            },
            cancellationToken: cancellationTokenSource.Token);
    }

    [RelayCommand]
    private Task CopyAsync(ChatMessage chatMessage) => clipboard.SetTextAsync(chatMessage.ToString());

    public bool CanCancel
    {
        get;
        private set
        {
            if (SetProperty(ref field, value)) CancelCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        cancellationTokenSource.Cancel();
    }

    [RelayCommand]
    private void Close()
    {
        IsOpened = false;
    }

    private void Reset()
    {
        cancellationTokenSource.Cancel();
        TargetBoundingRect = default;
        IsExpanded = false;
        QuickActions = [];
        ChatCommands = [];
    }

    private async Task ProcessUserChatMessageAsync(
        UserChatMessage userChatMessage,
        IKernelMixin kernelMixin,
        CancellationToken cancellationToken)
    {
        if (ChatContext is not { } chatContext) throw new InvalidOperationException("ChatContext is not initialized.");

        var elements = userChatMessage
            .Attachments
            .OfType<ChatVisualElementAttachment>()
            .Select(a => a.Element)
            .ToArray();

        if (elements.Length <= 0) return;

        var analyzingContextMessage = new ActionChatMessage(
            new AuthorRole("action"),
            LucideIconKind.TextSearch,
            "ActionChatMessage_Header_AnalyzingContext")
        {
            IsBusy = true
        };

        try
        {
            chatContext.Add(analyzingContextMessage);

            var xmlBuilder = new VisualElementXmlBuilder(
                elements,
                kernelMixin,
                kernelMixin.MaxTokenTotal / 20);
            var renderedVisualTreePrompt = await Task.Run(
                () =>
                    Prompts.RenderPrompt(
                        Prompts.VisualTreePrompt,
                        new Dictionary<string, Func<string>>
                        {
                            { "VisualTree", () => xmlBuilder.BuildXml(cancellationToken) },
                            { "FocusedElementId", () => xmlBuilder.GetIdMap(cancellationToken)[elements[0].Id].ToString() },
                        }),
                cancellationToken);
            userChatMessage.UserPrompt = renderedVisualTreePrompt + "\n\n" + userChatMessage.UserPrompt;
        }
        finally
        {
            analyzingContextMessage.IsBusy = false;
        }
    }

    private async Task GenerateAsync(IKernelMixin kernelMixin, ChatContext chatContext, AssistantChatMessage assistantChatMessage, CancellationToken cancellationToken)
    {
        // todo: maybe I need to move this builder to a better place (using DI)
        var modelSettings = Settings.Model;
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(kernelMixin);
        builder.Services.AddSingleton<ITextGenerationService>(kernelMixin);

        if (modelSettings is { IsWebSearchEnabled: true, WebSearchApiKey: { } webSearchApiKey } && !string.IsNullOrWhiteSpace(webSearchApiKey))
        {
            Uri.TryCreate(modelSettings.WebSearchEndpoint, UriKind.Absolute, out var webSearchEndPoint);
            builder.Plugins.AddFromObject(
                new WebSearchEnginePlugin(
                    modelSettings.WebSearchProvider switch
                    {
                        // TODO: "google" => new GoogleConnector(webSearchApiKey, webSearchEndPoint),
                        "brave" => new BraveConnector(webSearchApiKey, webSearchEndPoint),
                        "bocha" => new BoChaConnector(webSearchApiKey, webSearchEndPoint),
                        _ => new BingConnector(webSearchApiKey, webSearchEndPoint)
                    }),
                "web_search");
        }

        var kernel = builder.Build();

        var openAIPromptExecutionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = modelSettings.Temperature,
            TopP = modelSettings.TopP,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
        };
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var chatHistory = new ChatHistory(
            chatContext
                .Select(n => n.Message)
                .Where(m => !Equals(m, assistantChatMessage)) // exclude the current assistant message
                .Where(m => m.Role.Label is "system" or "assistant" or "user" or "tool")
                .Select(m => new ChatMessageContent(m.Role, m.ToString())));

        try
        {
            while (true)
            {
                AuthorRole? authorRole = null;
                var assistantContentBuilder = new StringBuilder();
                var functionCallContentBuilder = new FunctionCallContentBuilder();

                await foreach (var streamingContent in chatCompletionService.GetStreamingChatMessageContentsAsync(
                                   chatHistory,
                                   openAIPromptExecutionSettings,
                                   kernel,
                                   cancellationToken))
                {
                    if (streamingContent.Content is not null)
                    {
                        assistantContentBuilder.Append(streamingContent.Content);
                        assistantChatMessage.MarkdownBuilder.Append(streamingContent.Content);
                    }

                    authorRole ??= streamingContent.Role;
                    functionCallContentBuilder.Append(streamingContent);
                }

                chatHistory.AddAssistantMessage(assistantContentBuilder.ToString());

                var functionCalls = functionCallContentBuilder.Build();
                if (functionCalls.Count == 0) break;

                // TODO: tool calling
                var functionCallContent = new ChatMessageContent(authorRole ?? default, content: null);
                chatHistory.Add(functionCallContent);

                foreach (var functionCall in functionCalls)
                {
                    functionCallContent.Items.Add(functionCall);

                    var actionChatMessage = functionCall.PluginName switch
                    {
                        "web_search" => new ActionChatMessage(
                            AuthorRole.Tool,
                            LucideIconKind.Globe,
                            "ActionChatMessage_Header_WebSearching"),
                        _ => throw new NotImplementedException()
                    };
                    actionChatMessage.IsBusy = true;
                    chatContext.Insert(chatContext.MessageCount - 1, actionChatMessage);

                    try
                    {
                        var resultContent = await functionCall.InvokeAsync(kernel, cancellationToken);
                        var resultChatMessage = resultContent.ToChatMessage();
                        chatHistory.Add(resultChatMessage);
                        actionChatMessage.Content = resultChatMessage.Content;
                    }
                    catch (Exception ex)
                    {
                        chatHistory.Add(new FunctionResultContent(functionCall, $"Error: {ex.Message}").ToChatMessage());
                        // actionChatMessage.InlineCollection.Add($"Failed: {ex.Message}");
                    }
                    finally
                    {
                        actionChatMessage.IsBusy = false;
                    }
                }
            }
        }
        finally
        {
            assistantChatMessage.IsBusy = false;
        }
    }
}