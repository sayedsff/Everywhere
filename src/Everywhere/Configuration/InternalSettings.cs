using CommunityToolkit.Mvvm.ComponentModel;

namespace Everywhere.Configuration;

public partial class InternalSettings : SettingsCategory
{
    /// <summary>
    /// Used to popup welcome dialog on first launch and update.
    /// </summary>
    [ObservableProperty]
    public partial string? PreviousLaunchVersion { get; set; }

    /// <summary>
    /// Pop a tray notification when the application is launched for the first time.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFirstTimeHideToTrayIcon { get; set; } = true;

    [ObservableProperty]
    public partial bool IsToolCallEnabled { get; set; }

    public int MaxChatAttachmentCount { get; set; } = 10;

    [ObservableProperty]
    public partial bool IsMainViewSidebarExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsChatWindowPinned { get; set; }

    [ObservableProperty]
    public partial string? ChatInputBoxText { get; set; }

    [ObservableProperty]
    public partial int VisualTreeTokenLimit { get; set; } = 4096;
}