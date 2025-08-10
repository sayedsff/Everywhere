using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using Everywhere.Utilities;
using ZLinq;

namespace Everywhere.Models;

public readonly record struct ObjectObserverChangedEventArgs(string Path, object? Value);

public delegate void ObjectObserverChangedEventHandler(in ObjectObserverChangedEventArgs e);

/// <summary>
/// Observes an INotifyPropertyChanged and its properties for changes.
/// Supports nested objects and collections.
/// </summary>
public class ObjectObserver(ObjectObserverChangedEventHandler handler) : IDisposable
{
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> _cachedProperties = [];

    private PropertyInfo[] GetPropertyInfos(Type type) =>
        _cachedProperties.GetOrAdd(
            type,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .AsValueEnumerable()
                .Where(p => p is { CanRead: true, CanWrite: true, IsSpecialName: false })
                .Where(p => p.GetMethod?.GetParameters() is { Length: 0 }) // Ignore
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                .ToArray());

    private PropertyInfo? GetPropertyInfo(Type type, string propertyName) =>
        GetPropertyInfos(type).AsValueEnumerable().FirstOrDefault(p => p.Name == propertyName);

    private readonly ObjectObserverChangedEventHandler _handler = handler;
    private readonly DisposeCollector<Observation> _observations = new();

    ~ObjectObserver()
    {
        Dispose();
    }

    public ObjectObserver Observe(INotifyPropertyChanged target, string basePath)
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

        private bool _isDisposed;

        public Observation(string basePath, INotifyPropertyChanged target, ObjectObserver owner)
        {
            _basePath = basePath;
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

            _owner._handler.Invoke(new ObjectObserverChangedEventArgs($"{_basePath}:{e.PropertyName}", value));

            ObserveObject(e.PropertyName, value);
        }

        private void HandleTargetCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isDisposed) return;

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

            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                for (var i = 0; i < sender.NotNull<IList>().Count; i++)
                {
                    ObserveObject(i.ToString(), null);
                }
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
                    _ => new Observation($"{_basePath}:{path}", notifyPropertyChanged, _owner),
                    (_, o) =>
                    {
                        if (o._targetReference.TryGetTarget(out var t) && Equals(t, notifyPropertyChanged)) return o;

                        o.Dispose();
                        return new Observation($"{_basePath}:{path}", notifyPropertyChanged, _owner);
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