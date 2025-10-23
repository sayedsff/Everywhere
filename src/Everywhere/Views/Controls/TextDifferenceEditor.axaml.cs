using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Chat.Plugins;
using ZLinq;

namespace Everywhere.Views;

public enum TextDifferenceLineKind
{
    /// <summary>
    /// Not changed line.
    /// </summary>
    Context = 0,

    /// <summary>
    /// Added line.
    /// </summary>
    Added = 1,

    /// <summary>
    /// Removed line.
    /// </summary>
    Removed = 2
}

/// <summary>
/// Represents a single line block in a text difference view.
/// </summary>
public sealed class TextDifferenceLine : ObservableObject
{
    public int? LineNumber { get; init; }
    public int? NewLineNumber { get; init; }
    public string Text { get; init; } = string.Empty;
    public TextDifferenceLineKind Kind { get; init; } = TextDifferenceLineKind.Context;
}

/// <summary>
/// A diff block that groups multiple visual lines and carries one TextChange identity.
/// </summary>
public sealed partial class TextDifferenceBlock : ObservableObject
{
    /// <summary>
    /// The underlying TextChange id.
    /// </summary>
    public string? ChangeId { get; init; }

    /// <summary>
    /// Accepted state of the change: null=pending, true=applied, false=rejected.
    /// </summary>
    [ObservableProperty]
    public partial bool? Accepted { get; set; }

    /// <summary>
    /// Grouped visual lines to render.
    /// </summary>
    public AvaloniaList<TextDifferenceLine> Lines { get; } = [];
}

public partial class TextDifferenceEditor : TemplatedControl
{
    public static readonly StyledProperty<TextDifference?> TextDifferenceProperty =
        AvaloniaProperty.Register<TextDifferenceEditor, TextDifference?>(nameof(TextDifference));

    public static readonly StyledProperty<string?> OriginalTextProperty =
        AvaloniaProperty.Register<TextDifferenceEditor, string?>(nameof(OriginalText));

    public static readonly StyledProperty<bool> OnlyAcceptedProperty =
        AvaloniaProperty.Register<TextDifferenceEditor, bool>(nameof(OnlyAccepted));

    public static readonly StyledProperty<bool> IncludePendingProperty =
        AvaloniaProperty.Register<TextDifferenceEditor, bool>(nameof(IncludePending), true);

    public static readonly StyledProperty<bool> ShowLineNumbersProperty =
        AvaloniaProperty.Register<TextDifferenceEditor, bool>(nameof(ShowLineNumbers));

    public static readonly DirectProperty<TextDifferenceEditor, AvaloniaList<TextDifferenceBlock>> BlocksProperty =
        AvaloniaProperty.RegisterDirect<TextDifferenceEditor, AvaloniaList<TextDifferenceBlock>>(
            nameof(Blocks),
            o => o.Blocks);

    public TextDifference? TextDifference
    {
        get => GetValue(TextDifferenceProperty);
        set => SetValue(TextDifferenceProperty, value);
    }

    public string? OriginalText
    {
        get => GetValue(OriginalTextProperty);
        set => SetValue(OriginalTextProperty, value);
    }

    public bool OnlyAccepted
    {
        get => GetValue(OnlyAcceptedProperty);
        set => SetValue(OnlyAcceptedProperty, value);
    }

    public bool IncludePending
    {
        get => GetValue(IncludePendingProperty);
        set => SetValue(IncludePendingProperty, value);
    }

    public bool ShowLineNumbers
    {
        get => GetValue(ShowLineNumbersProperty);
        set => SetValue(ShowLineNumbersProperty, value);
    }

    /// <summary>
    /// Visual blocks to render (each represents one TextChange).
    /// </summary>
    public AvaloniaList<TextDifferenceBlock> Blocks { get; } = [];

    static TextDifferenceEditor()
    {
        TextDifferenceProperty.Changed.AddClassHandler<TextDifferenceEditor>(HandlePropertyChanged);
        OriginalTextProperty.Changed.AddClassHandler<TextDifferenceEditor>(HandlePropertyChanged);
        OnlyAcceptedProperty.Changed.AddClassHandler<TextDifferenceEditor>(HandlePropertyChanged);
        IncludePendingProperty.Changed.AddClassHandler<TextDifferenceEditor>(HandlePropertyChanged);
    }

    private static void HandlePropertyChanged(TextDifferenceEditor sender, AvaloniaPropertyChangedEventArgs args)
        => sender.Rebuild();

    [RelayCommand]
    private void AcceptBlock(string changeId)
    {
        var change = TextDifference?.Changes.FirstOrDefault(c => c.Id == changeId);
        if (change is null) return;
        change.Accepted = true;
        Rebuild();
    }

    [RelayCommand]
    private void DiscardBlock(string changeId)
    {
        var change = TextDifference?.Changes.FirstOrDefault(c => c.Id == changeId);
        if (change is null) return;
        change.Accepted = false;
        Rebuild();
    }

