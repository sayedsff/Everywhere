using System.ComponentModel;
using Everywhere.Models;
using ObservableCollections;

namespace Everywhere.Interfaces;

public interface IChatContextManager : INotifyPropertyChanged
{
    INotifyCollectionChangedSynchronizedViewList<ChatMessageNode> ChatMessageNodes { get; }

    ChatContext Current { get; set; }

    IEnumerable<ChatContextHistory> History { get; }

    void CreateNew();

    void UpdateHistory();
}