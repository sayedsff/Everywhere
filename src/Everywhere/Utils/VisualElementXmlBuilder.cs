using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Text;

namespace Everywhere.Utils;

public class VisualElementXmlBuilder(IVisualElement rootElement)
{
    private readonly Dictionary<IVisualElement, int> idMap = [];
    private StringBuilder? visualTreeXmlBuilder;

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
        BuildInternal(rootElement, 0);
        visualTreeXmlBuilder.Remove(visualTreeXmlBuilder.Length - 1, 1);

#if DEBUG
        sw.Stop();
        Debug.WriteLine($"BuildInternal finished in {sw.ElapsedMilliseconds}ms");
#endif

        void BuildInternal(IVisualElement currentElement, int indentLevel)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var indent = new string(' ', indentLevel * 2);
            visualTreeXmlBuilder.Append(indent);
            visualTreeXmlBuilder.Append('<').Append(currentElement.Type);

            var id = idMap.Count;
            idMap.Add(currentElement, id);
            visualTreeXmlBuilder.Append(" id=\"").Append(id).Append('"');

            if (currentElement.Name is { } name && !string.IsNullOrWhiteSpace(name))
            {
                visualTreeXmlBuilder.Append(" name=\"").Append(SecurityElement.Escape(name)).Append('"');
            }

            var textLines = currentElement.GetText()?.Split(Environment.NewLine);

            using var childrenEnumerator = currentElement.Children.GetEnumerator();
            if (textLines is [{ } text] && !string.IsNullOrWhiteSpace(text))
            {
                visualTreeXmlBuilder.Append(" text=\"").Append(SecurityElement.Escape(text)).Append('"');
            }

            if (!childrenEnumerator.MoveNext())
            {
                // If the element has no children, we can omit the text node in a single line.
                visualTreeXmlBuilder.AppendLine("/>");
            }
            else
            {
                visualTreeXmlBuilder.AppendLine(">");
                if (textLines is { Length: > 1 })
                {
                    var textLineIndent = new string(' ', indentLevel * 2 + 2);
                    foreach (var textLine in textLines)
                    {
                        if (string.IsNullOrWhiteSpace(textLine)) continue;
                        visualTreeXmlBuilder.Append(textLineIndent).AppendLine(SecurityElement.Escape(textLine));
                    }
                }

                do
                {
                    BuildInternal(childrenEnumerator.Current, indentLevel + 1);
                }
                while (childrenEnumerator.MoveNext());
                visualTreeXmlBuilder.Append(indent).Append("</").Append(currentElement.Type).AppendLine(">");
            }
        }
    }
}