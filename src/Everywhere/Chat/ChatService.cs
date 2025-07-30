using System.Text;
using Everywhere.Models;
using Lucide.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web.Brave;
using Microsoft.SemanticKernel.TextGeneration;

namespace Everywhere.Chat;

public class ChatService(
    IChatContextManager chatContextManager,
    IKernelMixinFactory kernelMixinFactory,
    Settings settings
) : IChatService
{
    public async Task SendMessageAsync(UserChatMessage userMessage, CancellationToken cancellationToken)
    {
        var chatContext = chatContextManager.Current;
        chatContext.Add(userMessage);
        var kernelMixin = kernelMixinFactory.Create();
        await ProcessUserChatMessageAsync(kernelMixin, chatContext, userMessage, cancellationToken);
        var assistantChatMessage = new AssistantChatMessage { IsBusy = true };
        chatContext.Add(assistantChatMessage);
        await GenerateAsync(kernelMixin, chatContext, assistantChatMessage, cancellationToken);
    }

    public Task RetryAsync(ChatMessageNode node, CancellationToken cancellationToken)
    {
        if (node.Message.Role != AuthorRole.Assistant)
        {
            return Task.FromException(new InvalidOperationException("Only assistant messages can be retried."));
        }

        var assistantChatMessage = new AssistantChatMessage { IsBusy = true };
        node.Context.CreateBranchOn(node, assistantChatMessage);
        return GenerateAsync(kernelMixinFactory.Create(), node.Context, assistantChatMessage, cancellationToken);
    }

    public Task EditAsync(ChatMessageNode node, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async static Task ProcessUserChatMessageAsync(
        IKernelMixin kernelMixin,
        ChatContext chatContext,
        UserChatMessage userChatMessage,
        CancellationToken cancellationToken)
    {
        var elements = userChatMessage
            .Attachments
            .OfType<ChatVisualElementAttachment>()
            .Select(a => a.Element)
            .ToArray();

        if (elements.Length <= 0) return;

        var analyzingContextMessage = new ActionChatMessage(
            new AuthorRole("action"),
            LucideIconKind.TextSearch,
            LocaleKey.ActionChatMessage_Header_AnalyzingContext)
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

    private async Task GenerateAsync(
        IKernelMixin kernelMixin,
        ChatContext chatContext,
        AssistantChatMessage assistantChatMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // todo: maybe I need to move this builder to a better place (using DI)
            var modelSettings = settings.Model;
            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IChatCompletionService>(kernelMixin);
            builder.Services.AddSingleton<ITextGenerationService>(kernelMixin);

            if (modelSettings.IsWebSearchSupported && settings.Internal.IsWebSearchEnabled)
            {
                var webSearchApiKey = modelSettings.WebSearchApiKey;
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
            cancellationToken.ThrowIfCancellationRequested();

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
                    .Select(CreateChatMessageContent));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                    cancellationToken.ThrowIfCancellationRequested();

                    functionCallContent.Items.Add(functionCall);

                    var actionChatMessage = functionCall.PluginName switch
                    {
                        "web_search" => new ActionChatMessage(
                            AuthorRole.Tool,
                            LucideIconKind.Globe,
                            LocaleKey.ActionChatMessage_Header_WebSearching),
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
                        actionChatMessage.ErrorMessageKey = ex.GetFriendlyMessage();
                    }
                    finally
                    {
                        actionChatMessage.IsBusy = false;
                    }
                }
            }

            if (chatContext.Metadata.Topic.IsNullOrEmpty() &&
                chatHistory.Any(c => c.Role == AuthorRole.User) &&
                chatHistory.Any(c => c.Role == AuthorRole.Assistant) &&
                chatHistory.First(c => c.Role == AuthorRole.User).Content is { Length: > 0 } userMessage &&
                chatHistory.First(c => c.Role == AuthorRole.Assistant).Content is { Length: > 0 } assistantMessage)
            {
                // If the chat history only contains one user message and one assistant message,
                // we can generate a title for the chat context.
                GenerateTitleAsync(
                    chatCompletionService,
                    userMessage,
                    assistantMessage,
                    chatContext.Metadata,
                    cancellationToken).Detach(IExceptionHandler.DangerouslyIgnoreAllException);
            }
        }
        catch (Exception e)
        {
            assistantChatMessage.ErrorMessageKey = e.GetFriendlyMessage();
        }
        finally
        {
            assistantChatMessage.IsBusy = false;
        }

        ChatMessageContent CreateChatMessageContent(ChatMessage chatMessage)
        {
            ChatMessageContent? content;
            switch (chatMessage)
            {
                case UserChatMessage user:
                {
                    content = new ChatMessageContent(chatMessage.Role, user.UserPrompt);

                    if (user.Attachments.OfType<ChatImageAttachment>().ToArray() is not { Length: > 0 } imageAttachments) break;
                    foreach (var imageAttachment in imageAttachments)
                    {
                        using var ms = new MemoryStream();
                        imageAttachment.Image.Save(ms, 100);
                        ms.Position = 0;
                        content.Items.Add(new ImageContent(ms.ToArray(), "image/png"));
                    }

                    break;
                }
                case AssistantChatMessage assistant:
                {
                    content = new ChatMessageContent(chatMessage.Role, assistant.MarkdownBuilder.ToString());
                    break;
                }
                case SystemChatMessage system:
                {
                    content = new ChatMessageContent(chatMessage.Role, system.SystemPrompt);
                    break;
                }
                default:
                {
                    content = new ChatMessageContent(chatMessage.Role, chatMessage.ToString());
                    break;
                }
            }
            return content;
        }
    }

    private async Task GenerateTitleAsync(
        IChatCompletionService chatCompletionService,
        string userMessage,
        string assistantMessage,
        ChatContextMetadata metadata,
        CancellationToken cancellationToken)
    {
        var chatHistory = new ChatHistory
        {
            new ChatMessageContent(
                AuthorRole.System,
                Prompts.RenderPrompt(
                    Prompts.SummarizeChatPrompt,
                    new Dictionary<string, Func<string>>
                    {
                        { "UserMessage", () => userMessage },
                        { "AssistantMessage", () => assistantMessage },
                        { "SystemLanguage", () => settings.Common.Language }
                    })),
        };
        var openAIPromptExecutionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.0f,
            TopP = 1.0f,
            FunctionChoiceBehavior = FunctionChoiceBehavior.None()
        };
        var chatMessageContent = await chatCompletionService.GetChatMessageContentAsync(
            chatHistory,
            openAIPromptExecutionSettings,
            cancellationToken: cancellationToken);
        metadata.Topic = chatMessageContent.Content;
    }
}