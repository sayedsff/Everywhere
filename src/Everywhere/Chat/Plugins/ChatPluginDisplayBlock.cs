using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Common;
using Everywhere.Interop;
using LiveMarkdown.Avalonia;
using MessagePack;
using Microsoft.SemanticKernel;
using ZLinq;

namespace Everywhere.Chat.Plugins;

/// <summary>
/// Used to represent a block of content displayed by a chat plugin.
/// </summary>
[MessagePackObject]
[Union(0, typeof(ChatPluginTextDisplayBlock))]
[Union(1, typeof(ChatPluginDynamicResourceKeyDisplayBlock))]
[Union(2, typeof(ChatPluginMarkdownDisplayBlock))]
[Union(3, typeof(ChatPluginProgressDisplayBlock))]
[Union(4, typeof(ChatPluginFileReferencesDisplayBlock))]
[Union(5, typeof(ChatPluginFileDifferenceDisplayBlock))]
[Union(6, typeof(ChatPluginFunctionContentDisplayBlock))]
public abstract partial class ChatPluginDisplayBlock : ObservableObject
{
    /// <summary>
    /// Indicates whether this display block is waiting for user input.
    /// </summary>
    [IgnoreMember]
    public virtual bool IsWaitingForUserInput => false;
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
/// <param name="FullPath"></param>
/// <param name="DisplayNameKey"></param>
/// <param name="IsFolder"></param>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial record ChatPluginFileReference(
    [property: Key(0)] string FullPath,
    [property: Key(1)] DynamicResourceKeyBase? DisplayNameKey = null,
    [property: Key(2)] bool IsFolder = false
)
{
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
/// Represents the content of a function (call or result) with optional display content.
/// </summary>
/// <param name="id"></param>
/// <param name="content"></param>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public sealed partial class ChatPluginFunctionContentDisplayBlock(string id, ChatPluginDisplayBlock? content) : ChatPluginDisplayBlock
{
    /// <summary>
    /// The unique identifier for the function call.
    /// </summary>
    [Key(0)]
    public string Id { get; } = id;

    [Key(1)]
    public ChatPluginDisplayBlock? Content { get; } = content;
}