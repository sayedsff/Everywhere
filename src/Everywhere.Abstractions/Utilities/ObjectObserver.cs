using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using ZLinq;

namespace Everywhere.Utilities;

public readonly record struct ObjectObserverChangedEventArgs(string Path, object? Value);

public delegate void ObjectObserverChangedEventHandler(in ObjectObserverChangedEventArgs e);

/// <summary>
/// Observes an INotifyPropertyChanged and its properties for changes.
/// Supports nested objects and collections.
/// </summary>
public class ObjectObserver(ObjectObserverChangedEventHandler handler) : IDisposable
{
    private readonly ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> _cachedProperties = [];

    private IReadOnlyList<PropertyInfo> GetPropertyInfos(Type type) =>
        _cachedProperties.GetOrAdd(
            type,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .AsValueEnumerable()
                .Where(p =>
                    p is { CanRead: true, CanWrite: true, IsSpecialName: false } ||
                    p.PropertyType.IsAssignableTo(typeof(INotifyPropertyChanged)))
                .Where(p => p.GetMethod?.GetParameters() is { Length: 0 }) // Ignore
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                .ToList());

    private PropertyInfo? GetPropertyInfo(Type type, string propertyName) =>
        GetPropertyInfos(type).AsValueEnumerable().FirstOrDefault(p => p.Name == propertyName);

    private readonly ObjectObserverChangedEventHandler _handler = handler;
    private readonly DisposeCollector<Observation> _observations = new();

    ~ObjectObserver()
    {
        Dispose();
    }

    public ObjectObserver Observe(INotifyPropertyChanged target, string basePath = "")
    {
        _observations.Add(new Observation(basePath, target, this));
        return this;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _observations.Dispose();
    }

    private class Observation : IDisposable
    {
        private readonly string _basePath;
        private readonly Type _targetType;
        private readonly ObjectObserver _owner;
        private readonly WeakReference<INotifyPropertyChanged> _targetReference;
        private readonly ConcurrentDictionary<string, Observation> _observations = [];

        /// <summary>
        /// when <see cref="ObservableCollection{T}"/> is Reset, we cannot get the old items count from event args.
        /// So we need to keep track of the count ourselves.
        /// </summary>
        private int _listItemCount;
        private bool _isDisposed;

        public Observation(string basePath, INotifyPropertyChanged target, ObjectObserver owner)
        {
            _basePath = (basePath + ':').TrimStart(':');
            _targetType = target.GetType();
            _owner = owner;
            _targetReference = new WeakReference<INotifyPropertyChanged>(target);

            target.PropertyChanged += HandleTargetPropertyChanged;
            if (target is INotifyCollectionChanged notifyCollectionChanged)
            {
                notifyCollectionChanged.CollectionChanged += HandleTargetCollectionChanged;
            }

            foreach (var propertyInfo in owner.GetPropertyInfos(target.GetType()))
            {
                object? value;
                try
                {
                    value = propertyInfo.GetValue(target);
                }
                catch
                {
                    value = null;
                }

                ObserveObject(propertyInfo.Name, value);
            }

            if (target is IList list)
            {
                _listItemCount = list.Count;
                for (var i = 0; i < list.Count; i++)
                {
                    ObserveObject(i.ToString(), list[i]);
                }
            }
        }

        ~Observation()
        {
            Dispose();
        }

        private void HandleTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isDisposed) return;
            if (e.PropertyName is null) return;
            if (!_targetReference.TryGetTarget(out var target)) return;
            if (_owner.GetPropertyInfo(_targetType, e.PropertyName) is not { } propertyInfo) return;

            object? value;
            try
            {
                value = propertyInfo.GetValue(target);
            }
            catch
            {
                value = null;
            }

            _owner._handler.Invoke(new ObjectObserverChangedEventArgs(_basePath + e.PropertyName, value));

            ObserveObject(e.PropertyName, value);
        }

        private void HandleTargetCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isDisposed) return;
            if (sender is not IList list) return;

            if (e.OldItems is not null)
            {
                var index = e.OldStartingIndex;
                for (var i = 0; i < e.OldItems.Count; i++)
                {
                    ObserveObject((index++).ToString(), null);
                }
            }

            if (e.NewItems is not null)
            {
                var index = e.NewStartingIndex;
                foreach (var item in e.NewItems)
                {
                    ObserveObject((index++).ToString(), item);
                }
            }

            Range changeRange = default;

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                {
                    // New items added, shift subsequent indices
                    changeRange = new Range(e.NewStartingIndex, list.Count);
                    break;
                }
                case NotifyCollectionChangedAction.Remove:
                {
                    // Items removed, notify the whole object as changed
                    _owner._handler.Invoke(new ObjectObserverChangedEventArgs(_basePath.TrimEnd(':'), sender));
                    break;
                }
                case NotifyCollectionChangedAction.Replace:
                {
                    // Items replaced, no index shift but need to re-observe
                    changeRange = new Range(e.NewStartingIndex, e.NewStartingIndex + e.NewItems?.Count ?? 1);
                    break;
                }
                case NotifyCollectionChangedAction.Move:
                {
                    // Items moved, re-observe old and new positions
                    _owner._handler.Invoke(new ObjectObserverChangedEventArgs(_basePath + e.OldStartingIndex, list[e.OldStartingIndex]));
                    _owner._handler.Invoke(new ObjectObserverChangedEventArgs(_basePath + e.NewStartingIndex, list[e.NewStartingIndex]));
                    break;
                }
                case NotifyCollectionChangedAction.Reset:
                {
                    // Reset clears the collection, so we need to re-observe all items
                    for (var i = 0; i < _listItemCount; i++)
                    {
                        ObserveObject(i.ToString(), null);
                    }

                    // Notify the whole object as changed
                    _owner._handler.Invoke(new ObjectObserverChangedEventArgs(_basePath.TrimEnd(':'), sender));
                    break;
                }
            }

            _listItemCount = list.Count;

            // Notify changes in the affected range
            for (var i = changeRange.Start.Value; i < changeRange.End.Value; i++)
            {
                if (i < 0 || i >= list.Count) continue; // Skip out of bounds indices
                _owner._handler.Invoke(new ObjectObserverChangedEventArgs(_basePath + i, list[i]));
            }
        }

        private void ObserveObject(string path, object? target)
        {
            if (target is not INotifyPropertyChanged notifyPropertyChanged)
            {
                _observations.TryRemove(path, out var observation);
                observation?.Dispose();
            }
            else
            {
                _observations.AddOrUpdate(
                    path,
                    _ => new Observation(_basePath + path, notifyPropertyChanged, _owner),
                    (_, o) =>
                    {
                        if (o._targetReference.TryGetTarget(out var t) && Equals(t, notifyPropertyChanged)) return o;

                        o.Dispose();
                        return new Observation(_basePath + path, notifyPropertyChanged, _owner);
                    });
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            GC.SuppressFinalize(this);

            if (!_targetReference.TryGetTarget(out var target)) return;

            target.PropertyChanged -= HandleTargetPropertyChanged;
            if (target is INotifyCollectionChanged notifyCollectionChanged)
            {
                notifyCollectionChanged.CollectionChanged -= HandleTargetCollectionChanged;
            }
        }
    }
}