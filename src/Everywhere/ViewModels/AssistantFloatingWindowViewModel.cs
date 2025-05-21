using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Assistant;
using Everywhere.Models;
using Everywhere.Utils;
using Everywhere.Views;
using Lucide.Avalonia;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web.Brave;
using ObservableCollections;
using ZLinq;
using ChatMessage = Everywhere.Models.ChatMessage;
using Prompts = Everywhere.Assistant.Prompts;

namespace Everywhere.ViewModels;

public partial class AssistantFloatingWindowViewModel : BusyViewModelBase
{
    public Settings Settings { get; }

    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    [ObservableProperty]
    public partial PixelRect TargetBoundingRect { get; private set; }

    [ObservableProperty]
    public partial DynamicResourceKey? Title { get; private set; }

    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<AssistantAttachment> Attachments =>
        field ??= attachments.ToNotifyCollectionChangedSlim(SynchronizationContextCollectionEventDispatcher.Current);

    [ObservableProperty]
    public partial IReadOnlyList<DynamicNamedCommand>? QuickActions { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<AssistantCommand>? AssistantCommands { get; private set; }

    public IReadOnlyList<DynamicNamedCommand> AddAttachmentCommands { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<ChatMessage> ChatMessages =>
        field ??= chatMessages.CreateView(x => x).With(v => v.AttachFilter(m => m.Role != AuthorRole.System))
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly IVisualElementContext visualElementContext;
    private readonly ObservableList<AssistantAttachment> attachments = [];
    private readonly ObservableList<ChatMessage> chatMessages = [];
    private readonly ReusableCancellationTokenSource cancellationTokenSource = new();

    public AssistantFloatingWindowViewModel(IVisualElementContext visualElementContext, Settings settings)
    {
        this.visualElementContext = visualElementContext;
        Settings = settings;

        AddAttachmentCommands =
        [
            new DynamicNamedCommand(
                LucideIconKind.Monitor,
                "AssistantFloatingWindowViewModel_AddAttachmentCommands_AddScreen",
                Command: AddElementCommand,
                CommandParameter: PickElementMode.Screen
            ),
            new DynamicNamedCommand(
                LucideIconKind.AppWindow,
                "AssistantFloatingWindowViewModel_AddAttachmentCommands_AddWindow",
                Command: AddElementCommand,
                CommandParameter: PickElementMode.Window
            ),
            new DynamicNamedCommand(
                LucideIconKind.SquareMousePointer,
                "AssistantFloatingWindowViewModel_AddAttachmentCommands_AddElement",
                Command: AddElementCommand,
                CommandParameter: PickElementMode.Element
            ),
        ];

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

        AssistantCommand[] textEditCommands =
        [
            new(
                "/translate",
                "AssistantCommand_Translate_Description",
                $$"""
                  {{Prompts.DefaultSystemPromptWithVisualTree}}

                  # Mission
                  Based on context, translate the content of focused element"
                  """,
                "Translate it into {0}",
                () => Settings.Common.Language),
            new(
                "/rewrite",
                "AssistantCommand_Rewrite_Description",
                $$"""
                  {{Prompts.DefaultSystemPromptWithVisualTree}}

                  # Mission
                  Based on context, rewrite the content of focused element"
                  """,
                "{0}",
                () => "Refine it"),
        ];

        attachments.CollectionChanged += (in args) =>
        {
            QuickActions = null;
            AssistantCommands = null;

            if (attachments.Count <= 0) return;

            switch (attachments[0])
            {
                case AssistantVisualElementAttachment { Element.Type: VisualElementType.TextEdit }:
                {
                    QuickActions = textEditActions;
                    AssistantCommands = textEditCommands;
                    break;
                }
            }
        };
    }

    private CancellationTokenSource? targetElementChangedTokenSource;

    [RelayCommand]
    public async Task TryFloatToTargetElementAsync(IVisualElement? targetElement)
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
            _ =>
            {
                if (attachments.Any(a => a is AssistantVisualElementAttachment vea && vea.Element.Equals(targetElement)))
                {
                    IsVisible = true;
                    return;
                }

                Reset();

                if (targetElement is not { Type: VisualElementType.TextEdit } ||
                    (targetElement.States & (
                        VisualElementStates.Offscreen |
                        VisualElementStates.Disabled |
                        VisualElementStates.ReadOnly |
                        VisualElementStates.Password)) != 0)
                {
                    IsVisible = false;
                    return;
                }

                using var process = Process.GetProcessById(targetElement.ProcessId);
                Title = "AssistantFloatingWindow_Title_NoonGreeting";

                TargetBoundingRect = targetElement.BoundingRectangle;
                attachments.Clear();
                attachments.Add(CreateFromVisualElement(targetElement));
                IsVisible = true;
            },
            flags: ExecutionFlags.EnqueueIfBusy,
            cancellationToken: cancellationToken);
    }

