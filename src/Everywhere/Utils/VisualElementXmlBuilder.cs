using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Text;

namespace Everywhere.Utils;

public class VisualElementXmlBuilder(IReadOnlyList<IVisualElement> coreElements, int softLimit = 16000)
{
    private readonly Dictionary<IVisualElement, int> idMap = [];
    private readonly Dictionary<IVisualElement, double> weightMap = [];
    private readonly Dictionary<IVisualElement, bool> essentialElements = []; // <element, isBuilt>
    private StringBuilder? visualTreeXmlBuilder;
    private int currentSize;

    public string BuildXml(CancellationToken cancellationToken)
    {
        EnsureBuilt(cancellationToken);
        return visualTreeXmlBuilder.ToString();
    }

    public IReadOnlyDictionary<IVisualElement, int> GetIdMap(CancellationToken cancellationToken)
    {
        EnsureBuilt(cancellationToken);
        return idMap;
    }

    [MemberNotNull(nameof(visualTreeXmlBuilder))]
    private void EnsureBuilt(CancellationToken cancellationToken)
    {
        if (visualTreeXmlBuilder != null) return;

#if DEBUG
        Debug.WriteLine("BuildInternal started...");
        var sw = Stopwatch.StartNew();
#endif

        visualTreeXmlBuilder = new StringBuilder();
        currentSize = 0;

        foreach (var coreElement in coreElements)
        {
            essentialElements[coreElement] = false;
        }

        var roots = FindRootElements();

        CalculateWeights();

        foreach (var root in roots)
        {
            BuildElementXml(root, 0, cancellationToken);
            visualTreeXmlBuilder.AppendLine();
        }

        // in case some core elements are not reachable from the roots, build them separately
        foreach (var notBuiltElement in essentialElements.Where(p => !p.Value).Select(p => p.Key))
        {
            BuildElementXml(notBuiltElement, 0, cancellationToken);
            visualTreeXmlBuilder.AppendLine();
        }

        visualTreeXmlBuilder.TrimEnd();

#if DEBUG
        sw.Stop();
        Debug.WriteLine($"BuildInternal finished in {sw.ElapsedMilliseconds}ms");
#endif
    }

    // Find the root elements of all core elements
    private IEnumerable<IVisualElement> FindRootElements()
    {
        if (coreElements.Count == 0) return [];

        // Find common ancestors
        var commonAncestors = FindCommonAncestors();
        if (commonAncestors.Count <= 0)
        {
            // No common ancestors, each core element is an independent root
            return coreElements.Select(e => e.GetAncestors(true).Last());
        }

        // Mark the found common ancestors as essential elements
        foreach (var commonAncestor in commonAncestors)
        {
            essentialElements[commonAncestor] = false;
        }

        return commonAncestors.Select(e => e.GetAncestors(true).Last());
    }

    private List<IVisualElement> FindCommonAncestors()
    {
        if (coreElements.Count <= 1) return [.. coreElements];

        // Get the ancestor path for each core element
        var ancestorPaths = new Dictionary<IVisualElement, HashSet<IVisualElement>>();

        foreach (var element in coreElements)
        {
            var path = new HashSet<IVisualElement>();
            var current = element;

            // Add the element itself and all its ancestors
            while (current != null)
            {
                path.Add(current);
                current = current.Parent;
            }

            ancestorPaths[element] = path;
        }

        // Find all common ancestors
        var commonAncestors = new HashSet<IVisualElement>(ancestorPaths[coreElements[0]]);

        for (var i = 1; i < coreElements.Count; i++)
        {
            commonAncestors.IntersectWith(ancestorPaths[coreElements[i]]);

            if (commonAncestors.Count == 0) return []; // No common ancestors
        }

        // Find the lowest common ancestors (closest to the core elements)
        return FindLowestCommonAncestors(commonAncestors);
    }

    private static List<IVisualElement> FindLowestCommonAncestors(HashSet<IVisualElement> commonAncestors)
    {
        // Check if any child node is also a common ancestor
        return (from ancestor in commonAncestors
            let isLowest = ancestor.Children.All(child => !commonAncestors.Contains(child))
            where isLowest
            select ancestor).ToList();
    }

    private void CalculateWeights()
    {
        weightMap.Clear();

        // Assign the highest weight to core elements
        foreach (var element in coreElements)
        {
            weightMap[element] = 1000;

            // Assign decreasing weights to the parents of core elements
            AssignAncestorWeights(element);

            // Assign decreasing weights to the children of core elements
            AssignDescendantWeights(element);
        }

        // Assign weights to siblings of existing elements
        AssignSiblingWeights();
    }

    private void AssignAncestorWeights(IVisualElement element)
    {
        var current = element.Parent;
        double weight = 900;

        while (current != null)
        {
            // If the ancestor already has a higher weight, keep the higher weight
            if (!weightMap.TryGetValue(current, out var existingWeight) || existingWeight < weight)
            {
                weightMap[current] = weight;
            }

            weight *= 0.9; // Weight decreases with distance
            current = current.Parent;
        }
    }

