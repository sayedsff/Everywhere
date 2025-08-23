using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Everywhere.Chat.Plugins;

public class FileSystemPlugin : BuiltInChatPlugin
{
    private enum ListFilesOrderBy
    {
        Name,
        Size,
        LastModified
    }

    [Serializable]
    private record ListFilesResult(IReadOnlyList<FileInformation> Items, int TotalCount);

    [Serializable]
    [DebuggerDisplay("{Name} - {Size} bytes, {LastModified} {Attributes}")]
    private record FileInformation(string Name, long Size, FileAttributes Attributes, DateTime LastModified);

    [Serializable]
    private record ReadTextFileResult(string Content, bool IsBinary, bool IsEndOfFile);


    public override LucideIconKind? Icon => LucideIconKind.FileBox;

    private readonly ILogger<FileSystemPlugin> _logger;

    public FileSystemPlugin(ILogger<FileSystemPlugin> logger) : base("FileSystem")
    {
        _logger = logger;

        _functions.Add(
            new AnonymousChatFunction(
                ListFiles,
                ChatFunctionPermissions.FileRead));
        _functions.Add(
            new AnonymousChatFunction(
                GetFileInformation,
                ChatFunctionPermissions.FileRead));
        _functions.Add(
            new AnonymousChatFunction(
                ReadTextFileAsync,
                ChatFunctionPermissions.FileRead));
        _functions.Add(
            new AnonymousChatFunction(
                MoveFile,
                ChatFunctionPermissions.FileAccess));
        _functions.Add(
            new AnonymousChatFunction(
                DeleteFile,
                ChatFunctionPermissions.FileAccess));
        _functions.Add(
            new AnonymousChatFunction(
                CreateFile,
                ChatFunctionPermissions.FileAccess));
        _functions.Add(
            new AnonymousChatFunction(
                CreateDirectory,
                ChatFunctionPermissions.FileAccess));
        _functions.Add(
            new AnonymousChatFunction(
                WriteTextFile,
                ChatFunctionPermissions.FileAccess));
    }

    [KernelFunction("list_files")]
    [Description("Lists files (including directories) in a specified path.")]
    private ListFilesResult ListFiles(string path, int skip = 0, int maxCount = 100, ListFilesOrderBy orderBy = ListFilesOrderBy.Name)
    {
        _logger.LogInformation(
            "Listing files in path: {Path}, skip: {Skip}, maxCount: {MaxCount}, orderBy: {OrderBy}",
            path,
            skip,
            maxCount,
            orderBy);

        var query = new DirectoryInfo(path)
            .EnumerateFileSystemInfos()
            .Select(item => new FileInformation(
                item.Name,
                item is FileInfo file ? file.Length : 0,
                item.Attributes,
                item.LastWriteTime));

        query = orderBy switch
        {
            ListFilesOrderBy.Name => query.OrderBy(item => item.Name),
            ListFilesOrderBy.Size => query.OrderBy(item => item.Size),
            ListFilesOrderBy.LastModified => query.OrderBy(item => item.LastModified),
            _ => throw new ArgumentOutOfRangeException(nameof(orderBy), orderBy, null)
        };

        // ReSharper disable PossibleMultipleEnumeration
        return new ListFilesResult(query.Skip(skip).Take(maxCount).ToReadOnlyList(), query.Count());
        // ReSharper restore PossibleMultipleEnumeration
    }

