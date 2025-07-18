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
        TrackableObject.AddPropertyChangedEventHandler(
            nameof(Settings),
            (sender, _) =>
            {
                if (sender is not SettingsBase settingsBase) return;

                debounceHelper.Execute(() =>
                {
                    if (settingsBase.Section.Length == 0) configuration.Set(settingsBase);
                    else configuration.Set(settingsBase.Section, settingsBase);
                });
            });

        return Task.CompletedTask;
    }
}