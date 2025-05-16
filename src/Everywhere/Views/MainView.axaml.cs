using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Everywhere.I18N;
using Everywhere.Models;

namespace Everywhere.Views;

public partial class MainView : ReactiveUserControl<MainViewModel>
{
    private readonly Settings settings;

    public MainView(Settings settings)
    {
        this.settings = settings;

        InitializeComponent();

        LocaleManager.CurrentLocale = CultureInfo.CurrentUICulture.Name;

        settings.Common.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(CommonSettings.Theme)) return;
            Dispatcher.UIThread.InvokeOnDemand(ApplyTheme);
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;
        topLevel.RequestedThemeVariant = settings.Common.Theme switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }
}