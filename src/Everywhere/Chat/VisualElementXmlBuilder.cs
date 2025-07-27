using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Text;
#if DEBUG
using JetBrains.Profiler.Api;
#endif
using Microsoft.KernelMemory.AI;

namespace Everywhere.Chat;

/// <summary>
///     This class builds an XML representation of the core elements, which is limited by the soft token limit and finally used by a LLM.
/// </summary>
/// <param name="coreElements"></param>
/// <param name="tokenizer"></param>
/// <param name="softTokenLimit"></param>
public class VisualElementXmlBuilder(IReadOnlyList<IVisualElement> coreElements, ITextTokenizer tokenizer, int softTokenLimit)
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

    private readonly Dictionary<string, int> idMap = [];
    private readonly HashSet<XmlVisualElement> rootElements = [];
    private StringBuilder? visualTreeXmlBuilder;

    public string BuildXml(CancellationToken cancellationToken)
    {
        EnsureBuilt(cancellationToken);
        return visualTreeXmlBuilder.ToString();
    }

    public IReadOnlyDictionary<string, int> GetIdMap(CancellationToken cancellationToken)
    {
        EnsureBuilt(cancellationToken);
        return idMap;
    }

    public IEnumerable<IVisualElement> GetRootElements(CancellationToken cancellationToken)
    {
        EnsureBuilt(cancellationToken);
        return rootElements.Select(e => e.Element);
    }

    [MemberNotNull(nameof(visualTreeXmlBuilder))]
    private void EnsureBuilt(CancellationToken cancellationToken)
    {
        if (coreElements.Count == 0) throw new InvalidOperationException("No core elements to build XML from.");

        if (visualTreeXmlBuilder != null) return;
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
                    // because some text elements' name is the same as text.
                    description = TruncateIfNeeded(name, remainingTokenCount);
                }
            }
            content ??= text is { Length: > 0 } ? TruncateIfNeeded(text, remainingTokenCount) : null;
            var contents = content?.Split('\n') ?? [];

            var tokenCount = 8; // estimated token count for the indentation, start tag and id
            if (description != null) tokenCount += tokenizer.CountTokens(description) + 3;
            tokenCount += contents.Length switch
            {
                1 => contents.Sum(tokenizer.CountTokens),
                > 1 => contents.Sum(line => tokenizer.CountTokens(line) + 4) + 8, // > 1, +4 for the indentation, +8 for the end tag
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
            rootElements.Add(current);
        }

        visualTreeXmlBuilder = new StringBuilder();
        foreach (var rootElement in rootElements) InternalBuildXml(rootElement, 0);
        visualTreeXmlBuilder.TrimEnd();

#if DEBUG
        MeasureProfiler.SaveData();
        sw.Stop();
        Debug.WriteLine($"BuildInternal finished in {sw.ElapsedMilliseconds}ms");
#endif

        string TruncateIfNeeded(string text, int maxLength)
        {
            var tokenCount = tokenizer.CountTokens(text);
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
            visualTreeXmlBuilder.Append(indent).Append('<').Append(element.Type);

            // Add ID
            var id = idMap.Count;
            idMap[element.Id] = id;
            visualTreeXmlBuilder.Append(" id=\"").Append(id).Append('"');

            if (xmlElement.Description != null)
            {
                visualTreeXmlBuilder.Append(" description=\"").Append(SecurityElement.Escape(xmlElement.Description)).Append('"');
            }

            if (xmlElement.Contents.Count == 1)
            {
                visualTreeXmlBuilder.Append(" content=\"").Append(SecurityElement.Escape(xmlElement.Contents[0])).Append('"');
            }

            if (xmlElement.Children.Count == 0 && xmlElement.Contents.Count == 0)
            {
                // Self-closing tag if no children and no content
                visualTreeXmlBuilder.Append("/>").AppendLine();
                return;
            }

            visualTreeXmlBuilder.Append('>').AppendLine();

            // Add contents
            foreach (var contentLine in xmlElement.Contents)
            {
                if (string.IsNullOrWhiteSpace(contentLine)) continue;
                visualTreeXmlBuilder
                    .Append(indent)
                    .Append("  ")
                    .Append(SecurityElement.Escape(contentLine))
                    .AppendLine();
            }

            // Handle child elements
            foreach (var child in xmlElement.Children) InternalBuildXml(child, indentLevel + 1);

            // End tag
            visualTreeXmlBuilder.Append(indent).Append("</").Append(element.Type).Append('>').AppendLine();
        }
    }
}