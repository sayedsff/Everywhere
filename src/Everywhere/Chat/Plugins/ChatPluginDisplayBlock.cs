using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Everywhere.Interop;
using LiveMarkdown.Avalonia;
using Lucide.Avalonia;
using MessagePack;
using ObservableCollections;
using ZLinq;

namespace Everywhere.Chat.Plugins;

/// <summary>
/// Used to represent a block of content displayed by a chat plugin.
/// </summary>
[MessagePackObject]
[Union(0, typeof(ChatPluginContainerDisplayBlock))]
[Union(1, typeof(ChatPluginTextDisplayBlock))]
[Union(2, typeof(ChatPluginDynamicResourceKeyDisplayBlock))]
[Union(3, typeof(ChatPluginMarkdownDisplayBlock))]
[Union(4, typeof(ChatPluginProgressDisplayBlock))]
[Union(5, typeof(ChatPluginFileReferencesDisplayBlock))]
[Union(6, typeof(ChatPluginFileDifferenceDisplayBlock))]
[Union(7, typeof(ChatPluginUrlsDisplayBlock))]
public abstract partial class ChatPluginDisplayBlock : ObservableObject
{
    /// <summary>
    /// Indicates whether this display block is waiting for user input.
    /// </summary>
    [IgnoreMember]
    public virtual bool IsWaitingForUserInput => false;
}

/// <summary>
/// Represents a container block that can hold other display blocks.
/// </summary>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginContainerDisplayBlock : ChatPluginDisplayBlock
{
    [Key(0)]
    public ObservableList<ChatPluginDisplayBlock> Children { get; private set; } = [];
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginTextDisplayBlock(string text) : ChatPluginDisplayBlock
{
    [Key(0)]
    public string Text { get; } = text;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginDynamicResourceKeyDisplayBlock(DynamicResourceKeyBase key) : ChatPluginDisplayBlock
{
    [Key(0)]
    public DynamicResourceKeyBase Key { get; } = key;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginMarkdownDisplayBlock : ChatPluginDisplayBlock
{
    public ObservableStringBuilder MarkdownBuilder { get; } = new();

    [Key(0)]
    private string Markdown
    {
        get => MarkdownBuilder.ToString();
        set => MarkdownBuilder.Clear().Append(value);
    }
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginProgressDisplayBlock(DynamicResourceKeyBase headerKey) : ChatPluginDisplayBlock
{
    [field: AllowNull, MaybeNull]
    public Progress<double> ProgressReporter => field ??= new Progress<double>(value => Progress = value);

    [Key(0)]
    public DynamicResourceKeyBase HeaderKey { get; } = headerKey;

    [Key(1)]
    [ObservableProperty]
    public partial double Progress { get; set; }
}

/// <summary>
/// Represents a reference to a file or folder in a chat plugin display block.
/// </summary>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatPluginFileReference(string fullPath, DynamicResourceKeyBase? displayNameKey = null)
{
    [Key(0)]
    public string FullPath { get; } = fullPath;

    [Key(1)]
    public DynamicResourceKeyBase? DisplayNameKey { get; } = displayNameKey;

    [IgnoreMember]
    public Task<LucideIconKind> IconAsync => Task.Run(() =>
    {
        if (Directory.Exists(FullPath)) return LucideIconKind.Folder;
        return Path.GetExtension(FullPath).ToLowerInvariant() switch
        {
            ".cs" or ".rs" or ".py" or ".js" or ".ts" or ".cpp" or ".c" or ".html" or ".css" or ".java" => LucideIconKind.FileCode,
            ".txt" or ".md" or ".markdown" or ".doc" or ".docx" or ".rtf" => LucideIconKind.FileText,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => LucideIconKind.FileImage,
            ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv" => LucideIconKind.FileVideoCamera,
            ".sh" or ".exe" or ".bat" or ".cmd" or ".ps1" => LucideIconKind.FileTerminal,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => LucideIconKind.FileArchive,
            ".mp3" or ".wav" or ".flac" or ".aac" => LucideIconKind.FileAudio,
            _ => LucideIconKind.File
        };
    });

    [RelayCommand]
    private void OpenFileLocation()
    {
        ServiceLocator.Resolve<INativeHelper>().OpenFileLocation(FullPath);
    }
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginFileReferencesDisplayBlock(params IReadOnlyList<ChatPluginFileReference> references) : ChatPluginDisplayBlock
{
    [Key(0)]
    public IReadOnlyList<ChatPluginFileReference> References { get; } = references.AsValueEnumerable().Take(10).ToList();

    [Key(1)]
    public int TotalReferenceCount { get; set; } = references.Count;

    [IgnoreMember]
    public bool HasMoreReferences => TotalReferenceCount > References.Count;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
[method: SerializationConstructor]
public sealed partial class ChatPluginFileDifferenceDisplayBlock(TextDifference difference) : ChatPluginDisplayBlock
{
    [Key(0)]
    public TextDifference Difference { get; } = difference;

    public string? OriginalText { get; init; }

    public override bool IsWaitingForUserInput => Difference.Acceptance is null;

    public ChatPluginFileDifferenceDisplayBlock(TextDifference difference, string originalText) : this(difference)
    {
        OriginalText = originalText;

        // Only subscribe to property changes in this constructor since deserialization will not change the Difference property.
        difference.PropertyChanged += HandleDifferencePropertyChanged;
    }

    /// <summary>
    /// Handles property changes on the TextDifference to update the IsWaitingForUserInput property.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleDifferencePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TextDifference.Acceptance)) OnPropertyChanged(nameof(IsWaitingForUserInput));
    }
}

/// <summary>
/// Represents a URL with an optional display name key. Usage example: web search results.
/// </summary>
/// <param name="url"></param>
/// <param name="displayNameKey"></param>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginUrl(string url, DynamicResourceKeyBase displayNameKey)
{
    [Key(0)]
    public string Url { get; } = url;

    [Key(1)]
    public DynamicResourceKeyBase DisplayNameKey { get; } = displayNameKey;

    /// <summary>
    /// The index of this URL in the original list, if applicable.
    /// Useful to let the LLM refer to the origin of the answer.
    /// </summary>
    [Key(2)]
    public int Index { get; set; }
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginUrlsDisplayBlock(params IReadOnlyList<ChatPluginUrl> urls) : ChatPluginDisplayBlock
{
    [Key(0)]
    public IReadOnlyList<ChatPluginUrl> Urls { get; } = urls;
}