    private void AssignDescendantWeights(IVisualElement element, double weight = 900)
    {
        foreach (var child in element.Children)
        {
            var childWeight = weight * 0.8;

            // If the child already has a higher weight, keep the higher weight
            if (weightMap.TryGetValue(child, out var existingWeight) && !(existingWeight < childWeight)) continue;

            // Recursively process the children of the child node
            weightMap[child] = childWeight;
            AssignDescendantWeights(child, childWeight);
        }
    }

    private void AssignSiblingWeights()
    {
        foreach (var element in weightMap.Keys.ToReadOnlyList())
        {
            if (element.Parent == null) continue;

            // Assign weights to sibling nodes
            var siblingWeight = weightMap[element];
            foreach (var sibling in element.Parent.Children)
            {
                if (!weightMap.TryGetValue(sibling, out var existingWeight) || existingWeight < siblingWeight)
                {
                    weightMap[sibling] = siblingWeight;
                }
            }
        }
    }

    private bool BuildElementXml(IVisualElement element, int indentLevel, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Skip if over soft limit and not an essential element
        if (currentSize >= softLimit && !essentialElements.ContainsKey(element))
            return false;

        // Calculate the maximum text length
        var maxTextLength = coreElements.Count > 0 ? softLimit / (coreElements.Count * 2) : softLimit / 10;

        var indent = new string(' ', indentLevel * 2);

        // Start tag
        AppendAndCalculateCurrentSize($"{indent}<{element.Type}");

        // Add ID
        var id = idMap.Count;
        idMap[element] = id;
        essentialElements[element] = true;
        AppendAndCalculateCurrentSize($" id=\"{id}\"");

        var isTextElement = element.Type is VisualElementType.Label or VisualElementType.TextEdit or VisualElementType.Document;
        var text = element.GetText();

        if (element.Name is { Length: > 0 } name)
        {
            if (isTextElement && string.IsNullOrEmpty(text))
            {
                AppendAndCalculateCurrentSize($" content=\"{SecurityElement.Escape(TruncateIfNeeded(name, maxTextLength))}\"");
            }
            else if (!isTextElement || name != text)
            {
                // If the element is not a text element, or the name is different from the text, add the name as a description
                // because some text elements' name is the same as text.
                AppendAndCalculateCurrentSize($" description=\"{SecurityElement.Escape(TruncateIfNeeded(name, maxTextLength))}\"");
            }
        }

        var textLines = text?.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Sort child elements by weight
        var childrenByWeight = element.Children
            .OrderByDescending(c => essentialElements.ContainsKey(c))
            .ThenByDescending(c => weightMap.GetValueOrDefault(c, 0))
            .ToList();

        // If there is only a single line of text and no child elements, simplify the output
        if (childrenByWeight.Count == 0)
        {
            if (textLines == null || textLines.Length == 0 || string.IsNullOrEmpty(textLines[0]))
            {
                // No text and no children, self-closing tag
                AppendAndCalculateCurrentSize("/>");
                AppendAndCalculateCurrentSize(Environment.NewLine);
                return true;
            }

            if (textLines is [{ } singleLine] && !string.IsNullOrEmpty(singleLine))
            {
                AppendAndCalculateCurrentSize($" content=\"{SecurityElement.Escape(TruncateIfNeeded(singleLine, maxTextLength))}\"/>");
                AppendAndCalculateCurrentSize(Environment.NewLine);
                return true;
            }
        }

        // Has child elements or multiline text
        AppendAndCalculateCurrentSize(">");
        AppendAndCalculateCurrentSize(Environment.NewLine);

        // Handle multiline text
        if (textLines is { Length: > 0 })
        {
            var textIndent = new string(' ', (indentLevel + 1) * 2);
            foreach (var line in textLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                AppendAndCalculateCurrentSize($"{textIndent}{SecurityElement.Escape(TruncateIfNeeded(line, maxTextLength))}");
                AppendAndCalculateCurrentSize(Environment.NewLine);
            }
        }

        // Handle child elements
        foreach (var child in childrenByWeight.Where(c => currentSize < softLimit || essentialElements.ContainsKey(c)))
        {
            if (!BuildElementXml(child, indentLevel + 1, cancellationToken)) break;
        }

        // End tag
        AppendAndCalculateCurrentSize($"{indent}</{element.Type}>");
        AppendAndCalculateCurrentSize(Environment.NewLine);

        return true;
    }

    private void AppendAndCalculateCurrentSize(string content)
    {
        if (visualTreeXmlBuilder == null) return;
        visualTreeXmlBuilder.Append(content);
        currentSize += content.Length;
    }

    private static string TruncateIfNeeded(string text, int maxLength)
    {
        if (maxLength <= 0 || text.Length <= maxLength)
            return text;

        return text[..Math.Max(0, maxLength - 3)] + "...";
    }
}