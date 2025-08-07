using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using Everywhere.Utils;
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
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> cachedProperties = [];

    private PropertyInfo[] GetPropertyInfos(Type type) =>
        cachedProperties.GetOrAdd(
            type,
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .AsValueEnumerable()
                .Where(p => p is { CanRead: true, CanWrite: true, IsSpecialName: false })
                .Where(p => p.GetMethod?.GetParameters() is { Length: 0 }) // Ignore
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                .ToArray());

    private PropertyInfo? GetPropertyInfo(Type type, string propertyName) =>
        GetPropertyInfos(type).AsValueEnumerable().FirstOrDefault(p => p.Name == propertyName);

    private readonly ObjectObserverChangedEventHandler handler = handler;
    private readonly DisposeCollector<Observation> observations = new();

    ~ObjectObserver()
    {
        Dispose();
    }

    public ObjectObserver Observe(INotifyPropertyChanged target, string basePath)
    {
        observations.Add(new Observation(basePath, target, this));
        return this;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        observations.Dispose();
    }

    private class Observation : IDisposable
    {
        private readonly string basePath;
        private readonly Type targetType;
        private readonly ObjectObserver owner;
        private readonly WeakReference<INotifyPropertyChanged> targetReference;
        private readonly ConcurrentDictionary<string, Observation> observations = [];

        private bool isDisposed;

        public Observation(string basePath, INotifyPropertyChanged target, ObjectObserver owner)
        {
            this.basePath = basePath;
            targetType = target.GetType();
            this.owner = owner;
            targetReference = new WeakReference<INotifyPropertyChanged>(target);

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
            if (isDisposed) return;
            if (e.PropertyName is null) return;
            if (!targetReference.TryGetTarget(out var target)) return;
            if (owner.GetPropertyInfo(targetType, e.PropertyName) is not { } propertyInfo) return;

            object? value;
            try
            {
                value = propertyInfo.GetValue(target);
            }
            catch
            {
                value = null;
            }

            owner.handler.Invoke(new ObjectObserverChangedEventArgs($"{basePath}:{e.PropertyName}", value));

            ObserveObject(e.PropertyName, value);
        }

        private void HandleTargetCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (isDisposed) return;

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
                observations.TryRemove(path, out var observation);
                observation?.Dispose();
            }
            else
            {
                observations.AddOrUpdate(
                    path,
                    _ => new Observation($"{basePath}:{path}", notifyPropertyChanged, owner),
                    (_, o) =>
                    {
                        if (o.targetReference.TryGetTarget(out var t) && Equals(t, notifyPropertyChanged)) return o;

                        o.Dispose();
                        return new Observation($"{basePath}:{path}", notifyPropertyChanged, owner);
                    });
            }
        }

        public void Dispose()
        {
            if (isDisposed) return;

            isDisposed = true;
            GC.SuppressFinalize(this);

            if (!targetReference.TryGetTarget(out var target)) return;

            target.PropertyChanged -= HandleTargetPropertyChanged;
            if (target is INotifyCollectionChanged notifyCollectionChanged)
            {
                notifyCollectionChanged.CollectionChanged -= HandleTargetCollectionChanged;
            }
        }
    }
}