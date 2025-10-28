using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Chat.Plugins;
using Everywhere.Serialization;
using LiveMarkdown.Avalonia;
using Lucide.Avalonia;
using MessagePack;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Chat;

[MessagePackObject(OnlyIncludeKeyedMembers = true)]
[Union(0, typeof(SystemChatMessage))]
[Union(1, typeof(AssistantChatMessage))]
[Union(2, typeof(UserChatMessage))]
[Union(3, typeof(ActionChatMessage))]
[Union(4, typeof(FunctionCallChatMessage))]
public abstract partial class ChatMessage : ObservableObject
{
    public abstract AuthorRole Role { get; }

    [IgnoreMember]
    [JsonIgnore]
    [ObservableProperty]
    public partial bool IsBusy { get; set; }
}

public interface IChatMessageWithAttachments
{
    IEnumerable<ChatAttachment> Attachments { get; }
}

[MessagePackObject(OnlyIncludeKeyedMembers = true)]
public partial class SystemChatMessage(string systemPrompt) : ChatMessage
{
    public override AuthorRole Role => AuthorRole.System;

    [Key(0)]
    [ObservableProperty]
    public partial string SystemPrompt { get; set; } = systemPrompt;

    public override string ToString() => SystemPrompt;
}

[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class AssistantChatMessage : ChatMessage
{
    public override AuthorRole Role => AuthorRole.Assistant;

    [Key(0)]
    private string? Content
    {
        get => null; // for forward compatibility
        init
        {
            if (!value.IsNullOrEmpty()) EnsureInitialSpan().MarkdownBuilder.Append(value);
        }
    }

    [Key(1)]
    [ObservableProperty]
    public partial DynamicResourceKeyBase? ErrorMessageKey { get; set; }

    [Key(2)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(3)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset FinishedAt { get; set; }

    [IgnoreMember]
    [JsonIgnore]
    public double ElapsedSeconds => Math.Max((FinishedAt - CreatedAt).TotalSeconds, 0);

    [Key(4)]
    private ObservableList<FunctionCallChatMessage>? FunctionCalls
    {
        get => null; // for forward compatibility
        init
        {
            if (value is { Count: > 0 }) EnsureInitialSpan().FunctionCalls.AddRange(value);
        }
    }

    /// <summary>
    /// Each span represents a part of the message content and function calls.
    /// </summary>
    [Key(5)]
    public ObservableList<AssistantChatMessageSpan> Spans { get; set; } = [];

    [Key(6)]
    [ObservableProperty]
    public partial long InputTokenCount { get; set; }

    [Key(7)]
    [ObservableProperty]
    public partial long OutputTokenCount { get; set; }

    [Key(8)]
    [ObservableProperty]
    public partial double TotalTokenCount { get; set; }

    private AssistantChatMessageSpan EnsureInitialSpan()
    {
        if (Spans.Count == 0) Spans.Add(new AssistantChatMessageSpan());
        return Spans[^1];
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach (var span in Spans.AsValueEnumerable().Where(s => s.MarkdownBuilder.Length > 0))
        {
            builder.AppendLine(span.MarkdownBuilder.ToString());
        }

        return builder.TrimEnd().ToString();
    }
}

/// <summary>
/// Represents a span of content in an assistant chat message.
/// A span can contain markdown content and associated function calls.
/// </summary>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class AssistantChatMessageSpan : ObservableObject
{
    public ObservableStringBuilder MarkdownBuilder { get; }

    [Key(0)]
    private string Content
    {
        get => MarkdownBuilder.ToString();
        init => MarkdownBuilder.Append(value);
    }

    [Key(1)]
    public ObservableList<FunctionCallChatMessage> FunctionCalls { get; set; } = [];

    [Key(2)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(3)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset FinishedAt { get; set; }

    [IgnoreMember]
    [JsonIgnore]
    public double ElapsedSeconds => Math.Max((FinishedAt - CreatedAt).TotalSeconds, 0);

    [Key(4)]
    [ObservableProperty]
    public partial string? ReasoningOutput { get; set; }

    [Key(5)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReasoningElapsedSeconds))]
    public partial DateTimeOffset? ReasoningFinishedAt { get; set; }

    [IgnoreMember]
    [JsonIgnore]
    public double ReasoningElapsedSeconds => Math.Max((ReasoningFinishedAt.GetValueOrDefault() - CreatedAt).TotalSeconds, 0);

    public AssistantChatMessageSpan()
    {
        MarkdownBuilder = new ObservableStringBuilder();
        MarkdownBuilder.Changed += delegate
        {
            OnPropertyChanged(nameof(Content));
        };
    }
}

[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class UserChatMessage(string userPrompt, IEnumerable<ChatAttachment> attachments) : ChatMessage, IChatMessageWithAttachments
{
    public override AuthorRole Role => AuthorRole.User;

    /// <summary>
    /// The actual prompt that sends to the LLM.
    /// </summary>
    [Key(0)]
    public string UserPrompt { get; set; } = userPrompt;

    [Key(1)]
    public IEnumerable<ChatAttachment> Attachments { get; set; } = attachments;

    /// <summary>
    /// The inlines that display in the chat message.
    /// </summary>
    public InlineCollection Inlines { get; } = new();

    [Key(2)]
    private IEnumerable<MessagePackInline> MessagePackInlines
    {
        get => Dispatcher.UIThread.InvokeOnDemand(() => Inlines.Select(MessagePackInline.FromInline).ToImmutableArray());
        set => Dispatcher.UIThread.InvokeOnDemand(() => Inlines.Reset(value.Select(i => i.ToInline())));
    }

    [Key(3)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public override string ToString() => UserPrompt;
}

/// <summary>
/// Represents an action message in the chat.
/// </summary>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ActionChatMessage : ChatMessage
{
    [Key(0)]
    public override AuthorRole Role { get; }

    [Key(1)]
    [ObservableProperty]
    public partial LucideIconKind Icon { get; set; }

    [Key(2)]
    [ObservableProperty]
    public partial DynamicResourceKey? HeaderKey { get; set; }

    [Key(3)]
    [ObservableProperty]
    public partial string? Content { get; set; }

    [Key(4)]
    [ObservableProperty]
    public partial DynamicResourceKeyBase? ErrorMessageKey { get; set; }

    [Key(5)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(6)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset FinishedAt { get; set; }

    [IgnoreMember]
    [JsonIgnore]
    public double ElapsedSeconds => Math.Max((FinishedAt - CreatedAt).TotalSeconds, 0);

    [SerializationConstructor]
    protected ActionChatMessage() { }

    public ActionChatMessage(AuthorRole role, LucideIconKind icon, DynamicResourceKey? headerKey)
    {
        Role = role;
        Icon = icon;
        HeaderKey = headerKey;
    }
}

/// <summary>
/// Represents a function call action message in the chat.
/// </summary>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class FunctionCallChatMessage : ChatMessage, IChatMessageWithAttachments, IChatPluginDisplaySink
{
    [Key(0)]
    public override AuthorRole Role => AuthorRole.Tool;

    [Key(1)]
    [ObservableProperty]
    public partial LucideIconKind Icon { get; set; }

    /// <summary>
    /// Obsolete: Use HeaderKey instead.
    /// </summary>
    [Key(2)]
    private DynamicResourceKey? ObsoleteHeaderKey
    {
        get => null; // for forward compatibility
        set => HeaderKey = value;
    }

    [Key(3)]
    public string? Content { get; set; }

    [Key(4)]
    [ObservableProperty]
    public partial DynamicResourceKeyBase? ErrorMessageKey { get; set; }

    [Key(5)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(6)]
    public List<FunctionCallContent> Calls { get; set; } = [];

    [Key(7)]
    public List<FunctionResultContent> Results { get; set; } = [];

    [Key(8)]
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset FinishedAt { get; set; } = DateTimeOffset.UtcNow;

    [IgnoreMember]
    [JsonIgnore]
    public double ElapsedSeconds => Math.Max((FinishedAt - CreatedAt).TotalSeconds, 0);

    [Key(9)]
    [ObservableProperty]
    public partial DynamicResourceKeyBase? HeaderKey { get; set; }

    /// <summary>
    /// The display blocks that make up the content of this function call message,
    /// which can include text, markdown, progress indicators, file references, and function call/result displays.
    /// These blocks are rendered in the chat UI to present the function call information to the user.
    /// And can be serialized for persistence or transmission.
    /// </summary>
    /// <remarks>
    /// The reason why we need to populate the Content property of function call/result display blocks
    /// is that during deserialization, the references to the actual FunctionCallContent and FunctionResultContent
    /// objects are not automatically restored. Therefore, we need to manually link them back
    /// based on their IDs after deserialization. This ensures that the display blocks have access
    /// to the full details of the function calls and results they are meant to represent.
    /// </remarks>
    [Key(10)]
    public ObservableCollection<ChatPluginDisplayBlock> DisplayBlocks { get; set; } = [];

    [Key(11)]
    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [IgnoreMember]
    [JsonIgnore]
    public bool IsWaitingForUserInput => DisplayBlocks.Any(db => db.IsWaitingForUserInput);

    /// <summary>
    /// Attachments associated with this action message. Used to provide additional context of a tool call result.
    /// </summary>
    [IgnoreMember]
    public IEnumerable<ChatAttachment> Attachments => Results.Select(r => r.Result).OfType<ChatAttachment>();

    [SerializationConstructor]
    private FunctionCallChatMessage() { }

    public FunctionCallChatMessage(LucideIconKind icon, DynamicResourceKeyBase? headerKey)
    {
        Icon = icon;
        HeaderKey = headerKey;

        DisplayBlocks.CollectionChanged += (_, e) =>
        {
            OnPropertyChanged(nameof(IsWaitingForUserInput));

            if (e.NewItems is { } newItems)
            {
                foreach (var item in newItems.OfType<ChatPluginDisplayBlock>())
                {
                    item.PropertyChanged += HandleDisplayBlockPropertyChanged;
                }
            }

            if (e.OldItems is { } oldItems)
            {
                foreach (var item in oldItems.OfType<ChatPluginDisplayBlock>())
                {
                    item.PropertyChanged -= HandleDisplayBlockPropertyChanged;
                }
            }
        };

        void HandleDisplayBlockPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ChatPluginDisplayBlock.IsWaitingForUserInput)) OnPropertyChanged(nameof(IsWaitingForUserInput));
        }
    }

    public void AppendText(string text)
    {
        DisplayBlocks.Add(new ChatPluginTextDisplayBlock(text));
    }

    public void AppendDynamicResourceKey(DynamicResourceKeyBase resourceKey)
    {
        DisplayBlocks.Add(new ChatPluginDynamicResourceKeyDisplayBlock(resourceKey));
    }

    public ObservableStringBuilder AppendMarkdown()
    {
        var markdownBlock = new ChatPluginMarkdownDisplayBlock();
        DisplayBlocks.Add(markdownBlock);
        return markdownBlock.MarkdownBuilder;
    }

    public IProgress<double> AppendProgress(DynamicResourceKeyBase headerKey)
    {
        var progressBlock = new ChatPluginProgressDisplayBlock(headerKey);
        DisplayBlocks.Add(progressBlock);
        return progressBlock.ProgressReporter;
    }

    public void AppendFileReferences(params IReadOnlyList<ChatPluginFileReference> references)
    {
        DisplayBlocks.Add(new ChatPluginFileReferencesDisplayBlock(references));
    }

    public void AppendFileDifference(TextDifference difference, string originalText)
    {
        DisplayBlocks.Add(new ChatPluginFileDifferenceDisplayBlock(difference, originalText));
    }

    public void AppendUrls(IReadOnlyList<ChatPluginUrl> urls)
    {
        DisplayBlocks.Add(new ChatPluginUrlsDisplayBlock(urls));
    }
}