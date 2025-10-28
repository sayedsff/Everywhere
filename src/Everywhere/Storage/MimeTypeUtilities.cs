using ZLinq;

namespace Everywhere.Storage;

public static class MimeTypeUtilities
{
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
    /// Verifies that the given MIME type is supported. If not, throws NotSupportedException. Original MIME type is returned if supported.
    /// </summary>
    /// <param name="mimeType"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static string VerifyMimeType(string mimeType)
    {
        return !SupportedMimeTypes.Values.Contains(mimeType) ? throw new NotSupportedException($"Unsupported MIME type: {mimeType}") : mimeType;
    }

    /// <summary>
    /// Ensures that a valid MIME type is provided. If the input MIME type is null, it attempts to detect it from the file path.
    /// If detection fails, it throws NotSupportedException.
    /// </summary>
    /// <param name="mimeType"></param>
    /// <param name="filePath"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static async Task<string> EnsureMimeTypeAsync(string? mimeType, string filePath)
    {
        if (mimeType is not null) return VerifyMimeType(mimeType);

        mimeType = await DetectMimeTypeAsync(filePath);
        return mimeType ?? throw new NotSupportedException($"Could not detect MIME type for file: {filePath}");
    }

    public static bool IsImage(string mimeType)
    {
        return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsVideo(string mimeType)
    {
        return mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAudio(string mimeType)
    {
        return mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> GetExtensionsForMimeTypePrefix(string prefix)
    {
        return SupportedMimeTypes
            .Where(kv => kv.Value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key);
    }
}