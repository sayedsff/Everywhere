using Avalonia.Data.Converters;
using ObservableCollections;

namespace Everywhere.ValueConverters;

public static class ObservableListConverters
{
    public static IValueConverter ObservableOnDispatcher { get; } = new FuncValueConverter<dynamic?, dynamic?>(
        list =>
        {
            if (list is null) return null;

            var dispatcher = SynchronizationContextCollectionEventDispatcher.Current;
            return list.ToNotifyCollectionChangedSlim(dispatcher);
        });
}