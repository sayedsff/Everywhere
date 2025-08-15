using System.Text;
using System.Text.Json;
using Everywhere.Models;
using Lucide.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Plugins.Web.Brave;

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
        var kernelMixin = kernelMixinFactory.GetOrCreate();
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
        return GenerateAsync(kernelMixinFactory.GetOrCreate(), node.Context, assistantChatMessage, cancellationToken);
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

    /// <summary>
    /// Kernel is very cheap to create, so we can create a new kernel for each request.
    /// This method builds the kernel based on the current settings.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    private Kernel BuildKernel(IKernelMixin kernelMixin)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(kernelMixin.ChatCompletionService);

        var modelSettings = settings.Model;
        if (modelSettings.SelectedModelDefinition?.IsFunctionCallingSupported.ActualValue is true && settings.Internal.IsWebSearchEnabled)
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
                        "bing" => new BingConnector(webSearchApiKey, webSearchEndPoint),
                        _ => throw new NotSupportedException($"Web search provider '{modelSettings.WebSearchProvider}' is not supported.")
                    }),
                "web_search");
        }

        return builder.Build();
    }

    /// <summary>
    /// Represents a record of a function call and its result.
    /// </summary>
    /// <param name="FunctionCall"></param>
    /// <param name="Result"></param>
    private record FunctionCallRecord(FunctionCallContent FunctionCall, FunctionResultContent Result);

    private async Task GenerateAsync(
        IKernelMixin kernelMixin,
        ChatContext chatContext,
        AssistantChatMessage assistantChatMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var kernel = BuildKernel(kernelMixin);
            var chatHistory = new ChatHistory(
                await chatContext
                    .Select(n => n.Message)
                    .Where(m => !Equals(m, assistantChatMessage)) // exclude the current assistant message
                    .Where(m => m.Role.Label is "system" or "assistant" or "user" or "tool")
                    .ToAsyncEnumerable()
                    .SelectMany(CreateChatMessageContentsAsync)
                    .OfType<ChatMessageContent>()
                    .ToArrayAsync(cancellationToken: cancellationToken));

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AuthorRole? authorRole = null;
                var assistantContentBuilder = new StringBuilder();
                var functionCallContentBuilder = new FunctionCallContentBuilder();
                var promptExecutionSettings = kernelMixin.GetPromptExecutionSettings();

                await foreach (var streamingContent in kernelMixin.ChatCompletionService.GetStreamingChatMessageContentsAsync(
                                   chatHistory,
                                   promptExecutionSettings,
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

                if (assistantContentBuilder.Length > 0) chatHistory.AddAssistantMessage(assistantContentBuilder.ToString());

                var functionCallContents = functionCallContentBuilder.Build();
                if (functionCallContents.Count == 0) break;

                // Group function calls by plugin name, and create ActionChatMessages for each group.
                foreach (var functionCallContentGroup in functionCallContents.GroupBy(f => f.PluginName))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var actionChatMessage = functionCallContentGroup.Key switch
                    {
                        "web_search" => new ActionChatMessage(
                            AuthorRole.Tool,
                            LucideIconKind.Globe,
                            LocaleKey.ActionChatMessage_Header_WebSearching),
                        _ => throw new NotImplementedException()
                    };
                    actionChatMessage.IsBusy = true;
                    assistantChatMessage.Actions.Add(actionChatMessage);

                    // Add call message to the chat history.
                    var functionCallMessage = new ChatMessageContent(AuthorRole.Assistant, content: null);
                    chatHistory.Add(functionCallMessage);

                    // Iterate through the function call contents in the group.
                    var functionCallRecords = new List<FunctionCallRecord>(functionCallContentGroup.Count());
                    foreach (var functionCallContent in functionCallContentGroup)
                    {
                        functionCallMessage.Items.Add(functionCallContent);

                        FunctionResultContent resultContent;
                        try
                        {
                            resultContent = await functionCallContent.InvokeAsync(kernel, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            resultContent = new FunctionResultContent(functionCallContent, $"Error: {ex.Message}");
                            actionChatMessage.ErrorMessageKey = ex.GetFriendlyMessage();
                        }

                        // Add result content to the chat history and the function result contents list.
                        functionCallRecords.Add(new FunctionCallRecord(functionCallContent, resultContent));
                        chatHistory.Add(resultContent.ToChatMessage());

                        if (actionChatMessage.ErrorMessageKey is not null)
                        {
                            break; // If an error occurs, we stop processing further function calls.
                        }
                    }

                    // Serialize the function calls and results to JSON.
                    actionChatMessage.Content = JsonSerializer.Serialize(functionCallRecords);
                    actionChatMessage.IsBusy = false;
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
                    kernelMixin.ChatCompletionService,
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
            assistantChatMessage.FinishedAt = DateTimeOffset.UtcNow;
            assistantChatMessage.IsBusy = false;
        }

        async IAsyncEnumerable<ChatMessageContent> CreateChatMessageContentsAsync(ChatMessage chatMessage)
        {
            switch (chatMessage)
            {
                case SystemChatMessage system:
                {
                    yield return new ChatMessageContent(chatMessage.Role, system.SystemPrompt);
                    break;
                }
                case AssistantChatMessage assistant:
                {
                    // If the assistant message has actions, we need to yield them first.
                    foreach (var actionChatMessage in assistant.Actions)
                    {
                        await foreach (var actionChatMessageContent in CreateChatMessageContentsAsync(actionChatMessage))
                        {
                            yield return actionChatMessageContent;
                        }
                    }

                    yield return new ChatMessageContent(chatMessage.Role, assistant.MarkdownBuilder.ToString());
                    break;
                }
                case UserChatMessage user:
                {
                    var content = new ChatMessageContent(chatMessage.Role, user.UserPrompt);

                    foreach (var imageAttachment in user.Attachments.OfType<ChatFileAttachment>().Where(a => a.IsImage))
                    {
                        using var image = await imageAttachment.GetImageAsync();
                        if (image is null) continue;

                        using var ms = new MemoryStream();
                        image.Save(ms, 100);
                        ms.Position = 0;
                        content.Items.Add(new ImageContent(ms.ToArray(), "image/png"));
                    }

                    yield return content;

                    break;
                }
                case ActionChatMessage { Role.Label: "tool", Content: { } json }:
                {
                    var functionCallRecords = DeserializeFunctionCallRecords(json);

                    var functionCallMessage = new ChatMessageContent(AuthorRole.Assistant, content: null);
                    functionCallMessage.Items.AddRange(functionCallRecords.Select(f => f.FunctionCall));
                    yield return functionCallMessage;

                    foreach (var (_, result) in functionCallRecords)
                    {
                        yield return result.ToChatMessage();
                    }

                    break;
                }
                case { Role.Label: "system" or "user" or "developer" or "tool" }:
                {
                    yield return new ChatMessageContent(chatMessage.Role, chatMessage.ToString());
                    break;
                }
            }

            static List<FunctionCallRecord> DeserializeFunctionCallRecords(string json)
            {
                try
                {
                    return JsonSerializer.Deserialize<List<FunctionCallRecord>>(json) ?? [];
                }
                catch (JsonException)
                {
                    return [];
                }
            }
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