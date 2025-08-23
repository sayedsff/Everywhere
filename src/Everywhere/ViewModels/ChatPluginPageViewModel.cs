using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Models;

namespace Everywhere.ViewModels;

public partial class ChatPluginPageViewModel(IChatPluginManager manager) : ReactiveViewModelBase
{
    public IChatPluginManager Manager => manager;

    [ObservableProperty]
    public partial ChatPlugin? SelectedPlugin { get; set; }
}