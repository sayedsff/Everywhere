using Avalonia.Controls.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Collections;
using Microsoft.Extensions.AI;

namespace Everywhere.Models;

public class ChatMessage(ChatRole role, string? actualContent = null) : ObservableObject
{
    public ChatRole Role => role;

    public BusyInlineCollection InlineCollection { get; } = new();

    public override string ToString() => actualContent ?? string.Join(null, InlineCollection.Select(
        i => i switch
        {
            Run run => run.Text,
            LineBreak => "\n",
            _ => string.Empty
        }));
}