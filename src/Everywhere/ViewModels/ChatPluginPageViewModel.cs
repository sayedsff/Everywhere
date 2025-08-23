using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Chat.Plugins;

namespace Everywhere.ViewModels;

public partial class ChatPluginPageViewModel(IChatPluginManager manager) : ReactiveViewModelBase
{
    public IChatPluginManager Manager => manager;

    [ObservableProperty]
    public partial ChatPlugin? SelectedPlugin { get; set; }
}