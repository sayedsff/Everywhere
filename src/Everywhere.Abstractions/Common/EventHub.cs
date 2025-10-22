using Everywhere.Utilities;

namespace Everywhere.Common;

public interface IEventSubscriber<in TEvent>
{
    void HandleEvent(TEvent @event);
}

/// <summary>
/// A thread-safe, subscriber-based, weak reference event hub
/// </summary>
public static class EventHub<TEvent>
{
    private static readonly List<WeakReference<IEventSubscriber<TEvent>>> Subscribers = [];

    public static IDisposable Subscribe(IEventSubscriber<TEvent> subscriber)
    {
        var weakReference = new WeakReference<IEventSubscriber<TEvent>>(subscriber);
        lock (Subscribers) Subscribers.Add(weakReference);
        return new AnonymousDisposable(() =>
        {
            lock (Subscribers) Subscribers.Remove(weakReference);
        });
    }

    public static void Publish(TEvent @event)
    {
        lock (Subscribers)
        {
            foreach (var weakReference in Subscribers)
            {
                if (weakReference.TryGetTarget(out var target)) target.HandleEvent(@event);
            }
        }
    }
}