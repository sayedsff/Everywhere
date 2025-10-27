using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Chat;
using Everywhere.Interop;
using Lucide.Avalonia;

namespace Everywhere.Configuration;

public partial class ChatWindowSettings : SettingsCategory
{
    public override string Header => "ChatWindow";

    public override LucideIconKind Icon => LucideIconKind.MessageCircle;

    [ObservableProperty]
    public partial KeyboardShortcut Shortcut { get; set; } = new(Key.E, KeyModifiers.Control | KeyModifiers.Shift);

    [ObservableProperty]
    public partial ChatWindowPinMode WindowPinMode { get; set; }

    /// <summary>
    /// Temporary chat mode when creating a new chat.
    /// </summary>
    [ObservableProperty]
    public partial TemporaryChatMode TemporaryChatMode { get; set; }

    [ObservableProperty]
    public partial VisualTreeDetailLevel VisualTreeDetailLevel { get; set; } = VisualTreeDetailLevel.Compact;

    /// <summary>
    /// When enabled, automatically add focused element as attachment when opening chat window.
    /// </summary>
    [ObservableProperty]
    public partial bool AutomaticallyAddElement { get; set; } = true;

    /// <summary>
    /// When enabled, chat window can generate response in the background when closed.
    /// </summary>
    [ObservableProperty]
    public partial bool AllowRunInBackground { get; set; } = true;

    /// <summary>
    /// When enabled, show chat statistics in the chat window.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowChatStatistics { get; set; } = true;

    // [ObservableProperty]
    // [SettingsSelectionItem(ItemsSourceBindingPath = "")]
    // public partial Guid TitleGeneratorAssistantId { get; set; }
    //
    // [ObservableProperty]
    // [SettingsStringItem(Watermark = Prompts.TitleGeneratorPrompt, IsMultiline = true, Height = 50)]
    // public partial Customizable<string> TitleGeneratorPromptTemplate { get; set; } = Prompts.TitleGeneratorPrompt;
}