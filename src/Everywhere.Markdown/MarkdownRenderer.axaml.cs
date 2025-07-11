// @author https://github.com/DearVa
// @author https://github.com/AuroraZiling
// @author https://github.com/SlimeNull

using System.Diagnostics;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Controls;
using Markdig;

namespace Everywhere.Markdown;

public partial class MarkdownRenderer : Control
{
    public ObservableStringBuilder MarkdownBuilder { get; } = new();

    private Channel<ObservableStringBuilderChangedEventArgs>? renderChannel;

    private readonly DocumentNode documentNode = new();
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseCodeBlockSpanFixer()
        .Build();

    public MarkdownRenderer()
    {
        LogicalChildren.Add(documentNode.Control);
        VisualChildren.Add(documentNode.Control);
        MarkdownBuilder.Changed += HandleMarkdownBuilderChanged;
    }

    private async void RenderProcessAsync(ChannelReader<ObservableStringBuilderChangedEventArgs> reader)
    {
        try
        {
            while (await reader.WaitToReadAsync())
            {
                while (reader.TryRead(out var e))
                {
                    var markdown = e.NewString;
                    var timer = Stopwatch.StartNew();
                    var document = await Task.Run(() => Markdig.Markdown.Parse(markdown, pipeline));
                    PrintMetrics($"Parse markdown in {timer.Elapsed.TotalMicroseconds} micro sec.");
                    timer.Restart();
                    documentNode.Update(document, e, CancellationToken.None);
                    PrintMetrics($"Render markdown in {timer.Elapsed.TotalMicroseconds} micro sec.");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Error rendering markdown\n{ex}");
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IsVisibleProperty) return;
        if (change.NewValue is true)
        {
            BeginRender();
        }
        else
        {
            EndRender();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsVisible)
        {
            BeginRender();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        EndRender();
    }

    private void BeginRender()
    {
        if (renderChannel is not null) return;
        renderChannel = Channel.CreateUnbounded<ObservableStringBuilderChangedEventArgs>();
        RenderProcessAsync(renderChannel);
        renderChannel.Writer.TryWrite(
            new ObservableStringBuilderChangedEventArgs(
                MarkdownBuilder.ToString(),
                0,
                MarkdownBuilder.Length));
    }

    private void EndRender()
    {
        if (renderChannel is null) return;
        renderChannel.Writer.Complete();
        renderChannel = null;
    }

    private void HandleMarkdownBuilderChanged(in ObservableStringBuilderChangedEventArgs e)
    {
        renderChannel?.Writer.WriteAsync(e).AsTask().Wait();
    }
}