    [RelayCommand]
    private async Task AddElementAsync(PickElementMode mode)
    {
        if (await visualElementContext.PickElementAsync(mode) is not { } element) return;
        Attachments.Add(await Task.Run(() => CreateFromVisualElement(element)));
    }

    private static AssistantVisualElementAttachment CreateFromVisualElement(IVisualElement element)
    {
        DynamicResourceKey headerKey;

        if (element.ProcessId != 0)
        {
            using var process = Process.GetProcessById(element.ProcessId);
            headerKey = new FormattedDynamicResourceKey("", [process.ProcessName]);
        }
        else
        {
            headerKey = "";
        }

        return new AssistantVisualElementAttachment(
            element,
            element.Type switch
            {
                VisualElementType.Label => LucideIconKind.Type,
                VisualElementType.TextEdit => LucideIconKind.TextCursorInput,
                VisualElementType.Document => LucideIconKind.FileText,
                VisualElementType.Screen => LucideIconKind.Monitor,
                VisualElementType.TopLevel => LucideIconKind.AppWindow,
                _ => LucideIconKind.Component
            },
            headerKey);
    }

    private static readonly AuthorRole ActionRole = new("Action");

    [RelayCommand]
    private Task ProcessChatMessageSentAsync(string message) => ExecuteBusyTaskAsync(
        async cancellationToken =>
        {
            message = message.Trim();
            if (message.Length == 0) return;

            CanCancel = true;
            try
            {
                string? systemPrompt = null;
                ChatMessage? userMessage = null;

                if (message[0] == '/')
                {
                    var commandString = message.IndexOf(' ') is var index and > 0 ? message[..index] : message;
                    if (AssistantCommands?.FirstOrDefault(c => c.Command.Equals(commandString, StringComparison.OrdinalIgnoreCase)) is { } command)
                    {
                        systemPrompt = command.SystemPrompt;
                        var commandArgument = message[commandString.Length..].Trim();
                        if (commandArgument.Length == 0)
                        {
                            commandArgument = command.DefaultValueFactory?.Invoke() ?? string.Empty;
                        }
                        var userPrompt = string.Format(command.UserPrompt, commandArgument);
                        userMessage = new UserChatMessage(userPrompt)
                        {
                            InlineCollection =
                            {
                                new Run(commandString) { TextDecorations = TextDecorations.Underline },
                                new Run(' ' + commandArgument)
                            }
                        };
                    }
                }

                userMessage ??= new UserChatMessage(message) { InlineCollection = { message } };
                chatMessages.Add(userMessage);

                if (chatMessages.Count == 1) // new chat, add system prompt
                {
                    var elements = attachments
                        .AsValueEnumerable()
                        .OfType<AssistantVisualElementAttachment>()
                        .Select(vea => OptimizedVisualElement.Create(vea.Element))
                        .ToList();

                    string builtSystemPrompt;
                    if (elements.Count > 0)
                    {
                        systemPrompt ??=
                            $$"""
                              {{Prompts.DefaultSystemPromptWithVisualTree}}

                              # Mission
                              Based on context, answer the user's question or accomplish the user's request
                              """;

                        var analyzingContextMessage = new ActionChatMessage(
                            ActionRole,
                            LucideIconKind.TextSearch,
                            "ActionChatMessage_Header_AnalyzingContext");
                        chatMessages.Add(analyzingContextMessage);
                        analyzingContextMessage.IsBusy = true;
                        builtSystemPrompt = await Task.Run(() => BuildSystemPrompt(elements, systemPrompt, cancellationToken), cancellationToken);
                        analyzingContextMessage.IsBusy = false;
                    }
                    else
                    {
                        builtSystemPrompt = systemPrompt ??
                            $$"""
                              {{Prompts.DefaultSystemPrompt}}

                              # Mission
                              Answer the user's question or accomplish the user's request
                              """;
                    }

                    chatMessages.Insert(0, new SystemChatMessage(builtSystemPrompt));
                }

                await GenerateAsync(cancellationToken);
            }
            finally
            {
                CanCancel = false;
            }
        },
        cancellationToken: cancellationTokenSource.Token);

