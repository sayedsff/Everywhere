// @author https://github.com/DearVa

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Markdig.Syntax;

namespace Everywhere.Markdown;

public partial class MarkdownRenderer
{
    /// <summary>
    /// Utility class to map <see cref="Control"/> to <see cref="BlockNode"/> itself
    /// </summary>
    /// <param name="target"></param>
    private class InlinesProxy(InlineCollection target)
    {
        public int Count => children.Count;

        public InlineNode this[int index]
        {
            get => children[index];
            set
            {
                target[index] = value.Inline;
                children[index] = value;
            }
        }

        private readonly List<InlineNode> children = [];

        public void Add(InlineNode node)
        {
            children.Add(node);
            target.Add(node.Inline);
            Debug.Assert(children.Count == target.Count, "Children count mismatch");
        }

        public void RemoveAt(int index)
        {
            var node = children[index];
            target.Remove(node.Inline);
            children.RemoveAt(index);
            Debug.Assert(children.Count == target.Count, "Children count mismatch");
        }
    }

    /// <summary>
    /// Utility class to map <see cref="Control"/> to <see cref="BlockNode"/> itself
    /// </summary>
    /// <param name="target"></param>
    private class BlocksProxy(Controls target)
    {
        private class MockBlockNode(Control control) : BlockNode
        {
            private const string NotSupportedMessage = "This node is a mock and does not support this operation.";

            public override Control Control => control;

            private Control control = control;

            public void SetControl(Control control) => this.control = control;

            protected override bool IsCompatible(MarkdownObject markdownObject)
            {
                throw new NotSupportedException(NotSupportedMessage);
            }

            protected override bool UpdateCore(
                MarkdownObject markdownObject,
                in ObservableStringBuilderChangedEventArgs change,
                CancellationToken cancellationToken)
            {
                throw new NotSupportedException(NotSupportedMessage);
            }
        }

        public int Count => children.Count;

        public BlockNode this[int index]
        {
            get => children[index];
            set
            {
                target[index] = value.Control;
                children[index] = value;
            }
        }

        private readonly List<BlockNode> children = [];

        public void Add(BlockNode node)
        {
            children.Add(node);
            target.Add(node.Control);
            Debug.Assert(children.Count == target.Count, "Children count mismatch");
        }

        /// <summary>
        /// Adds a control to the target collection for special cases where a mock node is needed.
        /// This should be treated carefully as it adds a mock node.
        /// </summary>
        /// <param name="control"></param>
        public void Add(Control control)
        {
            children.Add(new MockBlockNode(control));
            target.Add(control);
        }

        public void SetControlAt(int index, Control control)
        {
            target[index] = control;
            if (children[index] is MockBlockNode mockBlockNode) mockBlockNode.SetControl(control);
            else children[index] = new MockBlockNode(control);
        }

        public void RemoveAt(int index)
        {
            var node = children[index];
            target.Remove(node.Control);
            children.RemoveAt(index);
            Debug.Assert(children.Count == target.Count, "Children count mismatch");
        }
    }

    [Conditional("DEBUG")]
    private static void PrintMetrics(string metrics, [CallerMemberName] string memberName = "")
    {
        var message = $"[{DateTime.Now:G}] ({memberName}) {metrics}";
        if (Debugger.IsAttached) Debug.WriteLine(message);
        else Console.WriteLine(message);
    }
}