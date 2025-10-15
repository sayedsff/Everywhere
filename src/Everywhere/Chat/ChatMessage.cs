using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
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
        var builder = new System.Text.StringBuilder();
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
public partial class FunctionCallChatMessage : ChatMessage, IChatMessageWithAttachments
{
    [Key(0)]
    public override AuthorRole Role => AuthorRole.Tool;

    [Key(1)]
    [ObservableProperty]
    public partial LucideIconKind Icon { get; set; }

    [Key(2)]
    [ObservableProperty]
    public partial DynamicResourceKey? HeaderKey { get; set; }

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
    public ObservableList<FunctionCallContent> Calls { get; set; } = [];

    [Key(7)]
    public ObservableList<FunctionResultContent> Results { get; set; } = [];

    [Key(8)]
    [ObservableProperty]

    [NotifyPropertyChangedFor(nameof(ElapsedSeconds))]
    public partial DateTimeOffset FinishedAt { get; set; } = DateTimeOffset.UtcNow;

    [IgnoreMember]
    [JsonIgnore]
    public double ElapsedSeconds => Math.Max((FinishedAt - CreatedAt).TotalSeconds, 0);

    /// <summary>
    /// Attachments associated with this action message. Used to provide additional context of a tool call result.
    /// </summary>
    [IgnoreMember]
    public IEnumerable<ChatAttachment> Attachments => Results.Select(r => r.Result).OfType<ChatAttachment>();

    [SerializationConstructor]
    private FunctionCallChatMessage() { }

    public FunctionCallChatMessage(LucideIconKind icon, DynamicResourceKey? headerKey)
    {
        Icon = icon;
        HeaderKey = headerKey;
    }
}