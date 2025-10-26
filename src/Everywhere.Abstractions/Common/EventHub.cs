using Everywhere.Utilities;

namespace Everywhere.Common;

/// <summary>
/// An interface for event subscribers. Used by <see cref="EventHub{TEvent}"/>
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public interface IEventSubscriber<in TEvent>
{
    void HandleEvent(TEvent @event);
}

/// <summary>
/// A thread-safe, subscriber-based, weak reference event hub
/// </summary>
public static class EventHub<TEvent>
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly Lock SyncLock = new();
    private static readonly List<WeakReference<IEventSubscriber<TEvent>>> Subscribers = [];

    public static IDisposable Subscribe(IEventSubscriber<TEvent> subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);

        var weakReference = new WeakReference<IEventSubscriber<TEvent>>(subscriber);
        lock (SyncLock)
        {
            Prune();
            Subscribers.Add(weakReference);
        }

        return new AnonymousDisposable(() =>
        {
            lock (SyncLock)
            {
                Subscribers.Remove(weakReference);
            }
        });
    }

    public static void Publish(TEvent @event)
    {
        List<IEventSubscriber<TEvent>> targets;

        lock (SyncLock)
        {
            Prune();
            if (Subscribers.Count == 0) return;

            targets = new List<IEventSubscriber<TEvent>>(Subscribers.Count);
            foreach (var weakReference in Subscribers)
            {
                if (weakReference.TryGetTarget(out var target))
                    targets.Add(target);
            }
        }

        foreach (var target in targets)
        {
            target.HandleEvent(@event);
        }
    }

    /// <summary>
    /// Removes dead weak references from the subscriber list
    /// </summary>
    private static void Prune()
    {
        for (var i = Subscribers.Count - 1; i >= 0; i--)
        {
            if (!Subscribers[i].TryGetTarget(out _))
                Subscribers.RemoveAt(i);
        }
    }
}