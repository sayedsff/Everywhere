using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Everywhere.Models;

public partial class Settings : ObservableValidator
{
    [ObservableProperty]
    public partial string Language { get; set; } = CultureInfo.CurrentUICulture.Name;
}