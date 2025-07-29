using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Serialization;
using LiveMarkdown.Avalonia;
using Lucide.Avalonia;
using MessagePack;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Everywhere.Models;

[MessagePackObject(OnlyIncludeKeyedMembers = true)]
[Union(0, typeof(SystemChatMessage))]
[Union(1, typeof(AssistantChatMessage))]
[Union(2, typeof(UserChatMessage))]
[Union(3, typeof(ActionChatMessage))]
public abstract partial class ChatMessage : ObservableObject
{
    public abstract AuthorRole Role { get; }

    [IgnoreMember]
    [JsonIgnore]
    [ObservableProperty]
    public partial bool IsBusy { get; set; }
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

    public AssistantChatMessage()
    {
        MarkdownBuilder = new ObservableStringBuilder();
        MarkdownBuilder.Changed += delegate
        {
            OnPropertyChanged(nameof(Content));
        };
    }

    public override string ToString() => Content;
}

[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class UserChatMessage(string userPrompt, IReadOnlyList<ChatAttachment> attachments) : ChatMessage
{
    public override AuthorRole Role => AuthorRole.User;

    /// <summary>
    /// The actual prompt that sends to the LLM.
    /// </summary>
    [Key(0)]
    public string UserPrompt { get; set; } = userPrompt;

    [Key(1)]
    public IReadOnlyList<ChatAttachment> Attachments => attachments;

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

    public override string ToString() => UserPrompt;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ActionChatMessage : ChatMessage
{
    [Key(0)]
    [MessagePackFormatter(typeof(AuthorRoleMessagePackFormatter))]
    public override AuthorRole Role { get; }

    [ObservableProperty]
    [Key(1)]
    public partial LucideIconKind Icon { get; set; }

    [ObservableProperty]
    [Key(2)]
    public partial DynamicResourceKey? HeaderKey { get; set; }

    /// <summary>
    /// Actual content that llm returns.
    /// </summary>
    [Key(3)]
    public string? Content { get; set; }

    [Key(4)]
    [ObservableProperty]
    public partial DynamicResourceKeyBase? ErrorMessageKey { get; set; }

    [SerializationConstructor]
    private ActionChatMessage() { }

    public ActionChatMessage(AuthorRole role, LucideIconKind icon, DynamicResourceKey? headerKey)
    {
        Role = role;
        Icon = icon;
        HeaderKey = headerKey;
    }
}