using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Anthropic.SDK.Messaging;
using Avalonia.Threading;
using Everywhere.AI;
using Everywhere.Chat.Plugins;
using Everywhere.Common;
using Everywhere.Configuration;
using Everywhere.Interop;
using Everywhere.Storage;
using Everywhere.Utilities;
using LiveMarkdown.Avalonia;
using Lucide.Avalonia;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using ZLinq;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;
using FunctionCallContent = Microsoft.SemanticKernel.FunctionCallContent;
using FunctionResultContent = Microsoft.SemanticKernel.FunctionResultContent;
using ImageContent = Microsoft.SemanticKernel.ImageContent;
using TextContent = Microsoft.SemanticKernel.TextContent;

namespace Everywhere.Chat;

public class ChatService(
    IChatContextManager chatContextManager,
    IChatPluginManager chatPluginManager,
    IKernelMixinFactory kernelMixinFactory,
    Settings settings,
    ILogger<ChatService> logger
) : IChatService
{
    private readonly ActivitySource _activitySource = new(typeof(ChatService).FullName.NotNull());

    public async Task SendMessageAsync(UserChatMessage message, CancellationToken cancellationToken)
    {
        var customAssistant = settings.Model.SelectedCustomAssistant;
        if (customAssistant is null) return;

        using var activity = _activitySource.StartActivity();

        var chatContext = chatContextManager.Current;
        activity?.SetTag("chat.context.id", chatContext.Metadata.Id);
        chatContext.Add(message);

        await ProcessUserChatMessageAsync(chatContext, customAssistant, message, cancellationToken);

        var assistantChatMessage = new AssistantChatMessage { IsBusy = true };
        chatContext.Add(assistantChatMessage);

        await GenerateAsync(chatContext, customAssistant, assistantChatMessage, cancellationToken);
    }

    public async Task RetryAsync(ChatMessageNode node, CancellationToken cancellationToken)
    {
        var customAssistant = settings.Model.SelectedCustomAssistant;
        if (customAssistant is null) return;

        using var activity = _activitySource.StartActivity();
        activity?.SetTag("chat.context.id", node.Context.Metadata.Id);

        if (node.Message.Role != AuthorRole.Assistant)
        {
            throw new InvalidOperationException("Only assistant messages can be retried.");
        }

        var assistantChatMessage = new AssistantChatMessage { IsBusy = true };
        node.Context.CreateBranchOn(node, assistantChatMessage);

        await GenerateAsync(node.Context, customAssistant, assistantChatMessage, cancellationToken);
    }

    public Task EditAsync(ChatMessageNode node, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private async Task ProcessUserChatMessageAsync(
        ChatContext chatContext,
        CustomAssistant customAssistant,
        UserChatMessage userChatMessage,
        CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity();
        activity?.SetTag("chat.context.id", chatContext.Metadata.Id);

        var attachmentTagValues = activity is null ? null : new List<object>();
        var validVisualElements = new List<IVisualElement>();
        foreach (var attachment in userChatMessage.Attachments.AsValueEnumerable().Take(10))
        {
            switch (attachment)
            {
                case ChatTextAttachment textAttachment:
                {
                    attachmentTagValues?.Add(
                        new
                        {
                            type = "text",
                            length = textAttachment.Text.Length
                        });
                    break;
                }
                case ChatFileAttachment fileAttachment:
                {
                    attachmentTagValues?.Add(
                        new
                        {
                            type = "file",
                            mime_type = fileAttachment.MimeType,
                            file_size = GetFileSize(fileAttachment.FilePath)
                        });
                    break;
                }
                case ChatVisualElementAttachment visualElement:
                {
                    var element = visualElement.Element?.Target;
                    if (element is not null) validVisualElements.Add(element);

                    attachmentTagValues?.Add(
                        new
                        {
                            type = "element",
                            element_type = element?.Type.ToString() ?? "invalid",
                        });

                    break;
                }
            }
        }

        activity?.SetTag("attachments", JsonSerializer.Serialize(attachmentTagValues));

        if (validVisualElements.Count > 0)
        {
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

                var maxTokens = customAssistant.MaxTokens.ActualValue;
                var approximateTokenLimit = Math.Min(settings.Internal.VisualTreeTokenLimit, maxTokens / 2);
                var xmlBuilder = new VisualTreeXmlBuilder(
                    validVisualElements,
                    approximateTokenLimit,
                    chatContext.VisualElements.Count + 1,
                    settings.ChatWindow.VisualTreeXmlDetailLevel);
                var renderedVisualTreePrompt = await Task.Run(
                    () =>
                    {
                        // ReSharper disable once ExplicitCallerInfoArgument
                        using var builderActivity = _activitySource.StartActivity("BuildVisualTreeXml");

                        var xml = xmlBuilder.BuildXml(cancellationToken);
                        var builtVisualElements = xmlBuilder.BuiltVisualElements;
                        builderActivity?.SetTag("xml.length", xml.Length);
                        builderActivity?.SetTag("xml.length_limit", approximateTokenLimit);
                        builderActivity?.SetTag("xml.built_visual_elements.count", builtVisualElements.Count);

                        string focusedElementIdString;
                        if (builtVisualElements.FirstOrDefault(kv => ReferenceEquals(kv.Value, validVisualElements[0])) is
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
            catch (Exception e)
            {
                e = HandledChatException.Handle(e);
                activity?.SetStatus(ActivityStatusCode.Error, e.Message.Trim());
                analyzingContextMessage.ErrorMessageKey = e.GetFriendlyMessage();
                logger.LogError(e, "Error analyzing visual tree");
            }
            finally
            {
                analyzingContextMessage.FinishedAt = DateTimeOffset.UtcNow;
                analyzingContextMessage.IsBusy = false;
            }
        }

        // Append tool use prompt if enabled.
        // if (settings.Internal.IsToolCallEnabled)
        // {
        //     userChatMessage.UserPrompt += Prompts.TryUseToolUserPrompt;
        // }

        static long GetFileSize(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                return fileInfo.Exists ? fileInfo.Length : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    private IKernelMixin CreateKernelMixin(CustomAssistant customAssistant)
    {
        using var activity = _activitySource.StartActivity();

        try
        {
            var kernelMixin = kernelMixinFactory.GetOrCreate(customAssistant);
            activity?.SetTag("llm.provider.id", customAssistant.ModelProviderTemplateId);
            activity?.SetTag("llm.model.id", customAssistant.ModelDefinitionTemplateId);
            activity?.SetTag("llm.model.actual_id", customAssistant.ModelId.ActualValue);
            activity?.SetTag("llm.model.max_embedding", customAssistant.MaxTokens.ActualValue);
            return kernelMixin;
        }
        catch (Exception e)
        {
            // This method may throw if the model settings are invalid.
            activity?.SetStatus(ActivityStatusCode.Error, e.Message.Trim());
            throw;
        }
    }

    /// <summary>
    /// Kernel is very cheap to create, so we can create a new kernel for each request.
    /// This method builds the kernel based on the current settings.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    private Kernel BuildKernel(IKernelMixin kernelMixin, ChatContext chatContext, CustomAssistant customAssistant)
    {
        using var activity = _activitySource.StartActivity();

        var builder = Kernel.CreateBuilder();

        builder.Services.AddSingleton(kernelMixin.ChatCompletionService);
        builder.Services.AddSingleton(chatContext);

        if (kernelMixin.IsFunctionCallingSupported && settings.Internal.IsToolCallEnabled)
        {
            var chatPluginScope = chatPluginManager.CreateScope(chatContext, customAssistant);
            builder.Services.AddSingleton(chatPluginScope);
            activity?.SetTag("plugins.count", chatPluginScope.Plugins.AsValueEnumerable().Count());

            foreach (var plugin in chatPluginScope.Plugins)
            {
                builder.Plugins.Add(plugin);
            }
        }

        return builder.Build();
    }

    private async Task GenerateAsync(
        ChatContext chatContext,
        CustomAssistant customAssistant,
        AssistantChatMessage assistantChatMessage,
        CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity();
        activity?.SetTag("chat.context.id", chatContext.Metadata.Id);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var kernelMixin = CreateKernelMixin(customAssistant);
            var kernel = BuildKernel(kernelMixin, chatContext, customAssistant);

            var chatHistory = new ChatHistory();
            chatContext.SystemPrompt = Prompts.RenderPrompt(customAssistant.SystemPrompt.ActualValue, chatContextManager.SystemPromptVariables);

            foreach (var chatMessage in chatContext
                         .Select(n => n.Message)
                         .Where(m => !ReferenceEquals(m, assistantChatMessage)) // exclude the current assistant message
                         .Where(m => m.Role.Label is "system" or "assistant" or "user" or "tool")
                         .ToList()) // make a snapshot, otherwise async may cause thread deadlock
            {
                await foreach (var chatMessageContent in CreateChatMessageContentsAsync(chatMessage))
                {
                    chatHistory.Add(chatMessageContent);
                }
            }

            var toolCallCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var inputTokenCount = 0L;
                var outputTokenCount = 0L;
                var totalTokenCount = 0L;
                var chatSpan = new AssistantChatMessageSpan();
                assistantChatMessage.Spans.Add(chatSpan);

                AuthorRole? authorRole = null;
                IReadOnlyList<FunctionCallContent> functionCallContents;
                var assistantContentBuilder = new StringBuilder();
                var functionCallContentBuilder = new FunctionCallContentBuilder();
                var promptExecutionSettings = kernelMixin.GetPromptExecutionSettings(
                    kernelMixin.IsFunctionCallingSupported && settings.Internal.IsToolCallEnabled ?
                        FunctionChoiceBehavior.Auto(autoInvoke: false) :
                        null);

                // ReSharper disable once ExplicitCallerInfoArgument
                using (var llmStreamActivity = _activitySource.StartActivity("ChatCompletionService.GetStreamingChatMessageContents"))
                {
                    llmStreamActivity?.SetTag("chat.context.id", chatContext.Metadata.Id);
                    llmStreamActivity?.SetTag("llm.provider.id", customAssistant.ModelProviderTemplateId);
                    llmStreamActivity?.SetTag("llm.model.id", customAssistant.ModelDefinitionTemplateId);
                    llmStreamActivity?.SetTag("llm.model.actual_id", customAssistant.ModelId.ActualValue);
                    llmStreamActivity?.SetTag("llm.model.max_embedding", customAssistant.MaxTokens.ActualValue);

                    await foreach (var streamingContent in kernelMixin.ChatCompletionService.GetStreamingChatMessageContentsAsync(
                                       // They absolutely must modify this ChatHistory internally.
                                       // I can neither alter it nor inherit it.
                                       // Well, we'll see about that 😅
                                       // Let's copy the chat history to avoid modifying the original one.
                                       new ChatHistory(chatHistory),
                                       promptExecutionSettings,
                                       kernel,
                                       cancellationToken))
                    {
                        if (streamingContent.Metadata?.TryGetValue("Usage", out var usage) is true && usage is not null)
                        {
                            switch (usage)
                            {
                                case UsageContent usageContent:
                                {
                                    inputTokenCount = Math.Max(inputTokenCount, usageContent.Details.InputTokenCount ?? 0);
                                    outputTokenCount = Math.Max(outputTokenCount, usageContent.Details.OutputTokenCount ?? 0);
                                    totalTokenCount = Math.Max(totalTokenCount, usageContent.Details.TotalTokenCount ?? 0);
                                    break;
                                }
                                case UsageDetails usageDetails:
                                {
                                    inputTokenCount = Math.Max(inputTokenCount, usageDetails.InputTokenCount ?? 0);
                                    outputTokenCount = Math.Max(outputTokenCount, usageDetails.OutputTokenCount ?? 0);
                                    totalTokenCount = Math.Max(totalTokenCount, usageDetails.TotalTokenCount ?? 0);
                                    break;
                                }
                                case Usage anthropicUsage:
                                {
                                    inputTokenCount = Math.Max(inputTokenCount, anthropicUsage.InputTokens);
                                    outputTokenCount = Math.Max(outputTokenCount, anthropicUsage.OutputTokens);
                                    totalTokenCount = Math.Max(totalTokenCount, anthropicUsage.InputTokens + anthropicUsage.OutputTokens);
                                    break;
                                }
                                case ChatTokenUsage openAIUsage:
                                {
                                    inputTokenCount = Math.Max(inputTokenCount, openAIUsage.InputTokenCount);
                                    outputTokenCount = Math.Max(outputTokenCount, openAIUsage.OutputTokenCount);
                                    totalTokenCount = Math.Max(totalTokenCount, openAIUsage.TotalTokenCount);
                                    break;
                                }
                            }
                        }

                        foreach (var item in streamingContent.Items)
                        {
                            switch (item)
                            {
                                case StreamingChatMessageContent { Content.Length: > 0 } chatMessageContent:
                                {
                                    if (IsReasoningContent(chatMessageContent))
                                    {
                                        HandleReasoningMessage(chatMessageContent.Content);
                                    }
                                    else
                                    {
                                        await HandleTextMessage(chatMessageContent.Content);
                                    }
                                    break;
                                }
                                case StreamingTextContent { Text.Length: > 0 } textContent:
                                {
                                    if (IsReasoningContent(textContent))
                                    {
                                        HandleReasoningMessage(textContent.Text);
                                    }
                                    else
                                    {
                                        await HandleTextMessage(textContent.Text);
                                    }
                                    break;
                                }
                                case StreamingReasoningContent reasoningContent:
                                {
                                    HandleReasoningMessage(reasoningContent.Text);
                                    break;
                                }
                            }

                            bool IsReasoningContent(StreamingKernelContent content) =>
                                streamingContent.Metadata?.TryGetValue("reasoning", out var reasoning) is true && reasoning is true ||
                                content.Metadata?.TryGetValue("reasoning", out reasoning) is true && reasoning is true;

                            DispatcherOperation<ObservableStringBuilder> HandleTextMessage(string text)
                            {
                                // Mark the reasoning as finished when we receive the first content chunk.
                                if (chatSpan.ReasoningOutput is not null && chatSpan.ReasoningFinishedAt is null)
                                {
                                    chatSpan.ReasoningOutput = chatSpan.ReasoningOutput.TrimEnd();
                                    chatSpan.ReasoningFinishedAt = DateTimeOffset.UtcNow;
                                }

                                assistantContentBuilder.Append(text);
                                return Dispatcher.UIThread.InvokeAsync(() => chatSpan.MarkdownBuilder.Append(text));
                            }

                            void HandleReasoningMessage(string text)
                            {
                                if (chatSpan.ReasoningOutput is null) chatSpan.ReasoningOutput = text;
                                else chatSpan.ReasoningOutput += text;
                            }
                        }

                        // TODO: move to openai sdk
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

                    // Mark the reasoning as finished if we have any reasoning output.
                    if (chatSpan.ReasoningOutput is not null && chatSpan.ReasoningFinishedAt is null)
                    {
                        chatSpan.ReasoningFinishedAt = DateTimeOffset.UtcNow;
                    }

                    if (assistantContentBuilder.Length > 0) chatHistory.AddAssistantMessage(assistantContentBuilder.ToString());

                    functionCallContents = functionCallContentBuilder.Build();
                    assistantChatMessage.InputTokenCount += inputTokenCount;
                    assistantChatMessage.OutputTokenCount += outputTokenCount;
                    assistantChatMessage.TotalTokenCount += totalTokenCount;

                    llmStreamActivity?.SetTag("chat.history.count", chatHistory.Count);
                    llmStreamActivity?.SetTag("chat.embedding.input", inputTokenCount);
                    llmStreamActivity?.SetTag("chat.embedding.output", outputTokenCount);
                    llmStreamActivity?.SetTag("chat.embedding.total", totalTokenCount);
                    llmStreamActivity?.SetTag("chat.response.length", assistantContentBuilder.Length);
                    llmStreamActivity?.SetTag("chat.response.tool_call.count", functionCallContents.Count);
                }

                if (functionCallContents.Count <= 0) break;
                toolCallCount += functionCallContents.Count;

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
                    chatSpan.FunctionCalls.Add(functionCallChatMessage);

                    // Add call message to the chat history.
                    var functionCallMessage = new ChatMessageContent(AuthorRole.Assistant, content: null);
                    chatHistory.Add(functionCallMessage);

                    // Iterate through the function call contents in the group.
                    foreach (var functionCallContent in functionCallContentGroup)
                    {
                        // ReSharper disable once ExplicitCallerInfoArgument
                        using var functionCallActivity = _activitySource.StartActivity("Tool.InvokeFunction");
                        functionCallActivity?.SetTag("tool.plugin_name", functionCallContent.PluginName);
                        functionCallActivity?.SetTag("tool.function_name", functionCallContent.FunctionName);

                        functionCallChatMessage.Calls.Add(functionCallContent);
                        functionCallMessage.Items.Add(functionCallContent);

                        FunctionResultContent resultContent;
                        try
                        {
                            resultContent = await functionCallContent.InvokeAsync(kernel, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            ex = HandledSystemException.Handle(ex);
                            functionCallActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                            resultContent = new FunctionResultContent(functionCallContent, $"Error: {ex.Message}");
                            functionCallChatMessage.ErrorMessageKey = ex.GetFriendlyMessage();

                            logger.LogError(ex, "Error invoking function '{FunctionName}'", functionCallContent.FunctionName);
                        }

                        functionCallChatMessage.Results.Add(resultContent);
                        chatHistory.Add(new ChatMessageContent(AuthorRole.Tool, [resultContent]));

                        if (await TryCreateExtraToolCallResultsContentAsync(functionCallChatMessage) is { } extraToolCallResultsContent)
                        {
                            chatHistory.Add(extraToolCallResultsContent);
                        }

                        if (functionCallChatMessage.ErrorMessageKey is not null)
                        {
                            break; // If an error occurs, we stop processing further function calls.
                        }
                    }

                    functionCallChatMessage.FinishedAt = DateTimeOffset.UtcNow;
                    functionCallChatMessage.IsBusy = false;
                }

                chatSpan.FinishedAt = DateTimeOffset.UtcNow;
            }

            activity?.SetTag("tool_calls.count", toolCallCount);

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
            e = HandledChatException.Handle(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message.Trim());
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
                    // ReSharper disable once ForCanBeConvertedToForeach
                    // foreach would create an enumerator object, which will cause thread lock issues.
                    for (var spanIndex = 0; spanIndex < assistant.Spans.Count; spanIndex++)
                    {
                        var span = assistant.Spans[spanIndex];
                        if (span.MarkdownBuilder.Length > 0)
                        {
                            yield return new ChatMessageContent(chatMessage.Role, span.MarkdownBuilder.ToString());
                        }

                        // ReSharper disable once ForCanBeConvertedToForeach
                        // foreach would create an enumerator object, which will cause thread lock issues.
                        for (var callIndex = 0; callIndex < span.FunctionCalls.Count; callIndex++)
                        {
                            var functionCallChatMessage = span.FunctionCalls[callIndex];
                            await foreach (var actionChatMessageContent in CreateChatMessageContentsAsync(functionCallChatMessage))
                            {
                                yield return actionChatMessageContent;
                            }
                        }
                    }
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

                    // ReSharper disable once ForCanBeConvertedToForeach
                    // foreach would create an enumerator object, which will cause thread lock issues.
                    for (var resultIndex = 0; resultIndex < functionCall.Results.Count; resultIndex++)
                    {
                        var result = functionCall.Results[resultIndex];
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
            // Limit to 10 attachments
            // snapshot the attachments to avoid thread issues.
            foreach (var attachment in attachments.Take(10).ToList())
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
        using var activity = _activitySource.StartActivity();

        try
        {
            var language = settings.Common.Language == "default" ? "en-US" : settings.Common.Language;

            activity?.SetTag("chat.context.id", metadata.Id);
            activity?.SetTag("user_message.length", userMessage.Length);
            activity?.SetTag("assistant_message.length", assistantMessage.Length);
            activity?.SetTag("system_language", language);

            var chatHistory = new ChatHistory
            {
                new ChatMessageContent(
                    AuthorRole.System,
                    Prompts.RenderPrompt(
                        Prompts.TitleGeneratorPrompt,
                        new Dictionary<string, Func<string>>
                        {
                            { "UserMessage", () => userMessage.SafeSubstring(0, 2048) },
                            { "AssistantMessage", () => assistantMessage.SafeSubstring(0, 2048) },
                            { "SystemLanguage", () => language }
                        })),
            };
            var chatMessageContent = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);
            metadata.Topic = chatMessageContent.Content?.Trim().Trim('.', '!', '?', '。', '！', '？').SafeSubstring(0, 50);
            activity?.SetTag("topic.length", metadata.Topic?.Length ?? 0);
        }
        catch (Exception e)
        {
            e = HandledChatException.Handle(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            logger.LogError(e, "Failed to generate chat title");
        }
    }
}