    [RelayCommand]
    private Task RetryAsync(ChatMessage chatMessage) => ExecuteBusyTaskAsync(
        cancellationToken =>
        {
            var index = chatMessages.IndexOf(chatMessage);
            if (index == -1) return Task.CompletedTask;
            chatMessages.RemoveRange(index, chatMessages.Count - index); // TODO: history tree
            return GenerateAsync(cancellationToken);
        },
        cancellationToken: cancellationTokenSource.Token);

    [RelayCommand]
    private static Task CopyAsync(ChatMessage chatMessage) =>
        ServiceLocator.Resolve<AssistantFloatingWindow>().Clipboard?.SetTextAsync(chatMessage.ToString()) ?? Task.CompletedTask;

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
        Reset();
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
    }

    private void Reset()
    {
        cancellationTokenSource.Cancel();
        IsExpanded = false;
        chatMessages.Clear();
        QuickActions = [];
        AssistantCommands = [];
    }

    private string BuildSystemPrompt(List<OptimizedVisualElement> elements, string systemPrompt, CancellationToken cancellationToken)
    {
        var xmlBuilder = new VisualElementXmlBuilder(elements);

        var renderedSystemPrompt = Prompts.RenderPrompt(
            systemPrompt,
            new Dictionary<string, Func<string>>
            {
                { "OS", () => Environment.OSVersion.ToString() },
                { "Time", () => DateTime.Now.ToString("F") },
                { "SystemLanguage", () => new CultureInfo(Settings.Common.Language).DisplayName },
                { "VisualTree", () => xmlBuilder.BuildXml(cancellationToken) },
                { "FocusedElementId", () => xmlBuilder.GetIdMap(cancellationToken)[elements[0]].ToString() },
            });

        Console.WriteLine($"SystemPrompt\n{renderedSystemPrompt}"); // TODO: logging

        return renderedSystemPrompt;
    }

    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        // todo: maybe I need to move this builder to a better place (using DI)
        var modelSettings = Settings.Model;
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: modelSettings.ModelName,
            endpoint: new Uri(modelSettings.Endpoint, UriKind.Absolute),
            apiKey: modelSettings.ApiKey);

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
            chatMessages
                .Where(m => m.Role.Label is "system" or "assistant" or "user" or "tool")
                .Select(m => new ChatMessageContent(m.Role, m.ToString())));
        var assistantMessage = new AssistantChatMessage();
        chatMessages.Add(assistantMessage);

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
                        assistantMessage.InlineCollection.Add(streamingContent.Content);
                    }

                    authorRole ??= streamingContent.Role;
                    functionCallContentBuilder.Append(streamingContent);
                }

                chatHistory.AddAssistantMessage(assistantContentBuilder.ToString());

                var functionCalls = functionCallContentBuilder.Build();
                if (functionCalls.Count == 0) break;

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
                    chatMessages.Insert(chatMessages.Count - 1, actionChatMessage);

                    try
                    {
                        var resultContent = await functionCall.InvokeAsync(kernel, cancellationToken);
                        chatHistory.Add(resultContent.ToChatMessage());
                    }
                    catch (Exception ex)
                    {
                        chatHistory.Add(new FunctionResultContent(functionCall, ex.Message).ToChatMessage());
                        actionChatMessage.InlineCollection.Add($"Failed: {ex.Message}");
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
            assistantMessage.IsBusy = false;
        }
    }
}