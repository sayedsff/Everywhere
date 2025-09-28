using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Chat.Plugins;

namespace Everywhere.ViewModels;

public partial class ChatPluginPageViewModel(IChatPluginManager manager) : ReactiveViewModelBase
{
    public IChatPluginManager Manager => manager;

    public ChatPlugin? SelectedPlugin
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;

            // TabItem0 is invisible when there is no SettingsItems, so switch to TabItem1
            if (value is not { SettingsItems.Count: > 0 })
            {
                PluginDetailsTabSelectedIndex = 1;
            }
        }
    }

    [ObservableProperty]
    public partial int PluginDetailsTabSelectedIndex { get; set; }
}