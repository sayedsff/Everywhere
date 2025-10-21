using System.Buffers;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ZLinq;

namespace Everywhere.Chat.Plugins;

public class FileSystemPlugin : BuiltInChatPlugin
{
    public override DynamicResourceKeyBase HeaderKey { get; } = new DynamicResourceKey(LocaleKey.NativeChatPlugin_FileSystem_Header);
    public override DynamicResourceKeyBase DescriptionKey { get; } = new DynamicResourceKey(LocaleKey.NativeChatPlugin_FileSystem_Description);
    public override LucideIconKind? Icon => LucideIconKind.FileBox;

    private TimeSpan RegexTimeout => TimeSpan.FromSeconds(3);

    private readonly ILogger<FileSystemPlugin> _logger;

    public FileSystemPlugin(ILogger<FileSystemPlugin> logger) : base("file_system")
    {
        _logger = logger;

        _functions.Add(
            new NativeChatFunction(
                SearchFiles,
                ChatFunctionPermissions.FileRead));
        _functions.Add(
            new NativeChatFunction(
                GetFileInformation,
                ChatFunctionPermissions.FileRead));
        _functions.Add(
            new NativeChatFunction(
                SearchFileContentAsync,
                ChatFunctionPermissions.FileRead));
        _functions.Add(
            new NativeChatFunction(
                ReadTextFileAsync,
                ChatFunctionPermissions.FileRead));
        _functions.Add(
            new NativeChatFunction(
                MoveFile,
                ChatFunctionPermissions.FileAccess));
        _functions.Add(
            new NativeChatFunction(
                DeleteFilesAsync,
                ChatFunctionPermissions.FileAccess));
        _functions.Add(
            new NativeChatFunction(
                CreateDirectory,
                ChatFunctionPermissions.FileAccess));
        _functions.Add(
            new NativeChatFunction(
                WriteFileContentAsync,
                ChatFunctionPermissions.FileAccess));
        _functions.Add(
            new NativeChatFunction(
                ReplaceFileContentAsync,
                ChatFunctionPermissions.FileAccess));
    }

    [KernelFunction("search_files")]
    [Description("Search for files and directories in a specified path matching the given search pattern.")]
    private string SearchFiles(
        string path,
        [Description("Regex search pattern to match file and directory names.")] string pattern = ".*",
        int skip = 0,
        [Description("Maximum number of results to return. Max is 1000.")] int maxCount = 100,
        FilesOrderBy orderBy = FilesOrderBy.Default)
    {
        if (skip < 0) skip = 0;
        if (maxCount < 0) maxCount = 0;
        if (maxCount > 1000) maxCount = 1000;

        _logger.LogDebug(
            "Searching files in path: {Path} with pattern: {SearchPattern}, skip: {Skip}, maxCount: {MaxCount}, orderBy: {OrderBy}",
            path,
            pattern,
            skip,
            maxCount,
            orderBy);

        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);
        ExpandFullPath(ref path);
        var query = EnsureDirectoryInfo(path)
            .EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
            .Where(info => regex.IsMatch(info.Name))
            .Select(info => new FileRecord(
                info.Name,
                info is FileInfo file ? file.Length : -1,
                info.CreationTime,
                info.LastWriteTime,
                info.Attributes));

        query = orderBy switch
        {
            FilesOrderBy.Name => query.OrderBy(item => item.FullPath),
            FilesOrderBy.Size => query.OrderBy(item => item.BytesSize),
            FilesOrderBy.Created => query.OrderBy(item => item.Created),
            FilesOrderBy.LastModified => query.OrderBy(item => item.Modified),
            _ => query
        };

