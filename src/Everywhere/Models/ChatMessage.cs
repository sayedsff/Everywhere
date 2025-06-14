using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Views;
using Lucide.Avalonia;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Everywhere.Models;

public abstract class ChatMessage(AuthorRole role, string? actualContent = null) : ObservableObject
{
    public AuthorRole Role => role;
    public string? ActualContent { get; set; } = actualContent;
    public BusyInlineCollection InlineCollection { get; } = new();
    public bool IsBusy
    {
        get => InlineCollection.IsBusy;
        set
        {
            InlineCollection.IsBusy = value;
            OnPropertyChanged();
        }
    }

    public override string? ToString() => ActualContent ?? InlineCollection.Text;
}

public class SystemChatMessage(string systemPrompt) : ChatMessage(AuthorRole.System, systemPrompt);

public class AssistantChatMessage : ChatMessage
{
    public AssistantChatMessage() : base(AuthorRole.Assistant)
    {
        IsBusy = true;
    }

    public AssistantChatMessage(string actualContent) : base(AuthorRole.Assistant, actualContent)
    {
        InlineCollection.Add(actualContent);
    }
}

public class UserChatMessage(string userPrompt, IReadOnlyList<AssistantAttachment> attachments) : ChatMessage(AuthorRole.User, userPrompt)
{
    public IReadOnlyList<AssistantAttachment> Attachments => attachments;
}

public partial class ActionChatMessage : ChatMessage
{
    [ObservableProperty]
    public partial LucideIconKind Icon { get; set; }

    [ObservableProperty]
    public partial DynamicResourceKey? HeaderKey { get; set; }

    public ActionChatMessage(
        AuthorRole role,
        LucideIconKind icon = LucideIconKind.Brain,
        DynamicResourceKey? headerKey = null,
        string? actualContent = null) : base(role, actualContent)
    {
        Icon = icon;
        HeaderKey = headerKey;
        IsBusy = true;
    }
}