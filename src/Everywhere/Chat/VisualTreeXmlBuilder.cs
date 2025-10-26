using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Everywhere.Interop;
using ZLinq;

namespace Everywhere.Chat;

public enum VisualTreeDetailLevel
{
    [DynamicResourceKey(LocaleKey.VisualTreeDetailLevel_Minimal)]
    Minimal,

    [DynamicResourceKey(LocaleKey.VisualTreeDetailLevel_Compact)]
    Compact,

    [DynamicResourceKey(LocaleKey.VisualTreeDetailLevel_Detailed)]
    Detailed,
}

/// <summary>
///     This class builds an XML representation of the core elements, which is limited by the soft token limit and finally used by a LLM.
/// </summary>
/// <param name="coreElements"></param>
/// <param name="approximateTokenLimit"></param>
/// <param name="detailLevel"></param>
public partial class VisualTreeXmlBuilder(
    IReadOnlyList<IVisualElement> coreElements,
    int approximateTokenLimit,
    int startingId,
    VisualTreeDetailLevel detailLevel
)
{
    private enum QueueOrigin
    {
        CoreElement,
        Parent,
        PreviousSibling,
        NextSibling,
        FirstChild
    }

    private record QueuedElement(IVisualElement Element, QueueOrigin Origin, string? ParentId);

    private record XmlVisualElement(IVisualElement Element, string? Description, IReadOnlyList<string> Contents, int TokenCount)
    {
        public XmlVisualElement? Parent { get; set; }

        public List<XmlVisualElement> Children { get; } = [];

        public bool ShouldRender { get; set; } = true;

        public bool HasInformativeContent { get; set; }

        public int InformativeChildCount { get; set; }

        public virtual bool Equals(XmlVisualElement? other) => Element.Id == other?.Element.Id;

        public override int GetHashCode() => Element.Id.GetHashCode();
    }

    /// <summary>
    ///     The mapping from original element ID to the built sequential ID starting from <see cref="startingId"/>.
    /// </summary>
    public Dictionary<int, IVisualElement> BuiltVisualElements { get; } = new();

    private readonly HashSet<string> _coreElementIds = coreElements
        .Select(e => e.Id)
        .Where(id => !string.IsNullOrEmpty(id))
        .ToHashSet(StringComparer.Ordinal);

    private readonly HashSet<XmlVisualElement> _rootElements = [];
    private StringBuilder? _visualTreeXmlBuilder;
    private bool _detailLevelApplied;

    private const VisualElementStates InteractiveStates = VisualElementStates.Focused | VisualElementStates.Selected;

    public string BuildXml(CancellationToken cancellationToken)
    {
        if (coreElements.Count == 0) throw new InvalidOperationException("No core elements to build XML from.");

        if (_visualTreeXmlBuilder != null) return _visualTreeXmlBuilder.ToString();
        cancellationToken.ThrowIfCancellationRequested();

        var buildQueue = new Queue<QueuedElement>(coreElements.Select(e => new QueuedElement(e, QueueOrigin.CoreElement, e.Parent?.Id)));
        var visitedElements = new Dictionary<string, XmlVisualElement>();

        while (buildQueue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var accumulatedTokenCount = visitedElements.Values.Sum(e => e.TokenCount);
            var remainingTokenCount = approximateTokenLimit - accumulatedTokenCount;
            if (remainingTokenCount <= 0) break;

            var (element, queueOrigin, parentId) = buildQueue.Dequeue();
            var id = element.Id;
            if (visitedElements.ContainsKey(id)) continue;

            string? description = null;
            string? content = null;
            var isTextElement = element.Type is VisualElementType.Label or VisualElementType.TextEdit or VisualElementType.Document;
            var text = element.GetText();
            if (element.Name is { Length: > 0 } name)
            {
                if (isTextElement && string.IsNullOrEmpty(text))
                {
                    content = TruncateIfNeeded(name, remainingTokenCount);
                }
                else if (!isTextElement || name != text)
                {
                    // If the element is not a text element, or the name is different from the text, add the name as a description
                    // because the name of some text elements is the same as the text.
                    description = TruncateIfNeeded(name, remainingTokenCount);
                }
            }
            content ??= text is { Length: > 0 } ? TruncateIfNeeded(text, remainingTokenCount) : null;
            var contents = content?.Split('\n') ?? [];

            var tokenCount = 8; // estimated token count for the indentation, start tag and id
            if (description != null) tokenCount += EstimateTokenCount(description) + 3;
            tokenCount += contents.Length switch
            {
                1 => contents.Sum(EstimateTokenCount),
                > 1 => contents.Sum(line => EstimateTokenCount(line) + 4) + 8, // > 1, +4 for the indentation, +8 for the end tag
                _ => 0
            };

            var xmlElement = visitedElements[element.Id] = new XmlVisualElement(element, description, contents, tokenCount);
            if (parentId != null && visitedElements.TryGetValue(parentId, out var parentXmlElement))
            {
                if (queueOrigin == QueueOrigin.PreviousSibling) parentXmlElement.Children.Insert(0, xmlElement);
                else parentXmlElement.Children.Add(xmlElement);
                xmlElement.Parent = parentXmlElement;
            }

            remainingTokenCount -= tokenCount;
            if (remainingTokenCount < 0) break;

            if (queueOrigin is not QueueOrigin.FirstChild)
            {
                var parent = element.Parent;
                if (parent != null)
                {
                    buildQueue.Enqueue(new QueuedElement(parent, QueueOrigin.Parent, parent.Parent?.Id));
                }
            }

            if (queueOrigin is not QueueOrigin.NextSibling and not QueueOrigin.FirstChild) // first child's previous sibling is always null
            {
                var previousSibling = element.PreviousSibling;
                if (previousSibling != null && !visitedElements.ContainsKey(previousSibling.Id))
                {
                    buildQueue.Enqueue(new QueuedElement(previousSibling, QueueOrigin.PreviousSibling, parentId));
                }
            }

            if (queueOrigin is not QueueOrigin.PreviousSibling)
            {
                var nextSibling = element.NextSibling;
                if (nextSibling != null && !visitedElements.ContainsKey(nextSibling.Id))
                {
                    buildQueue.Enqueue(new QueuedElement(nextSibling, QueueOrigin.NextSibling, parentId));
                }
            }

            if (queueOrigin is not QueueOrigin.Parent)
            {
                var firstChild = element.Children.FirstOrDefault();
                if (firstChild != null && !visitedElements.ContainsKey(firstChild.Id))
                {
                    buildQueue.Enqueue(new QueuedElement(firstChild, QueueOrigin.FirstChild, id));
                }
            }
        }

        // build the XML representation of the visual tree
        foreach (var visitedElement in visitedElements.Values)
        {
            var current = visitedElement;
            while (current.Parent != null) current = current.Parent;
            _rootElements.Add(current);
        }

        ApplyDetailLevel();

        _visualTreeXmlBuilder = new StringBuilder();
        foreach (var rootElement in _rootElements) InternalBuildXml(rootElement, 0);
        _visualTreeXmlBuilder.TrimEnd();

        string TruncateIfNeeded(string text, int maxLength)
        {
            var tokenCount = EstimateTokenCount(text);
            if (maxLength <= 0 || tokenCount <= maxLength)
                return text;

            var approximateLength = text.Length * maxLength / tokenCount;
            return text[..Math.Max(0, approximateLength - 1)] + "...";
        }

        void InternalBuildXml(XmlVisualElement xmlElement, int indentLevel)
        {
            if (!xmlElement.ShouldRender)
            {
                foreach (var child in xmlElement.Children)
                {
                    InternalBuildXml(child, indentLevel);
                }
                return;
            }

            var indent = new string(' ', indentLevel * 2);
            var element = xmlElement.Element;
            var elementType = element.Type;

            // Start tag
            _visualTreeXmlBuilder.Append(indent).Append('<').Append(elementType);

            // Add ID
            var id = BuiltVisualElements.Count + startingId;
            BuiltVisualElements[id] = element;
            _visualTreeXmlBuilder.Append(" id=\"").Append(id).Append('"');

            var isContainer = elementType is
                VisualElementType.Document or
                VisualElementType.Panel or
                VisualElementType.TopLevel or
                VisualElementType.Screen;
            var includeBounds = isContainer && detailLevel != VisualTreeDetailLevel.Minimal;
            if (includeBounds)
            {
                // for containers, include the element's size
                var bounds = element.BoundingRectangle;
                _visualTreeXmlBuilder
                    .Append(" x=\"").Append(bounds.X).Append('"')
                    .Append(" y=\"").Append(bounds.Y).Append('"')
                    .Append(" width=\"").Append(bounds.Width).Append('"')
                    .Append(" height=\"").Append(bounds.Height).Append('"');
            }

            if (xmlElement.Description != null)
            {
                _visualTreeXmlBuilder.Append(" description=\"").Append(SecurityElement.Escape(xmlElement.Description)).Append('"');
            }

            if (xmlElement.Contents.Count == 1)
            {
                _visualTreeXmlBuilder.Append(" content=\"").Append(SecurityElement.Escape(xmlElement.Contents[0])).Append('"');
            }

            if (xmlElement.Children.Count == 0 && xmlElement.Contents.Count == 0)
            {
                bool shouldWarn = false;
                if (includeBounds)
                {
                    switch (detailLevel)
                    {
                        case VisualTreeDetailLevel.Detailed:
                            shouldWarn = element.BoundingRectangle is { Width: > 64, Height: > 64 };
                            break;
                        case VisualTreeDetailLevel.Compact:
                            shouldWarn = element.BoundingRectangle is { Width: > 256, Height: > 256 };
                            break;
                        case VisualTreeDetailLevel.Minimal:
                            shouldWarn = false;
                            break;
                    }
                }
                if (shouldWarn)
                {
                    _visualTreeXmlBuilder.Append(" warning=\"XML content may be inaccessible!\"");
                }

                // Self-closing tag if no children and no content
                _visualTreeXmlBuilder.Append("/>").AppendLine();
                return;
            }

            _visualTreeXmlBuilder.Append('>').AppendLine();

            // Add contents
            foreach (var contentLine in xmlElement.Contents)
            {
                if (string.IsNullOrWhiteSpace(contentLine)) continue;
                _visualTreeXmlBuilder
                    .Append(indent)
                    .Append("  ")
                    .Append(SecurityElement.Escape(contentLine))
                    .AppendLine();
            }

            // Handle child elements
            foreach (var child in xmlElement.Children) InternalBuildXml(child, indentLevel + 1);

            // End tag
            _visualTreeXmlBuilder.Append(indent).Append("</").Append(element.Type).Append('>').AppendLine();
        }

        return _visualTreeXmlBuilder.ToString();
    }

    private void ApplyDetailLevel()
    {
        if (_detailLevelApplied || detailLevel == VisualTreeDetailLevel.Detailed) return;

        foreach (var rootElement in _rootElements)
        {
            MarkRenderFlags(rootElement);
        }

        _detailLevelApplied = true;
    }

    private bool MarkRenderFlags(XmlVisualElement element)
    {
        var informativeChildCount = 0;
        foreach (var child in element.Children)
        {
            if (MarkRenderFlags(child)) informativeChildCount++;
        }

        var hasTextContent = element.Contents.Any(static line => !string.IsNullOrWhiteSpace(line));
        var hasDescription = !string.IsNullOrWhiteSpace(element.Description);
        var interactive = IsInteractiveElement(element.Element);
        var elementId = element.Element.Id;
        var isCoreElement = elementId is { Length: > 0 } && _coreElementIds.Contains(elementId);

        var hasSelfInformativeContent = hasTextContent || hasDescription || interactive || isCoreElement;
        var hasInformativeDescendant = informativeChildCount > 0;

        var shouldRender = hasSelfInformativeContent;
        if (!shouldRender)
        {
            shouldRender = detailLevel switch
            {
                VisualTreeDetailLevel.Compact => ShouldKeepContainerForCompact(element, informativeChildCount),
                VisualTreeDetailLevel.Minimal => ShouldKeepContainerForMinimal(element, informativeChildCount),
                _ => hasInformativeDescendant
            };
        }

        element.ShouldRender = shouldRender;
        element.InformativeChildCount = informativeChildCount;
        element.HasInformativeContent = hasSelfInformativeContent || hasInformativeDescendant;

        return element.HasInformativeContent;
    }

    private static bool ShouldKeepContainerForCompact(XmlVisualElement element, int informativeChildCount)
    {
        if (element.Parent is null) return informativeChildCount > 0;

        var type = element.Element.Type;
        return type switch
        {
            VisualElementType.Screen or VisualElementType.TopLevel => informativeChildCount > 1,
            VisualElementType.Document => informativeChildCount > 0,
            VisualElementType.Panel => informativeChildCount > 1,
            _ => false
        };
    }

    private static bool ShouldKeepContainerForMinimal(XmlVisualElement element, int informativeChildCount)
    {
        if (element.Parent is null)
        {
            return informativeChildCount > 0;
        }

        return false;
    }

    private static bool IsInteractiveElement(IVisualElement element)
    {
        if (element.Type is VisualElementType.Button or
            VisualElementType.Hyperlink or
            VisualElementType.CheckBox or
            VisualElementType.RadioButton or
            VisualElementType.ComboBox or
            VisualElementType.ListView or
            VisualElementType.ListViewItem or
            VisualElementType.TreeView or
            VisualElementType.TreeViewItem or
            VisualElementType.DataGrid or
            VisualElementType.DataGridItem or
            VisualElementType.TabControl or
            VisualElementType.TabItem or
            VisualElementType.Menu or
            VisualElementType.MenuItem or
            VisualElementType.Slider or
            VisualElementType.ScrollBar or
            VisualElementType.ProgressBar or
            VisualElementType.TextEdit or
            VisualElementType.Table or
            VisualElementType.TableRow) return true;

        return (element.States & InteractiveStates) != 0;
    }

    // The token-to-word ratio for English/Latin-based text.
    private const double EnglishTokenRatio = 3.0;

    // The token-to-character ratio for CJK-based text.
    private const double CjkTokenRatio = 2.0;

    /// <summary>
    ///     Approximates the number of LLM tokens for a given string.
    ///     This method first detects the language family of the string and then applies the corresponding heuristic.
    /// </summary>
    /// <param name="text">The input string to calculate the token count for.</param>
    /// <returns>An approximate number of tokens.</returns>
    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return IsCjkLanguage(text) ? (int)Math.Ceiling(text.Length * CjkTokenRatio) : (int)Math.Ceiling(CountWords(text) * EnglishTokenRatio);
    }

    /// <summary>
    ///     Detects if a string is predominantly composed of CJK characters.
    ///     This method makes a judgment by calculating the proportion of CJK characters.
    /// </summary>
    /// <param name="text">The string to be checked.</param>
    /// <returns>True if the string is mainly CJK, false otherwise.</returns>
    private static bool IsCjkLanguage(string text)
    {
        var cjkCount = 0;
        var totalChars = 0;

        foreach (var c in text.AsValueEnumerable().Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c)))
        {
            totalChars++;
            // Use regex to match CJK characters
            if (CjkRegex().IsMatch(c.ToString()))
            {
                cjkCount++;
            }
        }

        // Set a threshold: if the proportion of CJK characters exceeds 10%, it is considered a CJK language.
        return totalChars > 0 && (double)cjkCount / totalChars > 0.1;
    }

    /// <summary>
    ///     Counts the number of words in a string using a regular expression.
    ///     This method matches sequences of non-whitespace characters to provide a more accurate word count than simple splitting.
    /// </summary>
    /// <param name="s">The string in which to count words.</param>
    /// <returns>The number of words.</returns>
    private static int CountWords(string s)
    {
        // Matches one or more non-whitespace characters, considered as a single word.
        var collection = WordCountRegex().Matches(s);
        return collection.Count;
    }

    /// <summary>
    ///     Regex to match CJK characters, including Chinese, Japanese, and Korean.
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\p{IsCJKUnifiedIdeographs}|\p{IsCJKCompatibility}|\p{IsHangulJamo}|\p{IsHangulSyllables}|\p{IsHangulCompatibilityJamo}")]
    private static partial Regex CjkRegex();

    /// <summary>
    ///     Regex to match words (sequences of non-whitespace characters).
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\S+")]
    private static partial Regex WordCountRegex();
}