        return new FileRecords(query.Skip(skip).Take(maxCount)).ToString();
    }

    [KernelFunction("get_file_info")]
    [Description("Get information about a file or directory at the specified path.")]
    private string GetFileInformation(string path)
    {
        _logger.LogDebug("Getting file information for path: {Path}", path);

        ExpandFullPath(ref path);
        var info = EnsureFileSystemInfo(path);
        var sb = new StringBuilder();
        return sb.AppendLine(FileRecord.Header).Append(
            new FileRecord(
                info.Name,
                info is FileInfo file ? file.Length : -1,
                info.CreationTime,
                info.LastWriteTime,
                info.Attributes)).ToString();
    }

    [KernelFunction("search_file_content")]
    [Description("Searches for a specific text pattern within file(s) and returns matching lines.")]
    private async Task<string> SearchFileContentAsync(
        [Description("File or directory path to search.")] string path,
        [Description("Regex pattern to search for within the file.")] string pattern,
        bool ignoreCase = true,
        [Description("Regex pattern to include files to search. Effective when path is a folder.")]
        string filePattern = ".*",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Searching file content in path: {Path} with pattern: {SearchPattern}, ignoreCase: {IgnoreCase}, filePattern: {FilePattern}",
            path,
            pattern,
            ignoreCase,
            filePattern);

        var regexOptions = RegexOptions.Compiled | RegexOptions.Multiline;
        if (ignoreCase)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        var searchRegex = new Regex(pattern, regexOptions, RegexTimeout);
        var fileRegex = new Regex(filePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        ExpandFullPath(ref path);
        var fileSystemInfo = EnsureFileSystemInfo(path);

        var resultLines = new List<string>(capacity: 256);
        const int maxCollectedLines = 2000;
        const long maxSearchFileSize = 10 * 1024 * 1024; // 10 MB

        IEnumerable<FileInfo> filesToSearch = fileSystemInfo switch
        {
            FileInfo fileInfo when fileRegex.IsMatch(fileInfo.Name) => [fileInfo],
            DirectoryInfo directoryInfo => directoryInfo
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .AsValueEnumerable()
                .Where(file => fileRegex.IsMatch(file.Name))
                .ToList(),
            _ => []
        };

        foreach (var file in filesToSearch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // skip big or binary files
            if (file.Length > maxSearchFileSize) continue;

            await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (await IsBinaryFileAsync(stream))
            {
                continue;
            }

            stream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var lineNumber = 0;

            while (resultLines.Count < maxCollectedLines && await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                lineNumber++;

                if (!searchRegex.IsMatch(line)) continue;

                // format: full path:line: content
                resultLines.Add($"----\n{file.FullName}\n{lineNumber}\t{line}");

                if (resultLines.Count >= maxCollectedLines)
                {
                    break; // stop early, enough lines collected
                }
            }
        }

        if (resultLines.Count == 0)
        {
            return "No matching lines found.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Found {resultLines.Count} matching line(s).");
        foreach (var resultLine in resultLines) sb.AppendLine(resultLine);
        return sb.ToString();
    }

    [KernelFunction("read_file")]
    [Description(
        "Reads lines from a text file at the specified path. Supports reading from a specific line and limiting the number of lines." +
        "Binary files will read as hex string, 32 bytes per line.")]
    private async Task<string> ReadTextFileAsync(
        string path,
        long startBytes = 0L,
        long maxReadBytes = 10240L,
        [Description("Text encoding name for `System.Text.Encoding.GetEncoding`")] string encoding = "utf-8",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Reading text file at path: {Path}, startBytes: {StartBytes}, maxReadBytes: {MaxReadBytes}, encoding: {Encoding}",
            path,
            startBytes,
            maxReadBytes,
            encoding);

        var enc = Encoding.GetEncoding(encoding);

        ExpandFullPath(ref path);
        var fileInfo = EnsureFileInfo(path);
        if (fileInfo.Length > 10 * 1024 * 1024)
        {
            throw new NotSupportedException("File size is larger than 10 MB, read operation is not supported.");
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fileInfo.Length == 0)
        {
            return new FileContent(string.Empty, false, 0, 0).ToString();
        }

        var stringBuilder = new StringBuilder();
        if (await IsBinaryFileAsync(stream))
        {
            stream.Seek(startBytes, SeekOrigin.Begin);

            // Read binary file as hex string, 32 bytes per line
            var buffer = new byte[32];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                var hexString = BitConverter.ToString(buffer, 0, bytesRead);
                stringBuilder.AppendLine(hexString);

                if (stringBuilder.Length >= maxReadBytes)
                {
                    break;
                }
            }

            return new FileContent(stringBuilder.ToString(), true, stream.Position, fileInfo.Length - stream.Position).ToString();
        }

        stream.Seek(startBytes, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, enc);
        var readBuffer = ArrayPool<char>.Shared.Rent(1024);
        try
        {
            int charsRead;
            long totalBytesRead = 0;
            while ((charsRead = await reader.ReadAsync(readBuffer.AsMemory(), cancellationToken)) > 0)
            {
                var bytesRead = enc.GetByteCount(readBuffer, 0, charsRead);
                if (totalBytesRead + bytesRead > maxReadBytes)
                {
                    var allowedChars = enc.GetMaxCharCount((int)(maxReadBytes - totalBytesRead));
                    stringBuilder.Append(readBuffer, 0, allowedChars);
                    break;
                }

                stringBuilder.Append(readBuffer, 0, charsRead);
                totalBytesRead += bytesRead;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(readBuffer);
        }

        return new FileContent(stringBuilder.ToString(), false, stream.Position, fileInfo.Length - stream.Position).ToString();
    }

    [KernelFunction("move_file")]
    [Description("Moves or renames a file or directory.")]
    private bool MoveFile(
        [Description("Source file or directory path.")] string source,
        [Description("Destination file or directory path. Type must match the source.")] string destination)
    {
        _logger.LogDebug("Moving file from {Source} to {Destination}", source, destination);

        ExpandFullPath(ref source);
        ExpandFullPath(ref destination);

        var isFile = File.Exists(source);
        if (!isFile && !Directory.Exists(source))
        {
            throw new FileNotFoundException($"{nameof(source)} does not exist.");
        }

        var destinationDirectory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new DirectoryNotFoundException($"{destination} directory is invalid.");
        }

        try
        {
            Directory.CreateDirectory(destinationDirectory);
        }
        catch (Exception ex)
        {
            throw new IOException("Failed to create destination directory.", ex);
        }

        if (isFile)
        {
            File.Move(source, destination, overwrite: false);
        }
        else
        {
            Directory.Move(source, destination);
        }

        return true;
    }

    [KernelFunction("delete_files")]
    [Description("Delete files and directories at the specified path matching the given pattern. The maximum number of deletions is limited to 10,000.")]
    private async Task<string> DeleteFilesAsync(
        [FromKernelServices] IChatPluginUserInterface userInterface,
        [Description("File or directory path to delete.")] string path,
        [Description(
            "Regex search pattern to match file and directory names. Effective when path is a folder. Warn that this will delete all matching files and directories recursively.")]
        string pattern = ".*",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting file at {Path}", path);

        ExpandFullPath(ref path);

        if (Path.GetDirectoryName(path) is null)
        {
            throw new UnauthorizedAccessException("Cannot delete root directory.");
        }

        var fileSystemInfo = EnsureFileSystemInfo(path);
        if (fileSystemInfo.Attributes.HasFlag(FileAttributes.System))
        {
            throw new UnauthorizedAccessException("Cannot delete system files or directories.");
        }

        List<FileInfo> filesToDelete;
        List<DirectoryInfo> directoriesToDelete = [];
        switch (fileSystemInfo)
        {
            case FileInfo fileInfo:
            {
                filesToDelete = [fileInfo];
                break;
            }
            case DirectoryInfo directoryInfo:
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

                // directories matching pattern (will be deleted recursively)
                directoriesToDelete = directoryInfo
                    .EnumerateDirectories("*", SearchOption.AllDirectories)
                    .AsValueEnumerable()
                    .Where(d => regex.IsMatch(d.Name))
                    .OrderByDescending(d => d.FullName.Length) // deeper first
                    .ToList();

                // files matching pattern that are NOT inside directoriesToDelete (to avoid double work)
                var deleteDirPrefixes = directoriesToDelete
                    .Select(d => Path.GetFullPath(d.FullName.TrimEnd(Path.DirectorySeparatorChar)) + Path.DirectorySeparatorChar)
                    .ToArray();

                filesToDelete = directoryInfo
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .AsValueEnumerable()
                    .Take(10000) // limit to 10k files
                    .Where(f => regex.IsMatch(f.Name) &&
                        !deleteDirPrefixes.Any(p => f.FullName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                break;
            }
            default:
            {
                return "No files or directories to delete.";
            }
        }

        var fileNames = string.Join(Environment.NewLine, filesToDelete.Select(f => f.Name).Take(10));
        var consent = await userInterface.RequestConsentAsync(
            "deletion",
            new FormattedDynamicResourceKey(
                LocaleKey.NativeChatPlugin_FileSystem_DeleteFiles_DeletionConsent_Header,
                new DirectResourceKey(filesToDelete.Count)),
            fileNames,
            cancellationToken);
        if (!consent)
        {
            return $"{filesToDelete.Count} files/directories deletion was cancelled by user.";
        }

        var successCount = 0;
        var errorCount = 0;
        foreach (var info in filesToDelete)
        {
            if (info.Attributes.HasFlag(FileAttributes.System))
            {
                consent = await userInterface.RequestConsentAsync(
                    "system",
                    new DynamicResourceKey(LocaleKey.NativeChatPlugin_FileSystem_DeleteFiles_SystemFile_DeletionConsent_Header),
                    info.FullName,
                    cancellationToken);
                if (!consent)
                {
                    continue;
                }
            }

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return $"User cancelled the deletion operation. " +
                        $"{successCount} files/directories were deleted successfully, {errorCount} errors occurred.";
                }

                if (info.Exists) info.Delete();
                successCount++;
            }
            catch
            {
                errorCount++;
            }
        }

        return errorCount == 0 ?
            $"{filesToDelete.Count} files/directories were deleted successfully." :
            $"{successCount} files/directories were deleted successfully, {errorCount} errors occurred.";
    }

    [KernelFunction("create_directory")]
    [Description("Creates a new directory at the specified path.")]
    private void CreateDirectory(string path)
    {
        _logger.LogDebug("Creating directory at {Path}", path);

        ExpandFullPath(ref path);
        Directory.CreateDirectory(path);
    }

    [KernelFunction("write_file_content")]
    [Description("Writes content to a text file at the specified path. Binary files are not supported.")]
    private async Task WriteFileContentAsync(
        string path,
        string? content,
        [Description("Text encoding name for `System.Text.Encoding.GetEncoding`")] string encoding = "utf-8",
        bool append = false)
    {
        _logger.LogDebug("Writing text file at {Path}, encoding: {Encoding}, append: {Append}", path, encoding, append);

        ExpandFullPath(ref path);
        await using var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        if (await IsBinaryFileAsync(stream))
        {
            throw new InvalidOperationException("Cannot write to a binary file.");
        }

        await using var writer = new StreamWriter(stream, Encoding.GetEncoding(encoding));
        await writer.WriteAsync(content);
        await writer.FlushAsync();
    }

    [KernelFunction("replace_file_content")]
    [Description("Replaces content in a single text file at the specified path with regex. Binary files are not supported.")]
    private async Task<string> ReplaceFileContentAsync(
        [FromKernelServices] IChatPluginUserInterface userInterface,
        string path,
        [Description("Regex patterns to search for within the file.")] IReadOnlyList<string> patterns,
        [Description("Replacement strings that march patterns.")] IReadOnlyList<string> replacements,
        bool ignoreCase = false,
        [Description("Text encoding name for `System.Text.Encoding.GetEncoding`")] string encoding = "utf-8",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Replacing file content at {Path} with patterns: {Patterns}, replacements: {Replacements}, ignoreCase: {IgnoreCase}, encoding: {Encoding}",
            path,
            patterns,
            replacements,
            ignoreCase,
            encoding);

        if (patterns.Count == 0)
        {
            throw new ArgumentException("At least one pattern must be provided.", nameof(patterns));
        }

        if (replacements.Count != patterns.Count)
        {
            throw new ArgumentException("Replacements count must match patterns count.", nameof(replacements));
        }

        ExpandFullPath(ref path);
        var fileInfo = EnsureFileInfo(path);
        if (fileInfo.Length > 10 * 1024 * 1024)
        {
            throw new NotSupportedException("File size is larger than 10 MB, replace operation is not supported.");
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        if (await IsBinaryFileAsync(stream))
        {
            throw new InvalidOperationException("Cannot replace content in a binary file.");
        }

        stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.GetEncoding(encoding));
        var fileContent = await reader.ReadToEndAsync(cancellationToken);

        var regexOptions = RegexOptions.Compiled | RegexOptions.Multiline;
        if (ignoreCase)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        var replacedContent = fileContent;
        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            var replacement = i < replacements.Count ? replacements[i] : string.Empty;
            var regex = new Regex(pattern, regexOptions);
            replacedContent = regex.Replace(replacedContent, replacement);
        }

        var difference = new TextDifference(path);
        TextDifferenceBuilder.BuildLineDiff(difference, fileContent, replacedContent);

        userInterface.RequestDisplaySink().AppendFileDifference(difference, fileContent);
        await difference.WaitForAcceptanceAsync(cancellationToken);

        // Apply all accepted changes
        if (!difference.Changes.Any(t => t.Accepted is true))
        {
            return "All changes were rejected by user.";
        }

        replacedContent = difference.Apply(fileContent);
        stream.SetLength(0);
        stream.Seek(0, SeekOrigin.Begin);
        await using var writer = new StreamWriter(stream, Encoding.GetEncoding(encoding));
        await writer.WriteAsync(replacedContent);
        await writer.FlushAsync(cancellationToken);

        return difference.ToModelSummary(fileContent, default);
    }

    private static void ExpandFullPath(ref string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        path = Environment.ExpandEnvironmentVariables(path);
        path = Path.GetFullPath(path);
    }

    /// <summary>
    /// Ensures the specified path is a valid directory and returns its DirectoryInfo.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="DirectoryNotFoundException"></exception>
    private static DirectoryInfo EnsureDirectoryInfo(string path)
    {
        var directoryInfo = new DirectoryInfo(path);
        if (!directoryInfo.Exists)
        {
            if (File.Exists(path))
            {
                throw new InvalidOperationException("The specified path is a file, not a directory.");
            }

            throw new DirectoryNotFoundException("The specified path is not a directory or a file.");
        }

        return directoryInfo;
    }

    /// <summary>
    /// Ensures the specified path is a valid file and returns its FileInfo.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    private static FileInfo EnsureFileInfo(string path)
    {
        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            if (Directory.Exists(path))
            {
                throw new InvalidOperationException("The specified path is a directory, not a file.");
            }

            throw new FileNotFoundException("The specified path is not a file or a directory.");
        }

        return fileInfo;
    }

    private static FileSystemInfo EnsureFileSystemInfo(string path)
    {
        if (File.Exists(path))
        {
            return new FileInfo(path);
        }

        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path);
        }

        throw new FileNotFoundException("The specified path does not exist as a file or directory.");
    }

    private static async ValueTask<bool> IsBinaryFileAsync(Stream stream)
    {
        // Check if the file is binary by reading the first few bytes
        const int bufferSize = 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, bufferSize));
            return bytesRead > 0 && buffer.Take(bytesRead).Any(b => b == 0);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}