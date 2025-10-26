using ObservableCollections;

namespace Everywhere.Configuration;

public class PluginSettings : SettingsCategory
{
    public override string Header => "Plugin";

    public ObservableDictionary<string, bool> IsEnabledRecords { get; set; } = new();

    public WebSearchEngineSettings WebSearchEngine { get; set; } = new();

    public PluginSettings()
    {
        IsEnabledRecords.CollectionChanged += delegate { OnPropertyChanged(nameof(IsEnabledRecords)); };
    }
}