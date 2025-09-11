using System.ComponentModel;
using Everywhere.Database;
using Everywhere.Interop;
using Everywhere.Storage;
using Microsoft.SemanticKernel;
using ZLinq;

namespace Everywhere.Chat.Plugins;

public class VisualTreePlugin : BuiltInChatPlugin
{
    private readonly IBlobStorage _blobStorage;

    public VisualTreePlugin(IBlobStorage blobStorage) : base("VisualTree")
    {
        _blobStorage = blobStorage;

        _functions.Add(
            new AnonymousChatFunction(
                CaptureVisualElementAsync,
                ChatFunctionPermissions.ScreenRead));
    }

    /// <summary>
    /// When there is no visual element in the chat context, do not expose any function.
    /// </summary>
    /// <param name="chatContext"></param>
    /// <returns></returns>
    public override IEnumerable<ChatFunction> SnapshotFunctions(ChatContext chatContext) =>
        chatContext.VisualElementIdMap.Count == 0 ? [] : base.SnapshotFunctions(chatContext);

    [KernelFunction("capture_visual_element")]
    [Description("Captures a screenshot of the specified visual element by Id. Use when XML content is inaccessible or child elements are fewer than expected.")]
    private async Task<ChatFileAttachment> CaptureVisualElementAsync([FromKernelServices] ChatContext chatContext, int elementId)
    {
        var originalId = chatContext
            .VisualElementIdMap
            .AsValueEnumerable()
            .FirstOrDefault(kv => kv.Value == elementId)
            .Key;
        if (originalId.IsNullOrEmpty())
        {
            throw new ArgumentException($"Element with id {elementId} does not exist.");
        }

        var visualElement = chatContext
            .AsValueEnumerable()
            .Select(n => n.Message)
            .OfType<UserChatMessage>()
            .SelectMany(m => m.Attachments)
            .OfType<ChatVisualElementAttachment>()
            .Select(a => a.Element)
            .OfType<IVisualElement>()
            .FirstOrDefault(e => e.Id == originalId);
        if (visualElement is null)
        {
            throw new ArgumentException($"Visual element with id '{elementId}' is not found or not available at this time.", nameof(elementId));
        }

        var bitmap = await visualElement.CaptureAsync();

        BlobEntity blob;
        using (var stream = new MemoryStream())
        {
            bitmap.Save(stream, 100);
            blob = await _blobStorage.StorageBlobAsync(stream, "image/png");
        }

        return new ChatFileAttachment(
            new DynamicResourceKey(string.Empty),
            blob.LocalPath,
            blob.Sha256,
            blob.MimeType);
    }
}