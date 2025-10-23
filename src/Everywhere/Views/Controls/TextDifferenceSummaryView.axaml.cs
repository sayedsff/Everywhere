using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Chat.Plugins;
using ZLinq;

namespace Everywhere.Views;

public partial class TextDifferenceSummaryView : TemplatedControl
{
    public static readonly StyledProperty<TextDifference?> TextDifferenceProperty =
        AvaloniaProperty.Register<TextDifferenceSummaryView, TextDifference?>(nameof(TextDifference));

    public TextDifference? TextDifference
    {
        get => GetValue(TextDifferenceProperty);
        set => SetValue(TextDifferenceProperty, value);
    }

    public static readonly StyledProperty<string?> OriginalTextProperty =
        AvaloniaProperty.Register<TextDifferenceSummaryView, string?>(nameof(OriginalText));

    public string? OriginalText
    {
        get => GetValue(OriginalTextProperty);
        set => SetValue(OriginalTextProperty, value);
    }

    public int AddedLineCount =>
        TextDifference?.Changes
            .AsValueEnumerable()
            .Where(change => change.Kind is TextChangeKind.Insert or TextChangeKind.Replace)
            .Sum(change => TextDifferenceRenderer.CountLines(change.NewText ?? string.Empty)) ?? 0;

    public int RemovedLineCount =>
        TextDifference?.Changes
            .AsValueEnumerable()
            .Where(change => change.Kind is TextChangeKind.Delete or TextChangeKind.Replace)
            .Sum(change => TextDifferenceRenderer.CountLines(change.GetOriginalSlice(OriginalText ?? string.Empty))) ?? 0;

    [RelayCommand]
    private Task OpenFileAsync()
    {
        if (TextDifference is not { FilePath: { Length: > 0 } filePath } ||
            TopLevel.GetTopLevel(this) is not { Launcher: { } launcher } ||
            !Uri.TryCreate(filePath, UriKind.Absolute, out var uri))
        {
            return Task.CompletedTask;
        }

        return launcher.LaunchUriAsync(uri);
    }

    [RelayCommand]
    private void Edit()
    {
        if (TextDifference is not { } textDifference) return;
        if (OriginalText is not { } originalText) return;

        var window = new TransientWindow
        {
            Content = new TextDifferenceEditor
            {
                TextDifference = textDifference,
                OriginalText = originalText,
                ShowLineNumbers = true
            }
        };
        window.Closed += delegate
        {
            textDifference.TrySetAcceptanceResult();
        };

        if (TopLevel.GetTopLevel(this) is Window owner) window.ShowDialog(owner);
        else window.Show();
    }

    [RelayCommand]
    private void AcceptAll()
    {
        if (TextDifference is not { } textDifference) return;
        textDifference.AcceptAll();
        textDifference.TrySetAcceptanceResult();
    }

    [RelayCommand]
    private void DiscardAll()
    {
        if (TextDifference is not { } textDifference) return;
        textDifference.DiscardAll();
        textDifference.TrySetAcceptanceResult();
    }
}