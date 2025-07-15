// @author https://github.com/DearVa
// @author https://github.com/AuroraZiling
// @author https://github.com/SlimeNull

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Markdig;

namespace Everywhere.Markdown;

public partial class MarkdownRenderer : Control
{
    public static readonly DirectProperty<MarkdownRenderer, ObservableStringBuilder?> MarkdownBuilderProperty =
        AvaloniaProperty.RegisterDirect<MarkdownRenderer, ObservableStringBuilder?>(
            nameof(MarkdownBuilder),
            o => o.MarkdownBuilder,
            (o, v) => o.MarkdownBuilder = v);

    public ObservableStringBuilder? MarkdownBuilder
    {
        get;
        set
        {
            var oldValue = field;
            if (!SetAndRaise(MarkdownBuilderProperty, ref field, value)) return;

            if (oldValue is not null) oldValue.Changed -= CommitChange;
            if (value is not null)
            {
                value.Changed += CommitChange;
                CommitChange(new ObservableStringBuilderChangedEventArgs(value.ToString(), 0, value.Length));
            }
        }
    }

    private ObservableStringBuilderChangedEventArgs? pendingChange;

    private readonly DocumentNode documentNode = new();
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseCodeBlockSpanFixer()
        .Build();

    public MarkdownRenderer()
    {
        LogicalChildren.Add(documentNode.Control);
        VisualChildren.Add(documentNode.Control);

        //AddHandler(PointerPressedEvent, HandlePointerPressed, RoutingStrategies.Tunnel);
        //AddHandler(PointerMovedEvent, HandlePointerMoved, RoutingStrategies.Tunnel);
        //AddHandler(PointerReleasedEvent, HandlePointerReleased, RoutingStrategies.Tunnel);
    }

    protected override async void ArrangeCore(Rect finalRect)
    {
        if (pendingChange is { } e)
        {
            pendingChange = null;

            try
            {
                var markdown = e.NewString;
                var timer = Stopwatch.StartNew();
                var document = await Task.Run(() => Markdig.Markdown.Parse(markdown, pipeline));
                PrintMetrics($"Parse markdown in {timer.Elapsed.TotalMicroseconds} micro sec.");

                timer.Restart();
                documentNode.Update(document, e, CancellationToken.None);
                PrintMetrics($"Render markdown in {timer.Elapsed.TotalMicroseconds} micro sec.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await Console.Error.WriteAsync($"Error while rendering markdown: {ex.Message}");
            }
        }

        base.ArrangeCore(finalRect);
    }

    private void CommitChange(in ObservableStringBuilderChangedEventArgs e)
    {
        Dispatcher.UIThread.VerifyAccess();

        if (pendingChange is null) pendingChange = e;
        else
        {
            pendingChange = new ObservableStringBuilderChangedEventArgs(
                e.NewString,
                Math.Min(pendingChange.Value.StartIndex, e.StartIndex),
                Math.Max(pendingChange.Value.Length, e.Length));
        }

        InvalidateArrange();
    }
}