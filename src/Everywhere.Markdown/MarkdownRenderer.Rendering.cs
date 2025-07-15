// @author https://github.com/DearVa

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using ColorCode;
using ColorCode.Styling;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ZLinq;
using AvaloniaDocs = Avalonia.Controls.Documents;

namespace Everywhere.Markdown;

public partial class MarkdownRenderer
{
    private abstract class MarkdownNode
    {
        protected delegate void SelectedTextBlockUpdatedHandler(SelectableTextBlock selectableTextBlock, bool deleted);

        protected static event SelectedTextBlockUpdatedHandler? SelectedTextBlockUpdated;

        protected virtual SelectableTextBlock? SelectableTextBlock => null;

        /// <summary>
        /// records the source span of the block in the Markdown document.
        /// </summary>
        private SourceSpan span;

        public bool IsDirty(MarkdownObject markdownObject, in ObservableStringBuilderChangedEventArgs change)
        {
            return !span.Equals(markdownObject.Span) || span.End >= change.StartIndex && change.StartIndex + change.Length > span.Start;
        }

        public bool Update(MarkdownObject markdownObject, in ObservableStringBuilderChangedEventArgs change, CancellationToken cancellationToken)
        {
            if (!IsDirty(markdownObject, change))
            {
                // No need to update, the change does not affect this node
                return true;
            }

            var result = IsCompatible(markdownObject) && UpdateCore(markdownObject, change, cancellationToken);
            PrintMetrics(markdownObject.GetType().Name);
            span = markdownObject.Span;
            if (SelectableTextBlock is { } selectableTextBlock) SelectedTextBlockUpdated?.Invoke(selectableTextBlock, !result);
            return result;
        }

        protected abstract bool IsCompatible(MarkdownObject markdownObject);

