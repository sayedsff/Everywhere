using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Everywhere.I18N;
using Everywhere.Models;
using ShadUI.Themes;

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
            "Dark" => ThemeVariants.Dark,
            "Light" => ThemeVariants.Light,
            _ => ThemeVariants.Default
        };
    }
}