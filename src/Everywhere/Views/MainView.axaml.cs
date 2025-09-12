using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Everywhere.Configuration;
using ShadUI.Themes;

namespace Everywhere.Views;

public partial class MainView : ReactiveUserControl<MainViewModel>
{
    private readonly Settings _settings;

    public MainView(Settings settings)
    {
        _settings = settings;

        InitializeComponent();
    }

    private void HandleSettingsCommonPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommonSettings.Theme)) return;
        Dispatcher.UIThread.InvokeOnDemand(ApplyTheme);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _settings.Common.PropertyChanged += HandleSettingsCommonPropertyChanged;
        ApplyTheme();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        _settings.Common.PropertyChanged -= HandleSettingsCommonPropertyChanged;
    }

    private void ApplyTheme()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;
        topLevel.RequestedThemeVariant = _settings.Common.Theme switch
        {
            "Dark" => ThemeVariants.Dark,
            "Light" => ThemeVariants.Light,
            _ => ThemeVariants.Default
        };
    }
}