using System.Buffers;
using ZLinq;

namespace Everywhere.Utilities;

using System.IO;
using System.Text;

/// <summary>
/// Provides functionality to detect the encoding of a text stream.
/// It prioritizes BOM detection and then uses heuristics and statistical analysis
/// to guess the encoding for non-BOM files, with support for UTF-8, GBK, and Big5.
/// </summary>
public static class EncodingDetector
{
    // Pre-created encoding instances for performance.
    // Using a provider is necessary for .NET Core/.NET 5+ to support legacy encodings.
    private static readonly Encoding Gbk;
    private static readonly Encoding Big5;

    static EncodingDetector()
    {
        // Register the CodePagesEncodingProvider to get access to GBK, Big5, etc.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Gbk = Encoding.GetEncoding("GBK");
        Big5 = Encoding.GetEncoding("Big5");
    }

    /// <summary>
    /// Tries to detect the encoding of a stream by reading its initial bytes.
    /// Returns null if the stream is likely binary or the encoding cannot be determined.
    /// </summary>
    /// <param name="stream">The stream to analyze. Must be readable and seekable.</param>
    /// <param name="bufferSize">The number of bytes to read for detection. Default is 4096.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The detected Encoding, or null if it's binary or undetermined.</returns>
    public static async Task<Encoding?> DetectEncodingAsync(Stream stream, int bufferSize = 4096, CancellationToken cancellationToken = default)
    {
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(stream));

        var originalPosition = stream.Position;
        stream.Seek(0, SeekOrigin.Begin);

        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        try
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken);

            if (bytesRead == 0)
            {
                // An empty file can be treated as UTF-8 by default.
                return new UTF8Encoding(false);
            }

            var span = buffer.AsSpan(0, bytesRead);

            // 1. Check for Byte Order Marks (BOM) - The most reliable method.
            var bomEncoding = DetectBom(span);
            if (bomEncoding != null)
            {
                return bomEncoding;
            }

            // 2. Heuristic: Check for null bytes to detect binary files.
            // Text files (other than UTF-16/32, which BOM would have caught) rarely contain null bytes.
            if (IsLikelyBinary(span))
            {
                return null;
            }

            // 3. Heuristic analysis for non-BOM encodings (UTF-8, GBK, Big5).
            return DetectEncodingWithoutBom(span);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            stream.Seek(originalPosition, SeekOrigin.Begin);
        }
    }

    private static Encoding? DetectBom(ReadOnlySpan<byte> buffer)
    {
        // Byte Order Marks (BOMs) for various encodings.
        Span<byte> utf8Bom = [0xEF, 0xBB, 0xBF];
        if (buffer.StartsWith(utf8Bom)) return Encoding.UTF8;
        Span<byte> utf16LeBom = [0xFF, 0xFE];
        if (buffer.StartsWith(utf16LeBom)) return Encoding.Unicode;
        Span<byte> utf16BeBom = [0xFE, 0xFF];
        if (buffer.StartsWith(utf16BeBom)) return Encoding.BigEndianUnicode;
        Span<byte> utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];
        if (buffer.StartsWith(utf32LeBom)) return Encoding.UTF32;
        Span<byte> utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];
        if (buffer.StartsWith(utf32BeBom)) return new UTF32Encoding(true, true);
        return null;
    }

    private static bool IsLikelyBinary(ReadOnlySpan<byte> buffer)
    {
        const double nullByteThreshold = 0.1; // 10% threshold for null bytes.
        var nullCount = buffer.AsValueEnumerable().Count(b => b == 0x00);
        return (double)nullCount / buffer.Length > nullByteThreshold;
    }

    private static Encoding? DetectEncodingWithoutBom(ReadOnlySpan<byte> buffer)
    {
        // Confidence scores for each encoding.
        // A score of 0 means the encoding is considered invalid for the given buffer.
        var utf8Confidence = 1;
        var gbkConfidence = 1;
        var big5Confidence = 1;

        // Minimum confidence score required to make a decision.
        const int minConfidence = 10;

        var i = 0;
        while (i < buffer.Length)
        {
            var b1 = buffer[i];

            // Single-byte ASCII characters are valid in all these encodings.
            if (b1 < 0x80)
            {
                i++;
                continue;
            }

            // --- UTF-8 Validation ---
            if (utf8Confidence > 0)
            {
                if ((b1 & 0xE0) == 0xC0) // 2-byte sequence
                {
                    if (i + 1 < buffer.Length && (buffer[i + 1] & 0xC0) == 0x80)
                    {
                        utf8Confidence++;
                    }
                    else utf8Confidence = 0;
                }
                else if ((b1 & 0xF0) == 0xE0) // 3-byte sequence
                {
                    if (i + 2 < buffer.Length && (buffer[i + 1] & 0xC0) == 0x80 && (buffer[i + 2] & 0xC0) == 0x80)
                    {
                        utf8Confidence++;
                    }
                    else utf8Confidence = 0;
                }
                else if ((b1 & 0xF8) == 0xF0) // 4-byte sequence
                {
                    if (i + 3 < buffer.Length && (buffer[i + 1] & 0xC0) == 0x80 && (buffer[i + 2] & 0xC0) == 0x80 && (buffer[i + 3] & 0xC0) == 0x80)
                    {
                        utf8Confidence++;
                    }
                    else utf8Confidence = 0;
                }
                else
                {
                    // Invalid start of a multi-byte sequence.
                    utf8Confidence = 0;
                }
            }

            // --- Multi-byte (GBK, Big5) Validation ---
            if (i + 1 < buffer.Length)
            {
                var b2 = buffer[i + 1];

                // GBK: 0x81-FE, 0x40-FE (excluding 0x7F)
                if (gbkConfidence > 0)
                {
                    if (b1 is >= 0x81 and <= 0xFE && b2 is >= 0x40 and <= 0xFE && b2 != 0x7F)
                    {
                        gbkConfidence++;
                    }
                    else
                    {
                        // This is not a valid GBK 2-byte sequence start, but it might be a single byte.
                        // A more robust check would require a state machine, but for now, we don't invalidate.
                        // We only increment confidence on positive matches.
                    }
                }

                // Big5: 0x81-FE, 0x40-7E or 0xA1-FE
                if (big5Confidence > 0)
                {
                    if (b1 >= 0x81 && b1 <= 0xFE && ((b2 >= 0x40 && b2 <= 0x7E) || (b2 >= 0xA1 && b2 <= 0xFE)))
                    {
                        big5Confidence++;
                    }
                }
                i += 2; // Move past the 2-byte character.
            }
            else
            {
                // Incomplete multi-byte character at the end of the buffer.
                i++;
            }
        }

        // --- Decision Logic ---
        // If UTF-8 is valid and has some multi-byte chars, it's a strong candidate.
        if (utf8Confidence > gbkConfidence && utf8Confidence > big5Confidence && utf8Confidence > minConfidence)
        {
            return new UTF8Encoding(false);
        }

        // If GBK is the clear winner.
        if (gbkConfidence > utf8Confidence && gbkConfidence > big5Confidence && gbkConfidence > minConfidence)
        {
            return Gbk;
        }

        // If Big5 is the clear winner.
        if (big5Confidence > utf8Confidence && big5Confidence > gbkConfidence && big5Confidence > minConfidence)
        {
            return Big5;
        }

        // If UTF-8 is valid but had low confidence (e.g., mostly ASCII), it's still the best default guess.
        if (utf8Confidence > 0 && gbkConfidence <= 1 && big5Confidence <= 1)
        {
            return new UTF8Encoding(false);
        }

        // If no encoding stands out, we cannot determine the encoding reliably.
        return null;
    }
}