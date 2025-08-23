using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Everywhere.Interop;
using ZLinq;
#if DEBUG
using JetBrains.Profiler.Api;
#endif

namespace Everywhere.Chat;

/// <summary>
///     This class builds an XML representation of the core elements, which is limited by the soft token limit and finally used by a LLM.
/// </summary>
/// <param name="coreElements"></param>
/// <param name="softTokenLimit"></param>
public partial class VisualElementXmlBuilder(IReadOnlyList<IVisualElement> coreElements, int softTokenLimit)
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

        public virtual bool Equals(XmlVisualElement? other) => Element.Id == other?.Element.Id;

        public override int GetHashCode() => Element.Id.GetHashCode();
    }

    private readonly Dictionary<string, int> _idMap = [];
    private readonly HashSet<XmlVisualElement> _rootElements = [];
    private StringBuilder? _visualTreeXmlBuilder;

    public string BuildXml(CancellationToken cancellationToken)
    {
        EnsureBuilt(cancellationToken);
        return _visualTreeXmlBuilder.ToString();
    }

    public IReadOnlyDictionary<string, int> GetIdMap(CancellationToken cancellationToken)
    {
        EnsureBuilt(cancellationToken);
        return _idMap;
    }

    public IEnumerable<IVisualElement> GetRootElements(CancellationToken cancellationToken)
    {
        EnsureBuilt(cancellationToken);
        return _rootElements.Select(e => e.Element);
    }

    [MemberNotNull(nameof(_visualTreeXmlBuilder))]
    private void EnsureBuilt(CancellationToken cancellationToken)
    {
        if (coreElements.Count == 0) throw new InvalidOperationException("No core elements to build XML from.");

        if (_visualTreeXmlBuilder != null) return;
        cancellationToken.ThrowIfCancellationRequested();

#if DEBUG
        Debug.WriteLine("BuildInternal started...");
        var sw = Stopwatch.StartNew();
        MeasureProfiler.StartCollectingData();
#endif

        var buildQueue = new Queue<QueuedElement>(coreElements.Select(e => new QueuedElement(e, QueueOrigin.CoreElement, e.Parent?.Id)));
        var visitedElements = new Dictionary<string, XmlVisualElement>();

        while (buildQueue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var accumulatedTokenCount = visitedElements.Values.Sum(e => e.TokenCount);
            var remainingTokenCount = softTokenLimit - accumulatedTokenCount;
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

        _visualTreeXmlBuilder = new StringBuilder();
        foreach (var rootElement in _rootElements) InternalBuildXml(rootElement, 0);
        _visualTreeXmlBuilder.TrimEnd();

#if DEBUG
        MeasureProfiler.SaveData();
        sw.Stop();
        Debug.WriteLine($"BuildInternal finished in {sw.ElapsedMilliseconds}ms");
#endif

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
            var indent = new string(' ', indentLevel * 2);
            var element = xmlElement.Element;

            // Start tag
            _visualTreeXmlBuilder.Append(indent).Append('<').Append(element.Type);

            // Add ID
            var id = _idMap.Count;
            _idMap[element.Id] = id;
            _visualTreeXmlBuilder.Append(" id=\"").Append(id).Append('"');

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
    }

    // The token-to-word ratio for English/Latin-based text.
    private const double EnglishTokenRatio = 2.0;

    // The token-to-character ratio for CJK-based text.
    private const double CjkTokenRatio = 1.0;

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

        return IsCjkLanguage(text)
            ? (int)Math.Ceiling(text.Length * CjkTokenRatio)
            : (int)Math.Ceiling(CountWords(text) * EnglishTokenRatio);
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