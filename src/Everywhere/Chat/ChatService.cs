using System.Text;
using Everywhere.AI;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Storage;
using Everywhere.Utilities;
using Lucide.Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ZLinq;

namespace Everywhere.Chat;

public class ChatService(
    IChatContextManager chatContextManager,
    IChatPluginManager chatPluginManager,
    IKernelMixinFactory kernelMixinFactory,
    Settings settings,
    ILogger<ChatService> logger
) : IChatService
{
    public async Task SendMessageAsync(UserChatMessage userMessage, CancellationToken cancellationToken)
    {
        var chatContext = chatContextManager.Current;
        chatContext.Add(userMessage);
        var kernelMixin = kernelMixinFactory.GetOrCreate(settings.Model);
        await ProcessUserChatMessageAsync(kernelMixin, chatContext, userMessage, cancellationToken);
        userMessage.UserPrompt += "\n\nPlease try to use the tools if necessary, before answering.";
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
        return GenerateAsync(kernelMixinFactory.GetOrCreate(settings.Model), node.Context, assistantChatMessage, cancellationToken);
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
            .AsValueEnumerable()
            .OfType<ChatVisualElementAttachment>()
            .Select(a => a.Element?.Target)
            .OfType<IVisualElement>()
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
                kernelMixin.MaxTokenTotal / 20,
                chatContext.VisualElements.Count + 1);
            var renderedVisualTreePrompt = await Task.Run(
                () =>
                {
                    var xml = xmlBuilder.BuildXml(cancellationToken);
                    var builtVisualElements = xmlBuilder.BuiltVisualElements;

                    string focusedElementIdString;
                    if (builtVisualElements.FirstOrDefault(kv => ReferenceEquals(kv.Value, elements[0])) is
                        { Key: var focusedElementId, Value: not null })
                    {
                        focusedElementIdString = focusedElementId.ToString();
                    }
                    else
                    {
                        focusedElementIdString = "null";
                    }

                    // Adds the visual elements to the chat context for future reference.
                    chatContext.VisualElements.AddRange(builtVisualElements);

                    // Then deactivate all the references, making them weak references.
                    foreach (var reference in userChatMessage
                                 .Attachments
                                 .AsValueEnumerable()
                                 .OfType<ChatVisualElementAttachment>()
                                 .Select(a => a.Element)
                                 .OfType<ResilientReference<IVisualElement>>())
                    {
                        reference.IsActive = false;
                    }

                    return Prompts.RenderPrompt(
                        Prompts.VisualTreePrompt,
                        new Dictionary<string, Func<string>>
                        {
                            { "VisualTree", () => xml },
                            { "FocusedElementId", () => focusedElementIdString },
                        });
                },
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
    private Kernel BuildKernel(IKernelMixin kernelMixin, ChatContext chatContext)
    {
        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton(kernelMixin.ChatCompletionService);
        builder.Services.AddSingleton(chatContext);

        if (settings.Internal.IsToolCallEnabled)
        {
            var chatPluginScope = chatPluginManager.CreateScope(chatContext);
            builder.Services.AddSingleton(chatPluginScope);
            foreach (var plugin in chatPluginScope.Plugins)
            {
                builder.Plugins.Add(plugin);
            }
        }

        return builder.Build();
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

            var kernel = BuildKernel(kernelMixin, chatContext);
            var chatHistory = new ChatHistory();
            foreach (var chatMessage in chatContext
                         .Select(n => n.Message)
                         .Where(m => !ReferenceEquals(m, assistantChatMessage)) // exclude the current assistant message
                         .Where(m => m.Role.Label is "system" or "assistant" or "user" or "tool"))
            {
                await foreach (var chatMessageContent in CreateChatMessageContentsAsync(chatMessage))
                {
                    chatHistory.Add(chatMessageContent);
                }
            }

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

                    // for those LLM who doesn't implement function calling correctly,
                    // we need to generate a unique ToolCallId for each tool call update.
                    for (var i = 0; i < streamingContent.Items.Count; i++)
                    {
                        var item = streamingContent.Items[i];
                        if (item is StreamingFunctionCallUpdateContent { Name.Length: > 0, CallId: null or { Length: 0 } } idiotContent)
                        {
                            // Generate a unique ToolCallId for the function call update.
                            streamingContent.Items[i] = new StreamingFunctionCallUpdateContent(
                                Guid.NewGuid().ToString("N"),
                                idiotContent.Name,
                                idiotContent.Arguments,
                                idiotContent.FunctionCallIndex);
                        }
                    }

                    authorRole ??= streamingContent.Role;
                    functionCallContentBuilder.Append(streamingContent);
                }

                if (assistantContentBuilder.Length > 0) chatHistory.AddAssistantMessage(assistantContentBuilder.ToString());

                var functionCallContents = functionCallContentBuilder.Build();
                if (functionCallContents.Count == 0) break;

                // Group function calls by plugin name, and create ActionChatMessages for each group.
                var chatPluginScope = kernel.GetRequiredService<IChatPluginScope>();
                foreach (var functionCallContentGroup in functionCallContents.GroupBy(f => f.FunctionName))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!chatPluginScope.TryGetPluginAndFunction(
                            functionCallContentGroup.Key,
                            out var chatPlugin,
                            out var chatFunction))
                    {
                        throw new InvalidOperationException($"Function '{functionCallContentGroup.Key}' is not available");
                    }

                    var functionCallChatMessage = new FunctionCallChatMessage(
                        chatFunction.Icon ?? chatPlugin.Icon ?? LucideIconKind.Hammer,
                        chatPlugin.HeaderKey)
                    {
                        IsBusy = true,
                    };
                    assistantChatMessage.FunctionCalls.Add(functionCallChatMessage);

                    // Add call message to the chat history.
                    var functionCallMessage = new ChatMessageContent(AuthorRole.Assistant, content: null);
                    chatHistory.Add(functionCallMessage);

                    // Iterate through the function call contents in the group.
                    foreach (var functionCallContent in functionCallContentGroup)
                    {
                        functionCallChatMessage.Calls.Add(functionCallContent);
                        functionCallMessage.Items.Add(functionCallContent);

                        FunctionResultContent resultContent;
                        try
                        {
                            resultContent = await functionCallContent.InvokeAsync(kernel, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            resultContent = new FunctionResultContent(functionCallContent, $"Error: {ex.Message}");
                            functionCallChatMessage.ErrorMessageKey = ex.GetFriendlyMessage();
                            logger.LogError(ex, "Error invoking function '{FunctionName}'", functionCallContent.FunctionName);
                        }

                        functionCallChatMessage.Results.Add(resultContent);
                        chatHistory.Add(new ChatMessageContent(AuthorRole.Tool, [resultContent]));

                        if (functionCallChatMessage.ErrorMessageKey is not null)
                        {
                            break; // If an error occurs, we stop processing further function calls.
                        }
                    }

                    if (await TryCreateExtraToolCallResultsContentAsync(functionCallChatMessage) is { } extraToolCallResultsContent)
                    {
                        chatHistory.Add(extraToolCallResultsContent);
                    }

                    // Serialize the function calls and results to JSON.
                    functionCallChatMessage.IsBusy = false;
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
            logger.LogError(e, "Error generating chat response");
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
                    foreach (var functionCallChatMessage in assistant.FunctionCalls)
                    {
                        await foreach (var actionChatMessageContent in CreateChatMessageContentsAsync(functionCallChatMessage))
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
                    await AddAttachmentsToChatMessageContentAsync(user.Attachments, content);
                    yield return content;
                    break;
                }
                case FunctionCallChatMessage functionCall:
                {
                    var functionCallMessage = new ChatMessageContent(AuthorRole.Assistant, content: null);
                    functionCallMessage.Items.AddRange(functionCall.Calls);
                    yield return functionCallMessage;

                    foreach (var result in functionCall.Results)
                    {
                        yield return result.ToChatMessage();
                    }

                    if (await TryCreateExtraToolCallResultsContentAsync(functionCall) is { } extraToolCallResultsContent)
                    {
                        yield return extraToolCallResultsContent;
                    }

                    break;
                }
                case { Role.Label: "system" or "user" or "developer" or "tool" }:
                {
                    yield return new ChatMessageContent(chatMessage.Role, chatMessage.ToString());
                    break;
                }
            }
        }

        async ValueTask<ChatMessageContent?> TryCreateExtraToolCallResultsContentAsync(FunctionCallChatMessage functionCallChatMessage)
        {
            if (!functionCallChatMessage.Attachments.Any()) return null;

            var content = new ChatMessageContent(AuthorRole.User, "Extra tool call results in order");
            await AddAttachmentsToChatMessageContentAsync(functionCallChatMessage.Attachments, content);
            return content;
        }

        async ValueTask AddAttachmentsToChatMessageContentAsync(IEnumerable<ChatAttachment> attachments, ChatMessageContent content)
        {
            foreach (var attachment in attachments)
            {
                switch (attachment)
                {
                    case ChatTextAttachment text:
                    {
                        content.Items.Add(new TextContent(text.Text));
                        break;
                    }
                    case ChatFileAttachment file:
                    {
                        byte[] data;

                        var fileInfo = new FileInfo(file.FilePath);
                        if (!fileInfo.Exists || fileInfo.Length <= 0 || fileInfo.Length > 25 * 1024 * 1024) // TODO: Configurable max file size?
                        {
                            continue;
                        }

                        try
                        {
                            data = await File.ReadAllBytesAsync(file.FilePath, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            // If we fail to read the file, just skip it.
                            // The file might be deleted or moved.
                            // We don't want to fail the whole message because of one attachment.
                            // Just log the error and continue.
                            logger.LogWarning(ex, "Failed to read attachment file '{FilePath}'", file.FilePath);
                            continue;
                        }

                        if (MimeTypeUtilities.IsAudio(file.MimeType))
                        {
                            content.Items.Add(new AudioContent(data, file.MimeType));
                        }
                        else if (MimeTypeUtilities.IsImage(file.MimeType))
                        {
                            content.Items.Add(new ImageContent(data, file.MimeType));
                        }
                        else
                        {
                            content.Items.Add(new BinaryContent(data, file.MimeType));
                        }

                        break;
                    }
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