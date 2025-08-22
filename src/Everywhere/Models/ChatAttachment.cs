using System.Security.Cryptography;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Lucide.Avalonia;
using MessagePack;
using ZLinq;

namespace Everywhere.Models;

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
    public IVisualElement? Element { get; }

    [SerializationConstructor]
    private ChatVisualElementAttachment(DynamicResourceKeyBase headerKey, LucideIconKind icon) : base(headerKey)
    {
        Icon = icon;
    }

    public ChatVisualElementAttachment(DynamicResourceKeyBase headerKey, LucideIconKind icon, IVisualElement element) : base(headerKey)
    {
        Icon = icon;
        Element = element;
    }
}

[MessagePackObject(AllowPrivate = true, OnlyIncludeKeyedMembers = true)]
public partial class ChatTextAttachment(DynamicResourceKeyBase headerKey, string text) : ChatAttachment(headerKey)
{
    public override LucideIconKind Icon => LucideIconKind.Text;

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
    public string FilePath => filePath;

    [Key(2)]
    public string Sha256 => sha256;

    [Key(3)]
    public string MimeType => mimeType;

    public bool IsImage => MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

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
            await using var stream = File.OpenRead(FilePath);
            Bitmap bitmap = WriteableBitmap.Decode(stream);
            return await ResizeImageOnDemandAsync(bitmap);
        }
        catch
        {
            return null;
        }
    }

    public static readonly IReadOnlyDictionary<string, string> SupportedMimeTypes = new Dictionary<string, string>
    {
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".mp4", "video/mp4" },
        { ".mov", "video/quicktime" },
        { ".avi", "video/x-msvideo" },
        { ".mkv", "video/x-matroska" },
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".flac", "audio/flac" },
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".txt", "text/plain" },
        { ".rtf", "application/rtf" },
        { ".json", "application/json" },
        { ".csv", "text/csv" },
        { ".md", "text/markdown" },
        { ".html", "text/html" },
        { ".xhtml", "application/xhtml+xml" },
        { ".xml", "application/xml" },
        { ".css", "text/css" },
        { ".js", "text/javascript" },
        { ".sh", "application/x-sh" },
    };

    public static async Task<string?> DetectMimeTypeAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (SupportedMimeTypes.TryGetValue(extension, out var mimeType))
        {
            return mimeType;
        }

        // detect mime type by reading file header
        var buffer = new byte[1024];
        await using var stream = File.OpenRead(filePath);
        var bytesRead = await stream.ReadAsync(buffer);
        var isBinary = buffer.AsValueEnumerable().Take(bytesRead).Any(b => b == 0); // check for null bytes, which indicate binary data
        return isBinary ? null : "text/plain"; // default to plain text if no specific type is detected
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

        if (mimeType is not null && !SupportedMimeTypes.Values.Contains(mimeType))
        {
            throw new NotSupportedException($"Unsupported MIME type: {mimeType}");
        }

        mimeType ??= await DetectMimeTypeAsync(filePath);
        if (mimeType is null)
        {
            throw new NotSupportedException($"Could not detect MIME type for file: {filePath}");
        }

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