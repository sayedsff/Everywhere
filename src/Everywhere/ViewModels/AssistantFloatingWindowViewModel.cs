using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Agents;
using Everywhere.Enums;
using Everywhere.Models;
using Everywhere.Utils;
using Everywhere.Views;
using IconPacks.Avalonia.Material;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web.Brave;
using ObservableCollections;
using ChatMessage = Everywhere.Models.ChatMessage;

namespace Everywhere.ViewModels;

public partial class AssistantFloatingWindowViewModel : BusyViewModelBase
{
    public Settings Settings { get; }

    [ObservableProperty]
    public partial IVisualElement? TargetElement { get; private set; }

    [ObservableProperty]
    public partial PixelRect TargetBoundingRect { get; private set; }

    [ObservableProperty]
    public partial string? Title { get; private set; }

    [ObservableProperty]
    public partial List<DynamicKeyMenuItem> Actions { get; private set; } = [];

    [ObservableProperty]
    public partial List<AssistantCommand> AssistantCommands { get; private set; } = [];

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<ChatMessage> ChatMessages =>
        field ??= chatMessages.CreateView(x => x).With(v => v.AttachFilter(m => m.Role != AuthorRole.System))
            .ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly List<DynamicKeyMenuItem> textEditActions;
    private readonly List<AssistantCommand> textEditCommands;
    private readonly ObservableList<ChatMessage> chatMessages = [];

    private CancellationTokenSource? cancellationTokenSource;

    public AssistantFloatingWindowViewModel(Settings settings)
    {
        Settings = settings;

        textEditActions =
        [
            // new DynamicKeyMenuItem
            // {
            //     Icon = MakeIcon(PackIconMaterialKind.Translate),
            //     Header = "AssistantFloatingWindowViewModel_Translate",
            //     Command = GenerateAndReplaceCommand,
            //     CommandParameter =
            //         "Translate the content of XML node with id=\"{ElementId}\" between **{SystemLanguage}** and **English**" // todo
            // },
            // new DynamicKeyMenuItem
            // {
            //     Icon = MakeIcon(PackIconMaterialKind.FastForward),
            //     Header = "AssistantFloatingWindowViewModel_ContinueWriting",
            //     Command = GenerateAndAppendCommand,
            //     CommandParameter =
            //         "The user has already written a beginning as the content of XML node with id=\"{ElementId}\". " +
            //         "You should try to imitate the user's writing style and tone, and continue writing in the user's perspective"
            // },
            // new DynamicKeyMenuItem
            // {
            //     Header = "Change Tone to",
            //     Items =
            //     {
            //         new DynamicKeyMenuItem
            //         {
            //             Header = "Formal",
            //             Command = GenerateAndReplaceCommand,
            //             CommandParameter = "Change the tone of content of XML node with id=\"{ElementId}\" to **Formal**"
            //         },
            //         new DynamicKeyMenuItem
            //         {
            //             Header = "Casual",
            //             Command = GenerateAndReplaceCommand,
            //             CommandParameter = "Change the tone of content of XML node with id=\"{ElementId}\" to **Casual**"
            //         },
            //         new DynamicKeyMenuItem
            //         {
            //             Header = "Creative",
            //             Command = GenerateAndReplaceCommand,
            //             CommandParameter = "Change the tone of content of XML node with id=\"{ElementId}\" to **Creative**"
            //         },
            //         new DynamicKeyMenuItem
            //         {
            //             Header = "Professional",
            //             Command = GenerateAndReplaceCommand,
            //             CommandParameter = "Change the tone of content of XML node with id=\"{ElementId}\" to **Professional**"
            //         }
            //     }
            // }
        ];

        textEditCommands =
        [
            new AssistantCommand(
                "/translate",
                "AssistantCommand_Translate_Description",
                Prompts.RenderPrompt(
                    Prompts.DefaultSystemPromptWithMission,
                    new Dictionary<string, Func<string>>
                    {
                        { "Mission", () => "Based on context, translate the content of XML node with id=\"{ElementId}\"" }
                    }),
                "Translate it into {0}",
                () => Settings.Common.Language),
            new AssistantCommand(
                "/rewrite",
                "AssistantCommand_Rewrite_Description",
                Prompts.RenderPrompt(
                    Prompts.DefaultSystemPromptWithMission,
                    new Dictionary<string, Func<string>>
                    {
                        { "Mission", () => "Based on context, rewrite the content of XML node with id=\"{ElementId}\"" }
                    }),
                "{0}",
                () => "Refine it"),
        ];
    }

    private CancellationTokenSource? targetElementChangedTokenSource;

    [RelayCommand]
    public async Task SetTargetElementAsync(IVisualElement? targetElement)
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
                if (Equals(TargetElement, targetElement)) return;

                Reset();

