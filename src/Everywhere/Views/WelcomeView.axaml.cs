using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform;
using LiveMarkdown.Avalonia;
using Microsoft.Extensions.Logging;

namespace Everywhere.Views;

public partial class WelcomeView : ReactiveUserControl<WelcomeViewModel>
{
    public static readonly DirectProperty<WelcomeView, ObservableStringBuilder> MarkdownBuilderProperty =
        AvaloniaProperty.RegisterDirect<WelcomeView, ObservableStringBuilder>(
            nameof(MarkdownBuilder),
            o => o.MarkdownBuilder);

    public ObservableStringBuilder MarkdownBuilder { get; } = new();

    public WelcomeView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        try
        {
            MarkdownBuilder.Clear();

            using var changeLogReader = new StreamReader(AssetLoader.Open(new Uri("avares://Everywhere/Assets/CHANGELOG.md", UriKind.Absolute)));

            var maxLines = 200;
            while (changeLogReader.ReadLine() is { } line && maxLines-- > 0)
            {
                MarkdownBuilder.AppendLine(line);
            }

            if (maxLines == 0)
            {
                MarkdownBuilder.AppendLine("... (truncated)");
            }
        }
        catch (Exception ex)
        {
            ServiceLocator.Resolve<ILogger<WelcomeView>>().LogError(ex, "Failed to load changelog.");
        }
    }

    private void HandleMarkdownRendererInlineHyperlinkClick(object? sender, InlineHyperlinkClickedEventArgs e)
    {
        if (e.HRef is not { IsAbsoluteUri: true, Scheme: "https" or "http" } href) return;

        TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(href);
    }
}