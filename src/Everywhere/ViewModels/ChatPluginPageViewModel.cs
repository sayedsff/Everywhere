namespace Everywhere.ViewModels;

public class ChatPluginPageViewModel(IChatPluginManager manager) : ReactiveViewModelBase
{
    public IChatPluginManager Manager => manager;
}