                if (targetElement is not { Type: VisualElementType.TextEdit } ||
                    (targetElement.States & (
                        VisualElementStates.Offscreen |
                        VisualElementStates.Disabled |
                        VisualElementStates.ReadOnly |
                        VisualElementStates.Password)) != 0)
                {
                    TargetElement = null;
                    return;
                }

                using (var process = Process.GetProcessById(targetElement.ProcessId))
                {
                    Title = Path.GetFileNameWithoutExtension(process.ProcessName);
                }

                TargetBoundingRect = targetElement.BoundingRectangle;
                TargetElement = targetElement;
                Actions = textEditActions;
                AssistantCommands = textEditCommands;
            },
            flags: ExecutionFlags.EnqueueIfBusy,
            cancellationToken: cancellationToken);
    }

    private static readonly AuthorRole ActionRole = new("Action");

    [RelayCommand]
    private Task ProcessChatMessageSentAsync(string message) => ExecuteBusyTaskAsync(
        async cancellationToken =>
        {
            message = message.Trim();
            if (message.Length == 0) return;

            string? systemPrompt = null;
            ChatMessage? userMessage = null;

            if (message[0] == '/')
            {
                var commandString = message.IndexOf(' ') is var index and > 0 ? message[..index] : message;
                if (AssistantCommands.FirstOrDefault(
                        c => c.Command.Equals(commandString, StringComparison.OrdinalIgnoreCase)) is { } command)
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

            systemPrompt ??= Prompts.RenderPrompt(
                Prompts.DefaultSystemPromptWithMission,
                new Dictionary<string, Func<string>>
                {
                    { "Mission", () => "Focused XML node id=\"{ElementId}\". Based on context, answer the user's question" }
                });
            userMessage ??= new UserChatMessage(message) { InlineCollection = { message } };
            chatMessages.Add(userMessage);

            if (chatMessages.Count == 1)
            {
                if (TargetElement is not { } targetElement) return;

                var analysisMessage = new ActionChatMessage(
                    ActionRole,
                    PackIconMaterialKind.LayersSearch,
                    "ActionChatMessage_Header_AnalyzingContext");
                chatMessages.Add(analysisMessage);

                analysisMessage.IsBusy = true;
                var builtSystemPrompt = await Task.Run(() => BuildSystemPrompt(targetElement, systemPrompt, cancellationToken), cancellationToken);
                analysisMessage.IsBusy = false;
                chatMessages.Insert(0, new SystemChatMessage(builtSystemPrompt));
            }

            await GenerateAsync(cancellationToken);
        });

    [RelayCommand]
    private Task RetryAsync(ChatMessage chatMessage) => ExecuteBusyTaskAsync(
        cancellationToken =>
        {
            var index = chatMessages.IndexOf(chatMessage);
            if (index == -1) return Task.CompletedTask;
            chatMessages.RemoveRange(index, chatMessages.Count - index); // TODO: history tree
            return GenerateAsync(cancellationToken);
        });

    [RelayCommand]
    private static Task CopyAsync(ChatMessage chatMessage) =>
        ServiceLocator.Resolve<AssistantFloatingWindow>().Clipboard?.SetTextAsync(chatMessage.ToString()) ?? Task.CompletedTask;

    [RelayCommand]
    private void Cancel()
    {
        if (IsBusy) return;
        Reset();
    }

    private void Reset()
    {
        cancellationTokenSource?.Cancel();
        IsExpanded = false;
        chatMessages.Clear();
        Actions = [];
        AssistantCommands = [];
    }

    private string BuildSystemPrompt(IVisualElement element, string systemPrompt, CancellationToken cancellationToken)
    {
        var xmlBuilder = new VisualElementXmlBuilder(new OptimizedVisualElement(
            element
                .GetAncestors()
                .CurrentAndNext()
                .Where(p => p.current.ProcessId != p.next.ProcessId)
                .Select(p => p.current)
                .First()));

        var renderedSystemPrompt = Prompts.RenderPrompt(
            systemPrompt,
            new Dictionary<string, Func<string>>
            {
                { "OS", () => Environment.OSVersion.ToString() },
                { "Time", () => DateTime.Now.ToString("F") },
                { "SystemLanguage", () => new CultureInfo(Settings.Common.Language).DisplayName },
                { "VisualTree", () => xmlBuilder.BuildXml(cancellationToken) },
                { "ElementId", () => xmlBuilder.GetIdMap(cancellationToken)[element].ToString() },
            });

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
            builder.Plugins.AddFromObject(new WebSearchEnginePlugin(modelSettings.WebSearchProvider switch
            {
                // TODO: "google" => new GoogleConnector(webSearchApiKey, webSearchEndPoint),
                "brave" => new BraveConnector(webSearchApiKey, webSearchEndPoint),
                "bocha" => new BoChaConnector(webSearchApiKey, webSearchEndPoint),
                _ => new BingConnector(webSearchApiKey, webSearchEndPoint)
            }), "web_search");
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
                            PackIconMaterialKind.Earth,
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