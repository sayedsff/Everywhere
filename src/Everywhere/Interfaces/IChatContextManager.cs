using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Models;
using ObservableCollections;

namespace Everywhere.Interfaces;

public interface IChatContextManager : INotifyPropertyChanged
{
    NotifyCollectionChangedSynchronizedViewList<ChatMessageNode> ChatMessageNodes { get; }

    ChatContext Current { get; set; }

    IEnumerable<ChatContextHistory> History { get; }

    IRelayCommand CreateNewCommand { get; }

    IRelayCommand<ChatContext> RenameCommand { get; }

    IRelayCommand<ChatContext> RemoveCommand { get; }
    
    void UpdateHistory();
}