        /// <summary>
        /// Updates the block with the given inlines and change information.
        /// </summary>
        /// <param name="markdownObject"></param>
        /// <param name="change"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>true if the block was updated successfully, false if it needs to be removed</returns>
        protected abstract bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken);
    }

    private abstract class InlineNode : MarkdownNode
    {
        public abstract AvaloniaDocs.Inline Inline { get; }

        public Classes Classes => Inline.Classes;

        protected static InlineNode? CreateInlineNode(
            Inline inline,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            InlineNode? node = inline switch
            {
                LiteralInline => new FuncInlineNode<LiteralInline, AvaloniaDocs.Run>((literal, avaloniaInline) =>
                {
                    avaloniaInline.Text = literal.Content.ToString();
                    return true;
                })
                {
                    Classes = { "Literal" }
                },
                LineBreakInline => new FuncInlineNode<LineBreakInline, AvaloniaDocs.LineBreak>((_, _) => true),
                AutolinkInline => new FuncInlineNode<AutolinkInline, InlineHyperlink>((autolink, avaloniaInline) =>
                {
                    Uri.TryCreate(autolink.Url, UriKind.RelativeOrAbsolute, out var uri);
                    avaloniaInline.HRef = uri;

                    if (avaloniaInline.Content.Inlines is [AvaloniaDocs.Run run]) run.Text = autolink.Url;
                    else avaloniaInline.Content.Inlines.Add(new AvaloniaDocs.Run(autolink.Url));

                    return true;
                })
                {
                    Classes = { "Autolink" }
                },
                DelimiterInline => new FuncInlineNode<DelimiterInline, AvaloniaDocs.Run>((delimiter, avaloniaInline) =>
                {
                    avaloniaInline.Text = delimiter.ToLiteral();
                    return true;
                })
                {
                    Classes = { "Delimiter" }
                },
                TaskList => new FuncInlineNode<TaskList, AvaloniaDocs.InlineUIContainer>((taskList, avaloniaInline) =>
                {
                    if (avaloniaInline.Child is not CheckBox checkBox)
                    {
                        avaloniaInline.Child = checkBox = new CheckBox
                        {
                            Classes = { "TaskList" },
                            IsEnabled = false
                        };
                    }
                    checkBox.IsChecked = taskList.Checked;
                    return true;
                }),
                HtmlEntityInline => new FuncInlineNode<HtmlEntityInline, AvaloniaDocs.Run>((htmlEntity, avaloniaInline) =>
                {
                    avaloniaInline.Text = htmlEntity.Transcoded.ToString();
                    return true;
                })
                {
                    Classes = { "HtmlEntity" }
                },
                HtmlInline => new FuncInlineNode<HtmlInline, AvaloniaDocs.Run>((_, _) => true), // TODO: Implement HTML rendering
                CodeInline => new CodeInlineNode(),
                LinkInline => new LinkInlineNode(),
                EmphasisInline => new EmphasisInlineNode(),
                ContainerInline => new ContainerInlineNode(),
                _ => null
            };
            node?.Update(inline, change, cancellationToken);
            return node;
        }
    }

    private class FuncInlineNode<TInline, TAvaloniaInline>(Func<TInline, TAvaloniaInline, bool> updater) : InlineNode
        where TInline : Inline
        where TAvaloniaInline : AvaloniaDocs.Inline, new()
    {
        public override AvaloniaDocs.Inline Inline => inline;

        private readonly TAvaloniaInline inline = new();

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is TInline;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return updater((TInline)markdownObject, inline);
        }
    }

    private class InlinesNode : InlineNode
    {
        public override AvaloniaDocs.Inline Inline { get; }

        public AvaloniaDocs.InlineCollection Inlines { get; }

        private readonly InlinesProxy proxy;

        public InlinesNode(AvaloniaDocs.Span span) : this(span, span.Inlines) { }

        public InlinesNode(InlineHyperlink inlineHyperlink) : this(inlineHyperlink, inlineHyperlink.Content.Inlines) { }

        private InlinesNode(AvaloniaDocs.Inline inline, AvaloniaDocs.InlineCollection inlines)
        {
            Inline = inline;
            Inlines = inlines;
            proxy = new InlinesProxy(inlines);
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is IEnumerable<Inline>;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var i = -1;
            foreach (var inline in (IEnumerable<Inline>)markdownObject)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Add new inline
                if (proxy.Count > ++i)
                {
                    // Update existing inline
                    var oldInlineNode = proxy[i];

                    // if Update returned true, it means the block was updated successfully
                    if (oldInlineNode.Update(inline, change, cancellationToken)) continue;

                    // else, remove the old node and create a new one
                    if (CreateInlineNode(inline, change, cancellationToken) is not { } newInlineNode)
                    {
                        proxy.RemoveAt(i);
                        continue;
                    }

                    proxy[i] = newInlineNode;
                }
                else
                {
                    if (CreateInlineNode(inline, change, cancellationToken) is not { } newInlineNode) continue;
                    proxy.Add(newInlineNode);
                }
            }

            while (proxy.Count > i + 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                proxy.RemoveAt(proxy.Count - 1);
            }

            return i >= 0; // Return true if at least one inline was processed
        }
    }

    private class ContainerInlineNode() : InlinesNode(new AvaloniaDocs.Span())
    {
        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is ContainerInline;
        }
    }

    private class CodeInlineNode : InlineNode
    {
        protected override SelectableTextBlock SelectableTextBlock => selectableTextBlock;

        public override AvaloniaDocs.Inline Inline => inlineUIContainer;

        private readonly AvaloniaDocs.InlineUIContainer inlineUIContainer;
        private readonly SelectableTextBlock selectableTextBlock;

        public CodeInlineNode()
        {
            inlineUIContainer = new AvaloniaDocs.InlineUIContainer
            {
                Classes = { "Code" },
                Child = new Border
                {
                    Classes = { "Code" },
                    Child = selectableTextBlock = new SelectableTextBlock
                    {
                        Classes = { "Code" }
                    }
                }
            };
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is CodeInline;
        }

        protected override bool UpdateCore(MarkdownObject markdownObject, in ObservableStringBuilderChangedEventArgs change, CancellationToken cancellationToken)
        {
            var code = Unsafe.As<CodeInline>(markdownObject);
            selectableTextBlock.Text = code.Content;
            return true;
        }
    }

    private class LinkInlineNode() : InlinesNode(new InlineHyperlink())
    {
        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is LinkInline;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var linkInline = Unsafe.As<LinkInline>(markdownObject);
            if (linkInline.Url == null) return false;

            Uri.TryCreate(linkInline.Url, UriKind.Absolute, out var uri);

            if (linkInline.IsImage && uri is not null)
            {
                // img.DoubleTapped += (_, _) =>
                // {
                //     Console.WriteLine(uri?.ToString());
                // };

                Image img;
                if (Inlines is [AvaloniaDocs.InlineUIContainer inlineUIContainer])
                {
                    if (inlineUIContainer.Child is Image image)
                    {
                        img = image;
                    }
                    else
                    {
                        inlineUIContainer.Child = img = CreateImage();
                    }
                }
                else
                {
                    Inlines.Clear();
                    Inlines.Add(new AvaloniaDocs.InlineUIContainer(img = CreateImage()));
                }
                // ImageLoader.SetSource(img, linkInline.Url);

                return true;

                Image CreateImage() => new()
                {
                    Classes = { "Link" },
                };
            }

            var inlineHyperlink = (InlineHyperlink)Inline;
            inlineHyperlink.HRef = uri;

            if (linkInline.Label != null)
            {
                if (Inlines is [AvaloniaDocs.Run run])
                {
                    run.Text = linkInline.Label;
                }
                else
                {
                    Inlines.Clear();
                    Inlines.Add(new AvaloniaDocs.Run(linkInline.Label));
                }
            }
            else
            {
                base.UpdateCore(markdownObject, change, cancellationToken);
            }

            return true;
        }
    }

    private class EmphasisInlineNode : ContainerInlineNode
    {
        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is EmphasisInline;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var emphasisInline = Unsafe.As<EmphasisInline>(markdownObject);
            var span = (AvaloniaDocs.Span)Inline;
            switch (emphasisInline.DelimiterChar)
            {
                case '*' when emphasisInline.DelimiterCount == 2: // bold
                case '_' when emphasisInline.DelimiterCount == 2: // bold
                    span.FontWeight = FontWeight.Bold;
                    break;
                case '*': // italic
                case '_': // italic
                    span.FontStyle = FontStyle.Italic;
                    break;
                case '~': // 2x strike through, 1x subscript
                    if (emphasisInline.DelimiterCount == 2)
                        span.TextDecorations = TextDecorations.Strikethrough;
                    else
                        span.BaselineAlignment = BaselineAlignment.Subscript;
                    break;
                case '^': // 1x superscript
                    span.BaselineAlignment = BaselineAlignment.Superscript;
                    break;
                case '+': // 2x underline
                    span.TextDecorations = TextDecorations.Underline;
                    break;
                case '=': // 2x Marked
                    // TODO: Implement Marked
                    break;
            }

            return base.UpdateCore(markdownObject, in change, cancellationToken);
        }
    }

    private abstract class BlockNode : MarkdownNode
    {
        public abstract Control Control { get; }

        public Classes Classes => Control.Classes;

        protected static BlockNode? CreateBlockNode(
            Block block,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            BlockNode? node = block switch
            {
                Table => new TableNode(),
                TableCell => new TableCellNode(),
                ListBlock => new ListBlockNode(),
                CodeBlock => new CodeBlockNode(),
                QuoteBlock => new QuoteBlockNode(),
                HeadingBlock => new HeadingBlockNode(),
                ParagraphBlock => new ParagraphBlockNode(),
                ContainerBlock => new ContainerBlockNode(),
                HtmlBlock => new HtmlBlockNode(),
                _ => null
            };
            node?.Update(block, change, cancellationToken);
            return node;
        }
    }

    /// <summary>
    /// Works as <see cref="SelectableTextBlock"/>
    /// </summary>
    private class InlineCollectionNode : BlockNode
    {
        protected override SelectableTextBlock SelectableTextBlock => selectableTextBlock;

        public override Control Control => selectableTextBlock;

        private readonly InlinesNode inlinesNode;
        private readonly SelectableTextBlock selectableTextBlock;

        public InlineCollectionNode()
        {
            inlinesNode = new InlinesNode(new AvaloniaDocs.Span());
            selectableTextBlock = new SelectableTextBlock
            {
                Classes = { "InlineCollection" },
                Inlines = inlinesNode.Inlines
            };
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is IEnumerable<Inline>;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            return inlinesNode.Update(
                markdownObject,
                change,
                cancellationToken);
        }
    }

    private class TableNode : BlockNode
    {
        public override Control Control { get; }

        private readonly Grid container;
        private readonly BlocksProxy proxy;

        public TableNode()
        {
            container = new Grid();
            proxy = new BlocksProxy(container.Children);
            Control = new ScrollViewer
            {
                Classes = { "Table" },
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new Border
                {
                    Classes = { "Table" },
                    Child = container
                }
            };
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is Table;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var table = Unsafe.As<Table>(markdownObject);
            if (table.ColumnDefinitions.Count == 0) return false;

            while (table.ColumnDefinitions.Count < container.ColumnDefinitions.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                container.ColumnDefinitions.RemoveAt(container.ColumnDefinitions.Count - 1);
            }
            while (table.ColumnDefinitions.Count > container.ColumnDefinitions.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            var rowIndex = 0;
            foreach (var row in table.AsValueEnumerable().OfType<TableRow>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (rowIndex >= container.RowDefinitions.Count)
                {
                    container.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                foreach (var (cell, columnIndex) in row.AsValueEnumerable().OfType<TableCell>().Select((c, i) => (c, i)))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Control? cellControl = null;
                    do
                    {
                        var cellIndex = rowIndex * table.ColumnDefinitions.Count + columnIndex;
                        if (proxy.Count > cellIndex)
                        {
                            // existing item block node, update it
                            var oldCellBlockNode = proxy[cellIndex];
                            cellControl = oldCellBlockNode.Control;

                            // if Update returned true; it means the block was updated successfully
                            if (oldCellBlockNode.Update(cell, change, cancellationToken)) break;

                            // else, remove the old node and create a new one
                            if (CreateBlockNode(cell, change, cancellationToken) is not { } newCellBlockNode)
                            {
                                proxy.RemoveAt(cellIndex);
                                break;
                            }

                            proxy[cellIndex] = newCellBlockNode;
                            cellControl = newCellBlockNode.Control;
                        }
                        else
                        {
                            if (CreateBlockNode(cell, change, cancellationToken) is not { } newCellBlockNode) break;
                            proxy.Add(newCellBlockNode);
                            cellControl = newCellBlockNode.Control;
                        }
                    }
                    while (false);

                    if (cellControl is null) continue;

                    Grid.SetRow(cellControl, rowIndex);
                    Grid.SetColumn(cellControl, columnIndex);

                    if (row.IsHeader && cellControl.Classes.Count < 2) cellControl.Classes.Add("Header");
                    else if (!row.IsHeader && cellControl.Classes.Count > 1) cellControl.Classes.Remove("Header");

                    if (columnIndex >= table.ColumnDefinitions.Count) continue;
                    if (cellControl is not Border { Child: { } child }) continue;
                    var columnDefinition = table.ColumnDefinitions[columnIndex];
                    child.HorizontalAlignment = columnDefinition.Alignment switch
                    {
                        TableColumnAlign.Left => HorizontalAlignment.Left,
                        TableColumnAlign.Center => HorizontalAlignment.Center,
                        TableColumnAlign.Right => HorizontalAlignment.Right,
                        _ => HorizontalAlignment.Stretch
                    };
                }

                rowIndex++;
            }

            var columnCount = table.ColumnDefinitions.Count;
            var cellCount = rowIndex * columnCount;
            while (proxy.Count > cellCount)
            {
                cancellationToken.ThrowIfCancellationRequested();
                proxy.RemoveAt(proxy.Count - 1);
            }

            if (rowIndex == 0 || columnCount == 0)
            {
                return false;
            }

            while (rowIndex < container.RowDefinitions.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                container.RowDefinitions.RemoveAt(container.RowDefinitions.Count - 1);
            }

            return true;
        }
    }

    private class TableCellNode : ContainerBlockNode
    {
        public TableCellNode()
        {
            Classes[0] = "TableCell";
            control = new Border
            {
                Classes = { "TableCell" },
                Child = base.Control
            };
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is TableCell;
        }
    }

    private class ListBlockNode : BlockNode
    {
        public override Control Control => grid;

        private readonly Grid grid;
        private readonly BlocksProxy proxy;

        public ListBlockNode()
        {
            grid = new Grid
            {
                Classes = { "ListBlock" },
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition()
                }
            };
            proxy = new BlocksProxy(grid.Children);
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is ListBlock;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var listBlock = Unsafe.As<ListBlock>(markdownObject);
            if (listBlock.Count == 0) return false;

            var number = 1;
            for (var i = 0; i < listBlock.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (grid.RowDefinitions.Count <= i)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                var itemBlock = listBlock[i];

                // number part
                var numberIndex = i * 2;
                if (listBlock.IsOrdered)
                {
                    if (proxy.Count > numberIndex && proxy[numberIndex].Control is TextBlock existingNumberControl)
                    {
                        // existing number block node, update it
                        existingNumberControl.Text = $"{number++}.";
                    }
                    else
                    {
                        // create a new number block node
                        var numberControl = new TextBlock
                        {
                            Classes = { "ListBlockNumber" },
                            Text = $"{number++}."
                        };
                        if (proxy.Count > numberIndex)
                        {
                            // replace the existing number block node
                            proxy.SetControlAt(numberIndex, numberControl);
                        }
                        else
                        {
                            // add a new number block node
                            proxy.Add(numberControl);
                            Grid.SetRow(numberControl, i);
                        }
                    }
                }
                else
                {
                    if (proxy.Count > numberIndex)
                    {
                        // existing bullet block node, update it
                        proxy[numberIndex].Control.Classes[1] = GetBulletClass();
                    }
                    else
                    {
                        // create a new bullet block node
                        var bulletIcon = new Border
                        {
                            Classes =
                            {
                                "ListBlockBullet",
                                GetBulletClass()
                            }
                        };
                        proxy.Add(bulletIcon);
                        Grid.SetRow(bulletIcon, i);
                    }

                    string GetBulletClass() => "Level" + (listBlock.Column / 2) % 4;
                }

                // item part
                var itemIndex = i * 2 + 1;
                if (proxy.Count > itemIndex)
                {
                    // existing item block node, update it
                    var oldItemBlockNode = proxy[itemIndex];

                    // if Update returned true, it means the block was updated successfully
                    if (oldItemBlockNode.Update(itemBlock, change, cancellationToken)) continue;

                    // else, remove the old node and create a new one
                    if (CreateBlockNode(itemBlock, change, cancellationToken) is not { } newItemBlockNode)
                    {
                        proxy.RemoveAt(itemIndex);
                        continue;
                    }

                    proxy[itemIndex] = newItemBlockNode;
                }
                else
                {
                    if (CreateBlockNode(itemBlock, change, cancellationToken) is not { } newItemBlockNode) continue;
                    proxy.Add(newItemBlockNode);
                    Grid.SetRow(newItemBlockNode.Control, i);
                    Grid.SetColumn(newItemBlockNode.Control, 1);
                }
            }

            while (proxy.Count > listBlock.Count * 2)
            {
                cancellationToken.ThrowIfCancellationRequested();
                proxy.RemoveAt(proxy.Count - 1);
            }

            while (grid.RowDefinitions.Count > listBlock.Count)
            {
                cancellationToken.ThrowIfCancellationRequested();
                grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count - 1);
            }

            return true;
        }
    }

    private class CodeBlockNode : BlockNode
    {
        protected override SelectableTextBlock SelectableTextBlock => codeTextBlock;

        public override Control Control { get; }

        private SyntaxHighlighting? syntaxHighlighting;

        private readonly SelectableTextBlock codeTextBlock;

        public CodeBlockNode()
        {
            var codeContainer = new Border
            {
                Classes = { "CodeBlock" }
            };
            Control = codeContainer;

            var codeContainerGrid = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(), new ColumnDefinition(GridLength.Auto)],
                Classes = { "CodeBlock" }
            };
            codeContainer.Child = codeContainerGrid;

            codeTextBlock = new SelectableTextBlock
            {
                Classes = { "CodeBlock" },
            };
            codeContainerGrid.Children.Add(
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = codeTextBlock
                });

            var copyButton = new Button
            {
                Classes = { "CodeBlock", "Ghost" }
            };
            copyButton.Click += delegate
            {
                if (TopLevel.GetTopLevel(codeTextBlock) is not { Clipboard: { } clipboard }) return;
                clipboard.SetTextAsync(codeTextBlock.Inlines?.Text);
            };
            codeContainerGrid.Children.Add(copyButton);
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is CodeBlock;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var codeBlock = Unsafe.As<CodeBlock>(markdownObject);
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (codeBlock.Lines.Lines is null) return false;

            var inlines = codeTextBlock.Inlines ?? throw new InvalidOperationException("This should never happen");
            foreach (var (slice, i) in codeBlock.Lines.Lines.AsValueEnumerable().Take(codeBlock.Lines.Count).Select((l, i) => (l.Slice, i * 2)))
            {
                // Skip if the slice is completely outside the change range
                if (inlines.Count > i &&
                    (slice.End < change.StartIndex || change.StartIndex + change.Length <= slice.Start)) continue;

                if (inlines.Count <= i)
                {
                    inlines.Add(new AvaloniaDocs.Run(slice.ToString()));
                }
                else if (inlines[i] is AvaloniaDocs.Run run)
                {
                    // Update existing run
                    run.Text = slice.ToString();
                }
                else
                {
                    // Replace it with a new run if it's not a Run
                    inlines[i] = new AvaloniaDocs.Run(slice.ToString());
                }

                if (i / 2 < codeBlock.Lines.Count - 1)
                {
                    // Add a line break after each line except the last one
                    if (inlines.Count <= i + 1)
                    {
                        inlines.Add(new AvaloniaDocs.LineBreak());
                    }
                    else if (inlines[i + 1] is not AvaloniaDocs.LineBreak)
                    {
                        // Replace it with a LineBreak if it's not a LineBreak
                        inlines[i + 1] = new AvaloniaDocs.LineBreak();
                    }
                }
            }
            while (inlines.Count > codeBlock.Lines.Count * 2 - 1)
            {
                // Remove excess inlines
                inlines.RemoveAt(inlines.Count - 1);
            }

            // Highlighting only works for closed FencedCodeBlock with Info
            if (codeBlock is not FencedCodeBlock { IsOpen: false, Info.Length: > 0 } fencedCodeBlock) return true;

            // FencedCodeBlock with Info, use syntax highlighting
            var language = Languages.FindById(fencedCodeBlock.Info);
            if (language is null) return true;

            inlines.Clear();
            syntaxHighlighting ??= new SyntaxHighlighting(inlines, StyleDictionary.DefaultDark);
            syntaxHighlighting.FormatInlines(fencedCodeBlock.Lines.ToString(), language);
            return true;
        }
    }

    private class QuoteBlockNode : ContainerBlockNode
    {
        public QuoteBlockNode()
        {
            Classes[0] = "QuoteBlock";
            control = new Border
            {
                Classes = { "QuoteBlock" },
                Child = base.Control
            };
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is QuoteBlock;
        }
    }

    private class HeadingBlockNode : BlockNode
    {
        public override Control Control { get; }

        private readonly InlineCollectionNode headingText;

        public HeadingBlockNode()
        {
            headingText = new InlineCollectionNode();
            Control = new Border
            {
                Classes = { "HeadingBlock" },
                Child = headingText.Control
            };
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is HeadingBlock;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var headingBlock = Unsafe.As<HeadingBlock>(markdownObject);
            if (headingBlock.Inline is null) return false;

            if (!headingText.Update(headingBlock.Inline, change, cancellationToken)) return false;

            headingText.Classes[0] = "Heading" + headingBlock.Level;
            return true;
        }
    }

    private class ParagraphBlockNode : InlineCollectionNode
    {
        public ParagraphBlockNode()
        {
            Classes[0] = "ParagraphBlock";
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is ParagraphBlock;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var paragraphBlock = Unsafe.As<ParagraphBlock>(markdownObject);
            return paragraphBlock.Inline is not null && base.UpdateCore(paragraphBlock.Inline, change, cancellationToken);
        }
    }

    private class ContainerBlockNode : BlockNode
    {
        public override Control Control => control;

        protected Control control;

        private readonly BlocksProxy proxy;

        public ContainerBlockNode()
        {
            var container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Classes = { "ContainerBlock" }
            };
            control = container;
            proxy = new BlocksProxy(container.Children);
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is ContainerBlock;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            var containerBlock = Unsafe.As<ContainerBlock>(markdownObject);
            if (containerBlock.Count == 0) return false;

            var i = 0;
            for (; i < containerBlock.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var block = containerBlock[i];

                if (i < proxy.Count)
                {
                    var oldNode = proxy[i];
                    if (!oldNode.IsDirty(block, change)) continue;
                    if (oldNode.Update(block, change, cancellationToken)) continue;

                    // if Update returned false, it means the block needs to be removed
                    var newNode = CreateBlockNode(block, change, cancellationToken);
                    if (newNode is not null) proxy[i] = newNode;
                    else proxy.RemoveAt(i);
                }
                else
                {
                    var newNode = CreateBlockNode(block, change, cancellationToken);
                    if (newNode is not null) proxy.Add(newNode);
                }
            }

            for (var j = proxy.Count - 1; j >= i; j--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                proxy.RemoveAt(j);
            }

            return true;
        }
    }

    private class HtmlBlockNode : BlockNode
    {
        public override Control Control { get; } = new(); // TODO: Implement HTML rendering

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is HtmlBlock;
        }

        protected override bool UpdateCore(
            MarkdownObject markdownObject,
            in ObservableStringBuilderChangedEventArgs change,
            CancellationToken cancellationToken)
        {
            return true;
        }
    }

    private class DocumentNode : ContainerBlockNode
    {
        public IReadOnlyDictionary<SelectableTextBlock, Rect> SelectableTextBlockBounds => selectableTextBlockBounds;

        private readonly Dictionary<SelectableTextBlock, Rect> selectableTextBlockBounds = new();

        public DocumentNode()
        {
            Classes[0] = "MarkdownDocument";
            SelectedTextBlockUpdated += HandleSelectedTextBlockUpdated;
        }

        ~DocumentNode()
        {
            SelectedTextBlockUpdated -= HandleSelectedTextBlockUpdated;
        }

        private void HandleSelectedTextBlockUpdated(SelectableTextBlock selectableTextBlock, bool deleted)
        {
            if (deleted) selectableTextBlockBounds.Remove(selectableTextBlock);
            else
            {
                var bounds = selectableTextBlock.Bounds;
                var position = selectableTextBlock.TranslatePoint(new Point(bounds.X, bounds.Y), Control) ?? new Point();
                selectableTextBlockBounds[selectableTextBlock] = new Rect(position, bounds.Size);
            }
        }

        protected override bool IsCompatible(MarkdownObject markdownObject)
        {
            return markdownObject is MarkdownDocument;
        }
    }
}