    [RelayCommand]
    private void UndoBlock(string changeId)
    {
        var change = TextDifference?.Changes.FirstOrDefault(c => c.Id == changeId);
        if (change is null) return;
        change.Accepted = null; // back to pending
        Rebuild();
    }

    [RelayCommand]
    private void AcceptAll()
    {
        if (TextDifference is null) return;
        TextDifference.AcceptAll();
        Rebuild();
    }

    [RelayCommand]
    private void DiscardAll()
    {
        if (TextDifference is null) return;
        TextDifference.DiscardAll();
        Rebuild();
    }

    private void Rebuild()
    {
        Blocks.Clear();
        if (TextDifference is null || OriginalText is null) return;

        var opts = new TextDifferenceRenderOptions(OnlyAccepted, IncludePending, 0);
        var changes = TextDifference.GetFilteredChanges(opts)
            .AsValueEnumerable()
            .OrderBy(c => c.Range.Start)
            .ToList();

        // Pre-split original text into lines with absolute starts and content (no line break)
        var originalLines = SplitOriginalIntoLines(OriginalText); // List<(start, endInclBreak, content)>
        var lineIdx = 0; // current line index in original
        var lineNumber = 1; // current line number in original (1-based)
        var newLineNumber = 1; // new document line index (1-based, only accepted changes are applied conceptually)
        var cursor = 0; // current char cursor in original

        foreach (var change in changes)
        {
            // 1) emit context block before this change
            if (change.Range.Start > cursor)
            {
                var ctx = new TextDifferenceBlock();
                while (lineIdx < originalLines.Count &&
                       originalLines[lineIdx].start < change.Range.Start)
                {
                    if (originalLines[lineIdx].start >= cursor)
                    {
                        ctx.Lines.Add(
                            new TextDifferenceLine
                            {
                                LineNumber = lineNumber++,
                                NewLineNumber = newLineNumber++,
                                Kind = TextDifferenceLineKind.Context,
                                Text = originalLines[lineIdx].content
                            });
                    }

                    lineIdx++;
                }

                if (ctx.Lines.Count > 0) Blocks.Add(ctx);
            }

            // 2) emit change block
            var block = new TextDifferenceBlock
            {
                ChangeId = change.Id,
                Accepted = change.Accepted
            };

            var before = change.GetOriginalSlice(OriginalText);
            var after = change.NewText ?? string.Empty;

            switch (change.Accepted)
            {
                case true:
                {
                    // Applied view -> render final content as Context
                    switch (change.Kind)
                    {
                        case TextChangeKind.Insert:
                        case TextChangeKind.Replace:
                        {
                            foreach (var line in SplitLinesNoBreak(after))
                            {
                                block.Lines.Add(
                                    new TextDifferenceLine
                                    {
                                        LineNumber = lineNumber++,
                                        Kind = TextDifferenceLineKind.Context,
                                        Text = line
                                    });
                            }
                            break;
                        }
                        case TextChangeKind.Delete:
                        {
                            // Applied delete -> nothing remains; keep a placeholder for Undo
                            block.Lines.Add(
                                new TextDifferenceLine
                                {
                                    Kind = TextDifferenceLineKind.Context,
                                    Text = string.Empty
                                });

                            // consume original lines covered by this delete
                            while (lineIdx < originalLines.Count && originalLines[lineIdx].start < change.Range.End) lineIdx++;
                            break;
                        }
                    }

                    newLineNumber += TextDifferenceRenderer.CountLines(after);
                    break;
                }
                case false:
                {
                    // Rejected view -> render original content as Context
                    while (lineIdx < originalLines.Count && originalLines[lineIdx].start < change.Range.End)
                    {
                        block.Lines.Add(
                            new TextDifferenceLine
                            {
                                LineNumber = lineNumber++,
                                Kind = TextDifferenceLineKind.Context,
                                Text = originalLines[lineIdx].content
                            });
                        lineIdx++;
                    }

                    if (block.Lines.Count == 0 && change.Kind == TextChangeKind.Insert)
                    {
                        // Rejected insert -> nothing remains; keep a placeholder for Undo
                        block.Lines.Add(
                            new TextDifferenceLine
                            {
                                Kind = TextDifferenceLineKind.Context,
                                Text = string.Empty
                            });
                    }

                    newLineNumber += TextDifferenceRenderer.CountLines(after);
                    break;
                }
                default:
                {
                    // Diff view
                    switch (change.Kind)
                    {
                        case TextChangeKind.Delete:
                        {
                            // consume removed lines from original
                            while (lineIdx < originalLines.Count && originalLines[lineIdx].start < change.Range.End)
                            {
                                block.Lines.Add(
                                    new TextDifferenceLine
                                    {
                                        LineNumber = lineNumber++,
                                        Kind = TextDifferenceLineKind.Removed,
                                        Text = originalLines[lineIdx].content
                                    });
                                lineIdx++;
                            }
                            break;
                        }

                        case TextChangeKind.Insert:
                        {
                            foreach (var line in SplitLinesNoBreak(after))
                            {
                                block.Lines.Add(
                                    new TextDifferenceLine
                                    {
                                        NewLineNumber = newLineNumber++,
                                        Kind = TextDifferenceLineKind.Added,
                                        Text = line
                                    });
                            }
                            // insertion does not consume original lines
                            break;
                        }

                        case TextChangeKind.Replace:
                        {
                            // Remember where this change starts in original
                            var startLineIdx = lineIdx;

                            var a = SplitLinesNoBreak(before);
                            var b = SplitLinesNoBreak(after);

                            foreach (var (kind, line, _) in MergeReplaceLinesWithIndex(a, b))
                            {
                                if (kind == TextDifferenceLineKind.Added)
                                {
                                    block.Lines.Add(
                                        new TextDifferenceLine
                                        {
                                            NewLineNumber = newLineNumber++,
                                            Kind = TextDifferenceLineKind.Added,
                                            Text = line
                                        });
                                }
                                else
                                {
                                    // Context/Removed lines originate from original "a"
                                    block.Lines.Add(
                                        new TextDifferenceLine
                                        {
                                            LineNumber = lineNumber++,
                                            NewLineNumber = kind == TextDifferenceLineKind.Removed ? null : newLineNumber++,
                                            Kind = kind,
                                            Text = line
                                        });
                                }
                            }

                            // consume the original "a" lines
                            lineIdx = startLineIdx + a.Count;
                            break;
                        }
                    }
                    break;
                }
            }

            Blocks.Add(block);
            cursor = change.Range.End;
        }

        // 3) trailing context
        if (lineIdx < originalLines.Count)
        {
            var tail = new TextDifferenceBlock();
            while (lineIdx < originalLines.Count)
            {
                tail.Lines.Add(
                    new TextDifferenceLine
                    {
                        LineNumber = lineNumber++,
                        Kind = TextDifferenceLineKind.Context,
                        Text = originalLines[lineIdx].content
                    });
                lineIdx++;
            }
            if (tail.Lines.Count > 0) Blocks.Add(tail);
        }

        // --- helpers ---

        // Split original into lines, preserving absolute start index and content text (no line break)
        static IReadOnlyList<(int start, int end, string content)> SplitOriginalIntoLines(string s)
        {
            var list = new List<(int start, int end, string content)>();
            int i = 0, start = 0;
            while (i < s.Length)
            {
                if (s[i] == '\r' || s[i] == '\n')
                {
                    var nlLen = s[i] == '\r' && i + 1 < s.Length && s[i + 1] == '\n' ? 2 : 1;
                    var contentLen = i - start;
                    list.Add((start, i + nlLen, contentLen > 0 ? s.Substring(start, contentLen) : string.Empty));
                    i += nlLen;
                    start = i;
                }
                else i++;
            }

            if (start <= s.Length - 1)
            {
                // last line without trailing break
                var contentLen = s.Length - start;
                list.Add((start, s.Length, contentLen > 0 ? s.Substring(start, contentLen) : string.Empty));
            }

            if (list.Count == 0)
            {
                // empty file -> single empty line
                list.Add((0, 0, string.Empty));
            }

            return list;
        }

        // Split by line breaks, no CR/LF in result
        static IReadOnlyList<string> SplitLinesNoBreak(string s)
        {
            if (string.IsNullOrEmpty(s)) return [];
            var list = new List<string>();
            int i = 0, start = 0;
            while (i < s.Length)
            {
                if (s[i] == '\r' || s[i] == '\n')
                {
                    list.Add(s.Substring(start, i - start));
                    i += (s[i] == '\r' && i + 1 < s.Length && s[i + 1] == '\n') ? 2 : 1;
                    start = i;
                }
                else i++;
            }
            if (start < s.Length) list.Add(s[start..]);
            if (list.Count == 0) list.Add(string.Empty);
            return list;
        }

        // LCS merge with A-index: equal->Context (from A), otherwise Removed/Added
        static IEnumerable<(TextDifferenceLineKind kind, string line, int aIndex)> MergeReplaceLinesWithIndex(
            IReadOnlyList<string> a,
            IReadOnlyList<string> b)
        {
            int n = a.Count, m = b.Count;
            var dp = new int[n + 1, m + 1];
            for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

            int ii = 0, jj = 0;
            while (ii < n || jj < m)
            {
                if (ii < n && jj < m && a[ii] == b[jj])
                {
                    yield return (TextDifferenceLineKind.Context, a[ii], ii);
                    ii++;
                    jj++;
                }
                else if (jj < m && (ii == n || dp[ii, jj + 1] >= dp[ii + 1, jj]))
                {
                    yield return (TextDifferenceLineKind.Added, b[jj], ii);
                    jj++;
                }
                else if (ii < n)
                {
                    yield return (TextDifferenceLineKind.Removed, a[ii], ii);
                    ii++;
                }
            }
        }
    }
}