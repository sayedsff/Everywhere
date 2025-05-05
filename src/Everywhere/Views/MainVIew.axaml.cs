using System.Globalization;
using Everywhere.I18N;

namespace Everywhere.Views;

public partial class MainView : ReactiveUserControl<MainViewModel>
{
    public MainView()
    {
        InitializeComponent();

        LocaleManager.CurrentLocale = CultureInfo.CurrentUICulture.Name;
    }
}