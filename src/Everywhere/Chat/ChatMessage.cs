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
    public string SystemPrompt => systemPrompt;

    public override string ToString() => systemPrompt;
}

[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class AssistantChatMessage : ChatMessage
{
    public override AuthorRole Role => AuthorRole.Assistant;

    public ObservableStringBuilder MarkdownBuilder { get; }

    [Key(0)]
    private string Content
    {
        get => MarkdownBuilder.ToString();
        init => MarkdownBuilder.Append(value);
    }

    [Key(1)]
    [ObservableProperty]
    public partial DynamicResourceKeyBase? ErrorMessageKey { get; set; }

    [Key(2)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(3)]
    public DateTimeOffset FinishedAt { get; set; } = DateTimeOffset.UtcNow;

    [Key(4)]
    public ObservableList<FunctionCallChatMessage> FunctionCalls { get; set; } = [];

    public AssistantChatMessage()
    {
        MarkdownBuilder = new ObservableStringBuilder();
        MarkdownBuilder.Changed += delegate
        {
            OnPropertyChanged(nameof(Content));
        };
    }

    public override string ToString()
    {
        if (ErrorMessageKey is null) return Content;
        return Content + ErrorMessageKey;
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
    public string? Content { get; set; }

    [Key(4)]
    [ObservableProperty]
    public partial DynamicResourceKeyBase? ErrorMessageKey { get; set; }

    [Key(5)]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

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
public partial class FunctionCallChatMessage : ActionChatMessage, IChatMessageWithAttachments
{
    [Key(6)]
    public List<FunctionCallContent> Calls { get; set; } = [];

    [Key(7)]
    public List<FunctionResultContent> Results { get; set; } = [];

    /// <summary>
    /// Attachments associated with this action message. Used to provide additional context of a tool call result.
    /// </summary>
    [IgnoreMember]
    public IEnumerable<ChatAttachment> Attachments => Results.Select(r => r.Result).OfType<ChatAttachment>();

    [SerializationConstructor]
    private FunctionCallChatMessage() { }

    public FunctionCallChatMessage(LucideIconKind icon, DynamicResourceKey? headerKey) : base(AuthorRole.Tool, icon, headerKey)
    {
        Icon = icon;
        HeaderKey = headerKey;
    }
}