namespace Everywhere.Chat.Plugins;

[Flags]
public enum ChatFunctionPermissions
{
    /// <summary>
    /// No permissions granted. This is the default state.
    /// </summary>
    None = 0,

    /// <summary>
    /// Allows reading content from the screen (e.g., screenshots, UI element text).
    /// </summary>
    ScreenRead = 1 << 0, // 1

    /// <summary>
    /// Allows displaying information on the screen (e.g., notifications, UI changes) or interacting with the screen.
    /// </summary>
    ScreenAccess = ScreenRead | 1 << 1, // 1 | 2 = 3

    /// <summary>
    /// Allows accessing the internet. This covers both sending and receiving data.
    /// </summary>
    InternetAccess = 1 << 2, // 4

    /// <summary>
    /// Allows reading from the system clipboard.
    /// </summary>
    ClipboardRead = 1 << 3, // 8

    /// <summary>
    /// Allows reading and writing to the system clipboard.
    /// </summary>
    ClipboardAccess = ClipboardRead | 1 << 4, // 8 | 16 = 24

    /// <summary>
    /// Allows reading from the local file system.
    /// </summary>
    FileRead = 1 << 5, // 32

    /// <summary>
    /// Allows reading, writing or modifying files on the local file system.
    /// </summary>
    FileAccess = FileRead | 1 << 6, // 32 | 64 = 96

    /// <summary>
    /// Allows executing local shell commands. This is a high-risk permission
    /// that can potentially perform any system-level action.
    /// </summary>
    ShellExecute = ScreenAccess | InternetAccess | ClipboardAccess | FileAccess | 1 << 7,

    AllAccess = ~0
}