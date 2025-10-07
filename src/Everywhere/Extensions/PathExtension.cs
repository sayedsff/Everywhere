using System.Text.RegularExpressions;

namespace Everywhere.Extensions;

public static partial class PathExtension
{
#if WINDOWS
    /// <summary>
    /// Regex to match Windows absolute paths (captures filename at the end)
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"[A-Za-z]:\\(?:[^\\\r\n]+\\)*([^\\\r\n]+)", RegexOptions.Compiled)]
    private static partial Regex PathRegex();

    /// <summary>
    /// Regex to match common Windows user folder patterns (captures username)
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"([A-Za-z]:\\Users\\)([^\\\/]+)(\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UserFolderRegex();
#else
    /// <summary>
    /// Regex to match Unix absolute paths (captures filename)
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\/(?:[^\/\r\n]+\/)*([^\/\r\n]+)", RegexOptions.Compiled)]
    private static partial Regex PathRegex();

    /// <summary>
    /// Regex to match common Unix user folder patterns (captures username)
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"(\/home\/)([^\/]+)(\/)", RegexOptions.Compiled)]
    private static partial Regex UserFolderRegex();
#endif

    /// <summary>
    /// Sanitize a file path by removing sensitive information such as username or absolute directories.
    /// Keeps final file name for debugging but replaces directory prefixes with [redacted_path] or user folder replaced.
    /// </summary>
    public static string? SanitizePath(this string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        try
        {
            // Normalize slashes for consistent regex matching
            var normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            // First try to replace user profile prefix if available
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                var normalizedProfile = userProfile.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                if (normalized.StartsWith(normalizedProfile, comparison))
                {
                    var tail = normalized[normalizedProfile.Length..].TrimStart(Path.DirectorySeparatorChar);
                    var fileName = Path.GetFileName(tail);
                    return string.IsNullOrEmpty(fileName) ?
                        $"[redacted_user_profile]" :
                        $"[redacted_user_profile]{Path.DirectorySeparatorChar}{fileName}";
                }
            }

            // If user profile not matched, try regex methods on the normalized path
            var m = PathRegex().Match(normalized);
            if (m.Success)
            {
                var file = m.Groups[1].Value;
                return $"[redacted_path]{Path.DirectorySeparatorChar}{file}";
            }

            // fallback: replace common user folder pattern
            var replaced = UserFolderRegex().Replace(normalized, "$1[redacted]$3");
            if (replaced != normalized) return replaced;


            // Last resort: if path contains a path separator, return last segment
            if (normalized.Contains(Path.DirectorySeparatorChar))
            {
                var fileName = Path.GetFileName(normalized);
                return string.IsNullOrEmpty(fileName) ? "[redacted_path]" : $"[redacted_path]{Path.DirectorySeparatorChar}{fileName}";
            }

            // nothing to do
            return path;
        }
        catch
        {
            // Fail safe: return a redacted placeholder so we do not leak anything on error
            return "[redacted_path]";
        }
    }

    /// <summary>
    /// Scrub any path-like substrings inside a larger string (e.g., breadcrumb messages).
    /// It will replace recognized absolute paths with a redacted short form.
    /// </summary>
    public static string SanitizeStringForPaths(this string? input, int maxLength = 1000)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        try
        {
            var sanitizedInput = input;
            // replace user folders first
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
#if WINDOWS
                const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
#else
                const StringComparison comparison = StringComparison.Ordinal;
#endif
                sanitizedInput = sanitizedInput.Replace(userProfile, "[redacted_user_profile]", comparison);
            }

            // Normalize slashes for consistent regex matching before replacing path patterns
            var normalizedForRegex = sanitizedInput.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

            // Replace path patterns, keep filename only
            var finalInput = PathRegex().Replace(normalizedForRegex, m => $"[redacted_path]{Path.DirectorySeparatorChar}{m.Groups[1].Value}");

            if (finalInput.Length > maxLength) finalInput = finalInput[..maxLength];
            return finalInput;
        }
        catch
        {
            return "[redacted]";
        }
    }
}