using System.Globalization;
using Everywhere.Models;
using Everywhere.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Everywhere.Initialization;

/// <summary>
/// Used to initialize settings-saving logic.
/// </summary>
/// <param name="configuration"></param>
public class SettingsInitializer([FromKeyedServices(nameof(Settings))] IConfiguration configuration) : IAsyncInitializer
{
    private readonly DebounceHelper debounceHelper = new(TimeSpan.FromSeconds(1));

    public int Priority => 10;

    public Task InitializeAsync()
    {
        LocaleManager.CurrentLocale ??= CultureInfo.CurrentUICulture.Name;

        TrackableObject<SettingsBase>.AddPropertyChangedEventHandler(
            (sender, _) =>
            {
                debounceHelper.Execute(() =>
                {
                    if (sender.Section.Length == 0) configuration.Set(sender);
                    else configuration.Set(sender.Section, sender);
                });
            });

        return Task.CompletedTask;
    }
}