using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ObservableCollections;

namespace Everywhere.Chat;

public interface IChatContextManager : INotifyPropertyChanged
{
    NotifyCollectionChangedSynchronizedViewList<ChatMessageNode> ChatMessageNodes { get; }

    ChatContext Current { get; set; }

    IReadOnlyList<ChatContextHistory> History { get; }

    IReadOnlyDictionary<string, Func<string>> SystemPromptVariables { get; }

    IRelayCommand CreateNewCommand { get; }

    IRelayCommand<ChatContext> RemoveCommand { get; }
    
    void UpdateHistory();
}