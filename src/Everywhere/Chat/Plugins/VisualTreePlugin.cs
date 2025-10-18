using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private readonly IBlobStorage blobStorage;
    private readonly IVisualElementContext visualElementContext;
    private readonly Settings settings;
    private static readonly JsonSerializerOptions ActionSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly Dictionary<string, VirtualKey> ShortcutKeyAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = VirtualKey.Enter,
        ["return"] = VirtualKey.Enter,
        ["tab"] = VirtualKey.Tab,
        ["escape"] = VirtualKey.Escape,
        ["esc"] = VirtualKey.Escape,
        ["space"] = VirtualKey.Space,
        ["spacebar"] = VirtualKey.Space,
        ["backspace"] = VirtualKey.Backspace,
        ["delete"] = VirtualKey.Delete,
        ["del"] = VirtualKey.Delete,
        ["left"] = VirtualKey.Left,
        ["right"] = VirtualKey.Right,
        ["up"] = VirtualKey.Up,
        ["down"] = VirtualKey.Down,
        ["home"] = (VirtualKey)0x24,
        ["end"] = (VirtualKey)0x23,
        ["pageup"] = (VirtualKey)0x21,
        ["pagedown"] = (VirtualKey)0x22,
        ["f1"] = (VirtualKey)0x70,
        ["f2"] = (VirtualKey)0x71,
        ["f3"] = (VirtualKey)0x72,
        ["f4"] = (VirtualKey)0x73,
        ["f5"] = (VirtualKey)0x74,
        ["f6"] = (VirtualKey)0x75,
        ["f7"] = (VirtualKey)0x76,
        ["f8"] = (VirtualKey)0x77,
        ["f9"] = (VirtualKey)0x78,
        ["f10"] = (VirtualKey)0x79,
        ["f11"] = (VirtualKey)0x7A,
        ["f12"] = (VirtualKey)0x7B
    };

    private static readonly Dictionary<string, VirtualKey> ShortcutModifierAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = VirtualKey.Control,
        ["control"] = VirtualKey.Control,
        ["shift"] = VirtualKey.Shift,
        ["alt"] = VirtualKey.Alt,
        ["option"] = VirtualKey.Alt,
        ["win"] = VirtualKey.Windows,
        ["windows"] = VirtualKey.Windows,
        ["meta"] = VirtualKey.Windows,
        ["cmd"] = VirtualKey.Windows,
        ["command"] = VirtualKey.Windows
    };

    public VisualTreePlugin(IBlobStorage blobStorage, IVisualElementContext visualElementContext, Settings settings) : base("VisualTree")
    {
    this.blobStorage = blobStorage;
    this.visualElementContext = visualElementContext;
    this.settings = settings;

        _functions.Add(
            new AnonymousChatFunction(
                CaptureVisualElementByIdAsync,
                ChatFunctionPermissions.ScreenRead));
        _functions.Add(
            new AnonymousChatFunction(
                CaptureFullScreenAsync,
                ChatFunctionPermissions.ScreenRead));
        _functions.Add(
            new AnonymousChatFunction(
                ExecuteVisualActionQueueAsync,
                ChatFunctionPermissions.ScreenAccess));
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
    return CaptureVisualElementAsync(ResolveVisualElement(chatContext, elementId, nameof(elementId)));
    }

    [KernelFunction("capture_full_screen")]
    [Description("Captures a screenshot of the entire screen. Use when no specific visual element is available.")]
    private Task<ChatFileAttachment> CaptureFullScreenAsync()
    {
    var visualElement = visualElementContext.ElementFromPointer(PickElementMode.Screen);
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
            blob = await blobStorage.StorageBlobAsync(stream, "image/png");
        }

        return new ChatFileAttachment(
            new DynamicResourceKey(string.Empty),
            blob.LocalPath,
            blob.Sha256,
            blob.MimeType);
    }

    [KernelFunction("execute_visual_action_queue")]
    [Description("Executes a reliable UI automation action queue. Supports clicking elements, entering text, sending shortcuts (e.g., Ctrl+V), and waiting without simulating pointer input. Useful for automating stable interactions, even when the target window is minimized.")]
    private async Task<string> ExecuteVisualActionQueueAsync(
        [FromKernelServices] ChatContext chatContext,
    [Description("JSON array describing the action queue. Example: [{\"type\":\"click\",\"element_id\":1},{\"type\":\"set_text\",\"element_id\":2,\"text\":\"hello\"},{\"type\":\"send_shortcut\",\"element_id\":2,\"shortcut\":\"ctrl+enter\"},{\"type\":\"wait\",\"delay_ms\":500}]")] string actionsJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionsJson);
        var steps = DeserializeActions(actionsJson);
        if (steps.Count == 0)
        {
            throw new ArgumentException("Action queue cannot be empty.", nameof(actionsJson));
        }

        var index = 0;
        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            index++;

            if (!TryParseActionType(step.Type, out var actionType))
            {
                throw new ArgumentException($"Unsupported action type '{step.Type}' at position {index}.", nameof(actionsJson));
            }

            try
            {
                switch (actionType)
                {
                    case VisualActionType.Click:
                    {
                        var element = ResolveVisualElement(chatContext, step.RequireElementId(), nameof(actionsJson));
                        await element.InvokeAsync(cancellationToken).ConfigureAwait(false);
                        break;
                    }
                    case VisualActionType.SetText:
                    {
                        var element = ResolveVisualElement(chatContext, step.RequireElementId(), nameof(actionsJson));
                        await element.SetTextAsync(step.Text ?? string.Empty, cancellationToken).ConfigureAwait(false);
                        break;
                    }
                    case VisualActionType.SendShortcut:
                    {
                        var element = ResolveVisualElement(chatContext, step.RequireElementId(), nameof(actionsJson));
                        var shortcut = ResolveShortcut(step, nameof(actionsJson));
                        await element.SendShortcutAsync(shortcut, cancellationToken).ConfigureAwait(false);
                        break;
                    }
                    case VisualActionType.Wait:
                    {
                        var delay = step.ResolveDelayMilliseconds();
                        if (delay < 0)
                        {
                            throw new ArgumentException($"Delay must be non-negative for wait actions (step {index}).", nameof(actionsJson));
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(delay), cancellationToken).ConfigureAwait(false);
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Action #{index} ({actionType}) failed: {ex.Message}", ex);
            }
        }

        return $"Executed {steps.Count} action(s).";
    }

    private static IReadOnlyList<VisualActionStep> DeserializeActions(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<VisualActionStep>>(json, ActionSerializerOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The action queue payload is not valid JSON.", nameof(json), ex);
        }
    }

    private static bool TryParseActionType(string? value, out VisualActionType actionType)
    {
        actionType = VisualActionType.Click;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);
        return normalized switch
        {
            "click" or "invoke" or "press" => (actionType = VisualActionType.Click) == VisualActionType.Click,
            "settext" or "input" or "inputtext" or "type" or "entertext" => (actionType = VisualActionType.SetText) == VisualActionType.SetText,
            "sendshortcut" or "shortcut" or "sendkey" or "presskey" => (actionType = VisualActionType.SendShortcut) == VisualActionType.SendShortcut,
            "wait" or "delay" or "sleep" => (actionType = VisualActionType.Wait) == VisualActionType.Wait,
            _ => false
        };
    }

    private static VisualElementShortcut ResolveShortcut(VisualActionStep step, string argumentName)
    {
        if (!string.IsNullOrWhiteSpace(step.Shortcut))
        {
            return ParseShortcutExpression(step.Shortcut, argumentName);
        }

        if (!string.IsNullOrWhiteSpace(step.Key))
        {
            var key = ParseShortcutKey(step.Key, argumentName);
            var modifiers = ParseShortcutModifiers(step.Modifiers, argumentName);
            return new VisualElementShortcut(key, modifiers);
        }

        throw new ArgumentException("Shortcut is required for send_shortcut actions.", argumentName);
    }

    private static VisualElementShortcut ParseShortcutExpression(string shortcutExpression, string argumentName)
    {
        var tokens = shortcutExpression.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            throw new ArgumentException("Shortcut expression cannot be empty.", argumentName);
        }

        var keyToken = tokens[^1];
        var key = ParseShortcutKey(keyToken, argumentName);
        var modifiers = ParseShortcutModifiers(tokens.Take(tokens.Length - 1).ToArray(), argumentName);
        return new VisualElementShortcut(key, modifiers);
    }

    private static VirtualKey ParseShortcutKey(string token, string argumentName)
    {
        var normalized = NormalizeShortcutToken(token);

        if (ShortcutKeyAliases.TryGetValue(normalized, out var mapped))
        {
            return mapped;
        }

        if (normalized.Length == 1)
        {
            var ch = char.ToUpperInvariant(normalized[0]);
            if (char.IsLetterOrDigit(ch))
            {
                return (VirtualKey)ch;
            }
        }

        throw new ArgumentException($"Unsupported shortcut key token '{token}'.", argumentName);
    }

    private static VirtualKey ParseShortcutModifiers(IReadOnlyCollection<string>? tokens, string argumentName)
    {
        if (tokens is null || tokens.Count == 0) return VirtualKey.None;

        var modifiers = VirtualKey.None;
        foreach (var raw in tokens)
        {
            var normalized = NormalizeShortcutToken(raw);
            if (normalized.Length == 0)
            {
                continue;
            }

            if (ShortcutModifierAliases.TryGetValue(normalized, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            throw new ArgumentException($"Unsupported shortcut modifier token '{raw}'.", argumentName);
        }

        return modifiers;
    }

    private static string NormalizeShortcutToken(string token) =>
        token.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);

    private static IVisualElement ResolveVisualElement(ChatContext chatContext, int elementId, string? argumentName = null)
    {
        if (!chatContext.VisualElements.TryGetValue(elementId, out var visualElement))
        {
            throw new ArgumentException($"Visual element with id '{elementId}' is not found or has been destroyed.", argumentName ?? nameof(elementId));
        }

        return visualElement;
    }

    private sealed record VisualActionStep
    {
        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("element_id")]
        public int? ElementId { get; init; }

        [JsonPropertyName("target_id")]
        public int? TargetId { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("shortcut")]
        public string? Shortcut { get; init; }

        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("modifiers")]
        public string[]? Modifiers { get; init; }

        [JsonPropertyName("delay_ms")]
        public int? DelayMilliseconds { get; init; }

        [JsonPropertyName("duration_ms")]
        public int? DurationMilliseconds { get; init; }

        public int RequireElementId() => ElementId ?? TargetId ?? throw new ArgumentException("Element id is required for this action.");

        public int ResolveDelayMilliseconds()
        {
            var delay = DelayMilliseconds ?? DurationMilliseconds;
            if (delay is null)
            {
                throw new ArgumentException("Delay must be provided for wait actions.");
            }

            return delay.Value;
        }
    }

    private enum VisualActionType
    {
        Click,
        SetText,
        Wait,
        SendShortcut
    }
}