using LiveMarkdown.Avalonia;

namespace Everywhere.Chat.Plugins;

/// <summary>
/// Allows chat plugins to display content in the user interface.
/// </summary>
public interface IChatPluginDisplaySink
{
    /// <summary>
    /// Appends plain text to the display sink.
    /// </summary>
    /// <param name="text"></param>
    void AppendText(string text);

    /// <summary>
    /// Appends a dynamic resource key to the display sink.
    /// </summary>
    /// <param name="resourceKey"></param>
    void AppendDynamicResourceKey(DynamicResourceKeyBase resourceKey);

    /// <summary>
    /// Appends a markdown builder to the display sink. The caller can use the returned builder to build markdown content.
    /// </summary>
    /// <returns></returns>
    ObservableStringBuilder AppendMarkdown();

    /// <summary>
    /// Appends a progress indicator to the display sink. The caller can use the returned progress reporter to report progress between 0.0 and 1.0.
    /// </summary>
    /// <returns>a progress reporter that accepts values between 0.0 and 1.0, NaN for indeterminate progress</returns>
    IProgress<double> AppendProgress(DynamicResourceKeyBase headerKey);

    /// <summary>
    /// Appends a file reference to the display sink.
    /// </summary>
    /// <param name="references"></param>
    void AppendFileReferences(params IReadOnlyList<ChatPluginFileReference> references);

    /// <summary>
    /// Appends a text file difference to the display sink and waits for the user to review it.
    /// </summary>
    /// <param name="difference"></param>
    /// <param name="originalText"></param>
    void AppendFileDifference(TextDifference difference, string originalText);

    void AppendUrls(IReadOnlyList<ChatPluginUrl> urls);
}