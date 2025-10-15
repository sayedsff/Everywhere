using System.ComponentModel;
using Everywhere.AI;
using Everywhere.Configuration;
using Everywhere.Database;
using Everywhere.Interop;
using Everywhere.Storage;
using Lucide.Avalonia;
using Microsoft.SemanticKernel;

namespace Everywhere.Chat.Plugins;

public class VisualTreePlugin : BuiltInChatPlugin
{
    public override LucideIconKind? Icon => LucideIconKind.Component;

    private readonly IBlobStorage _blobStorage;
    private readonly IVisualElementContext _visualElementContext;
    private readonly Settings _settings;

    public VisualTreePlugin(IBlobStorage blobStorage, IVisualElementContext visualElementContext, Settings settings) : base("VisualTree")
    {
        _blobStorage = blobStorage;
        _visualElementContext = visualElementContext;
        _settings = settings;

        _functions.Add(
            new AnonymousChatFunction(
                CaptureVisualElementByIdAsync,
                ChatFunctionPermissions.ScreenRead));
        _functions.Add(
            new AnonymousChatFunction(
                CaptureFullScreenAsync,
                ChatFunctionPermissions.ScreenRead));
    }

    /// <summary>
    /// When LLM does not support image input or image feature is disabled, and there is no visual element in the chat context, hide this plugin.
    /// </summary>
    /// <param name="chatContext"></param>
    /// <param name="customAssistant"></param>
    /// <returns></returns>
    public override IEnumerable<ChatFunction> SnapshotFunctions(ChatContext chatContext, CustomAssistant customAssistant) =>
        customAssistant.IsImageInputSupported.ActualValue is not true ||
        chatContext.VisualElements.Count == 0 ?
            [] :
            base.SnapshotFunctions(chatContext, customAssistant);

    [KernelFunction("capture_visual_element_by_id")]
    [Description("Captures a screenshot of the specified visual element by Id. Use when XML content is inaccessible or element is image-like.")]
    private Task<ChatFileAttachment> CaptureVisualElementByIdAsync([FromKernelServices] ChatContext chatContext, int elementId)
    {
        if (!chatContext.VisualElements.TryGetValue(elementId, out var visualElement))
        {
            throw new ArgumentException($"Visual element with id '{elementId}' is not found or has been destroyed.", nameof(elementId));
        }

        return CaptureVisualElementAsync(visualElement);
    }

    [KernelFunction("capture_full_screen")]
    [Description("Captures a screenshot of the entire screen. Use when no specific visual element is available.")]
    private Task<ChatFileAttachment> CaptureFullScreenAsync()
    {
        var visualElement = _visualElementContext.ElementFromPointer(PickElementMode.Screen);
        if (visualElement is null)
        {
            throw new InvalidOperationException("No screen is available to capture.");
        }

        return CaptureVisualElementAsync(visualElement);
    }

    private async Task<ChatFileAttachment> CaptureVisualElementAsync(IVisualElement visualElement)
    {
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