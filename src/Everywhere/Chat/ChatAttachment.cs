using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Common;
using Everywhere.Interop;
using Everywhere.Storage;
using Everywhere.Utilities;
using Lucide.Avalonia;
using MessagePack;
using Serilog;

namespace Everywhere.Chat;

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
[Union(0, typeof(ChatVisualElementAttachment))]
[Union(1, typeof(ChatTextAttachment))]
[Union(3, typeof(ChatFileAttachment))]
public abstract partial class ChatAttachment(DynamicResourceKeyBase headerKey) : ObservableObject
{
    public abstract LucideIconKind Icon { get; }

    [Key(0)]
    public DynamicResourceKeyBase HeaderKey => headerKey;
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatVisualElementAttachment : ChatAttachment
{
    [Key(1)]
    public override LucideIconKind Icon { get; }

    /// <summary>
    /// Ignore this property during serialization because it should already be converted into prompts and shouldn't appear in history.
    /// </summary>
    [IgnoreMember]
    public ResilientReference<IVisualElement>? Element { get; }

    /// <summary>
    /// Indicates whether the visual element is currently focused.
    /// This will change with focus and display with primary color in the UI.
    /// </summary>
    [IgnoreMember]
    public bool IsFocusedElement { get; set; }

    [SerializationConstructor]
    private ChatVisualElementAttachment(DynamicResourceKeyBase headerKey, LucideIconKind icon) : base(headerKey)
    {
        Icon = icon;
    }

    public ChatVisualElementAttachment(DynamicResourceKeyBase headerKey, LucideIconKind icon, IVisualElement element) : base(headerKey)
    {
        Icon = icon;
        Element = new ResilientReference<IVisualElement>(element);
    }
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatTextAttachment(DynamicResourceKeyBase headerKey, string text) : ChatAttachment(headerKey)
{
    public override LucideIconKind Icon => LucideIconKind.TextInitial;

    [Key(1)]
    public string Text => text;
}

/// <summary>
/// Represents a file attachment in a chat message.
/// Supports image, video, audio, document, and plain file types.
/// </summary>
/// <param name="headerKey"></param>
/// <param name="filePath"></param>
/// <param name="sha256"></param>
/// <param name="mimeType"></param>
[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatFileAttachment(
    DynamicResourceKeyBase headerKey,
    string filePath,
    string sha256,
    string mimeType
) : ChatAttachment(headerKey)
{
    public override LucideIconKind Icon => LucideIconKind.File;

    [Key(1)]
    public string FilePath { get; set; } = filePath;

    [Key(2)]
    public string Sha256 { get; } = sha256;

    [Key(3)]
    public string MimeType { get; } = MimeTypeUtilities.VerifyMimeType(mimeType);

    public bool IsImage => MimeTypeUtilities.IsImage(MimeType);

    public Bitmap? Image
    {
        get
        {
            if (field is not null) return field;

            Task.Run(() => GetImageAsync(1024, 1024)).ContinueWith(task =>
            {
                if (task is { IsCompletedSuccessfully: true, Result: not null })
                {
                    field = task.Result;
                }
                else
                {
                    field = null;
                }

                // Notify property changed on the UI thread
                OnPropertyChanged();
            });

            return field;
        }
    }

    public async Task<Bitmap?> GetImageAsync(int maxWidth = 2560, int maxHeight = 2560)
    {
        if (!IsImage) return null;

        try
        {
            if (!File.Exists(FilePath)) return null;
            await using var stream = File.OpenRead(FilePath);
            var bitmap = Bitmap.DecodeToWidth(stream, maxWidth);
            return await ResizeImageOnDemandAsync(bitmap, maxWidth, maxHeight);
        }
        catch (Exception ex)
        {
            ex = HandledSystemException.Handle(ex);
            Log.Logger.ForContext<ChatFileAttachment>().Error(ex, "Failed to load image from file: {FilePath}", FilePath);
            return null;
        }
    }

    public override string ToString()
    {
        return $"{MimeType}: {Path.GetFileName(FilePath)}";
    }

    /// <summary>
    /// Creates a new ChatFileAttachment from a file path.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="mimeType">null for auto-detection</param>
    /// <param name="maxBytesSize"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the file does not exist.
    /// </exception>
    /// <exception cref="OverflowException">
    /// Thrown if the file size exceeds the maximum allowed size.
    /// </exception>
    public static Task<ChatFileAttachment> CreateAsync(
        string filePath,
        string? mimeType = null,
        long maxBytesSize = 25L * 1024 * 1024) => Task.Run(async () =>
    {
        await using var stream = File.OpenRead(filePath);
        if (stream.Length > maxBytesSize)
        {
            throw new NotSupportedException($"File size exceeds the maximum allowed size of {maxBytesSize} bytes.");
        }

        mimeType = await MimeTypeUtilities.EnsureMimeTypeAsync(mimeType, filePath);

        var sha256 = await SHA256.HashDataAsync(stream);
        var sha256String = Convert.ToHexString(sha256).ToLowerInvariant();
        return new ChatFileAttachment(new DirectResourceKey(Path.GetFileName(filePath)), filePath, sha256String, mimeType);
    });

    private async static ValueTask<Bitmap> ResizeImageOnDemandAsync(Bitmap image, int maxWidth = 2560, int maxHeight = 2560)
    {
        if (image.PixelSize.Width <= maxWidth && image.PixelSize.Height <= maxHeight)
        {
            return image;
        }

        var scale = Math.Min(maxWidth / (double)image.PixelSize.Width, maxHeight / (double)image.PixelSize.Height);
        var newWidth = (int)(image.PixelSize.Width * scale);
        var newHeight = (int)(image.PixelSize.Height * scale);

        return await Task.Run(() => image.CreateScaledBitmap(new PixelSize(newWidth, newHeight)));
    }
}