    [KernelFunction("get_file_info")]
    [Description("Gets information about a file or directory at the specified path.")]
    private FileInformation GetFileInformation(string path)
    {
        _logger.LogInformation("Getting file information for path: {Path}", path);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"The file or directory at '{path}' does not exist.");
        }

        return new FileInformation(
            fileInfo.Name,
            fileInfo.Length,
            fileInfo.Attributes,
            fileInfo.LastWriteTime);
    }

    [KernelFunction("read_file")]
    [Description(
        "Reads lines from a text file at the specified path. Supports reading from a specific line and limiting the number of lines." +
        "Binary files will read as hex string, 16 bytes per line.")]
    private async Task<ReadTextFileResult> ReadTextFileAsync(string path, int startLine = 0, int maxLines = 500, string encoding = "utf-8")
    {
        _logger.LogInformation(
            "Reading text file at path: {Path}, startLine: {StartLine}, maxLines: {MaxLines}, encoding: {Encoding}",
            path,
            startLine,
            maxLines,
            encoding);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File does not exist: {path}");
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > 10 * 1024 * 1024)
        {
            throw new InvalidOperationException("File too large, not supported.");
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (fileInfo.Length == 0)
        {
            return new ReadTextFileResult(string.Empty, false, true);
        }

        var stringBuilder = new StringBuilder();
        if (await IsBinaryFileAsync(stream))
        {
            // Read binary file as hex string, 16 bytes per line
            var buffer = new byte[16];
            int bytesRead;
            var currentLine = 0;

            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                if (currentLine >= startLine && maxLines > 0)
                {
                    var hexString = BitConverter.ToString(buffer, 0, bytesRead).Replace("-", " ");
                    stringBuilder.AppendLine(hexString);
                    maxLines--;
                }

                currentLine++;
                if (maxLines <= 0) break;
            }

            return new ReadTextFileResult(stringBuilder.ToString(), true, bytesRead == 0);
        }

        using var reader = new StreamReader(stream, Encoding.GetEncoding(encoding));
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;

            if (startLine > 0)
            {
                startLine--;
                continue;
            }

            stringBuilder.AppendLine(line);
            if (--maxLines <= 0) break;
        }

        return new ReadTextFileResult(stringBuilder.ToString(), false, reader.EndOfStream);
    }

    [KernelFunction("move_file")]
    [Description("Moves or renames a file or directory from sourcePath to destinationPath.")]
    private bool MoveFile(string sourcePath, string destinationPath)
    {
        _logger.LogInformation("Moving file from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Source and destination paths cannot be null or empty.");
        }
        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Source does not exist: {sourcePath}");
        }

        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath, overwrite: false);
        }
        else if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, destinationPath);
        }

        return true;
    }

    [KernelFunction("delete_file")]
    [Description("Deletes a file or an directory at the specified path.")]
    private bool DeleteFile(string path, bool recursive = false)
    {
        _logger.LogInformation("Deleting file at {Path}", path);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive);
        }
        else
        {
            throw new FileNotFoundException($"File or directory does not exist: {path}");
        }

        return true;
    }

    [KernelFunction("create_file")]
    [Description("Creates a new file at the specified path, similar to `touch` command in Unix.")]
    private bool CreateFile(
        string path,
        [Description("If true, throws an error if the file already exists. If false, overwrites the existing file.")]
        bool errorIfExists = true)
    {
        _logger.LogInformation("Creating file at {Path}, errorIfExists: {ErrorIfExists}", path, errorIfExists);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }
        if (errorIfExists && File.Exists(path))
        {
            throw new IOException("File already exists.");
        }

        using var stream = File.Create(path);
        return true;
    }

    [KernelFunction("create_directory")]
    [Description("Creates a new directory at the specified path.")]
    private void CreateDirectory(string path)
    {
        _logger.LogInformation("Creating directory at {Path}", path);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }
        if (Directory.Exists(path))
        {
            throw new IOException("Directory already exists.");
        }

        Directory.CreateDirectory(path);
    }

    [KernelFunction("write_text_file")]
    [Description("Writes content to a text file at the specified path. Binary files are not supported.")]
    private async Task WriteTextFile(string path, string content, string encoding = "utf-8", bool append = false)
    {
        _logger.LogInformation("Writing text file at {Path}, encoding: {Encoding}, append: {Append}", path, encoding, append);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty.", nameof(path));
        }
        ArgumentNullException.ThrowIfNull(content);

        await using var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        if (await IsBinaryFileAsync(stream))
        {
            throw new InvalidOperationException("Cannot write to a binary file.");
        }

        await using var writer = new StreamWriter(stream, Encoding.GetEncoding(encoding));
        await writer.WriteAsync(content);
        await writer.FlushAsync();
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