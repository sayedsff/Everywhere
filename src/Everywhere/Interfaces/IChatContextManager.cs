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

    void UpdateHistory();

    void Remove(ChatContext chatContext);
}