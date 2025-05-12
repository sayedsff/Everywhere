using Avalonia.Controls.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Collections;
using IconPacks.Avalonia.Material;
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

    public override string ToString() => ActualContent ?? string.Join(
        null,
        InlineCollection.Select(
            i => i switch
            {
                Run run => run.Text,
                LineBreak => "\n",
                _ => string.Empty
            }));
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

public class UserChatMessage(string? actualContent = null) : ChatMessage(AuthorRole.User, actualContent);

public partial class ActionChatMessage : ChatMessage
{
    [ObservableProperty]
    public partial PackIconMaterialKind Icon { get; set; }

    [ObservableProperty]
    public partial DynamicResourceKey? HeaderKey { get; set; }

    public ActionChatMessage(AuthorRole role,
        PackIconMaterialKind icon = PackIconMaterialKind.Brain,
        DynamicResourceKey? headerKey = null,
        string? actualContent = null) : base(role, actualContent)
    {
        Icon = icon;
        HeaderKey = headerKey;
        IsBusy = true;
    }
}