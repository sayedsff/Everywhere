using System.ComponentModel;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using MessagePack;

namespace Everywhere.Utils;

public delegate void TrackableObjectPropertyChangedEventHandler(TrackableObject sender, PropertyChangedEventArgs e);

public class TrackableObject(string scope) : ObservableObject
{
    private static readonly ConcurrentDictionary<string, HashSet<TrackableObjectPropertyChangedEventHandler>> ScopeHandlers = new();

    public static IDisposable AddPropertyChangedEventHandler(string scope, TrackableObjectPropertyChangedEventHandler handler)
    {
        if (string.IsNullOrEmpty(scope)) throw new ArgumentException("Scope cannot be null or empty.", nameof(scope));

        var handlers = ScopeHandlers.GetOrAdd(scope, _ => []);
        lock (handlers)
        {
            handlers.Add(handler);
        }

        return new AnonymousDisposable(() =>
        {
            if (!ScopeHandlers.TryGetValue(scope, out var currentHandlers)) return;

            lock (currentHandlers)
            {
                currentHandlers.Remove(handler);
            }
        });
    }

    [JsonIgnore]
    [IgnoreMember]
    public string Scope { get; } = scope;

    [JsonIgnore]
    [IgnoreMember]
    protected bool isTrackingEnabled;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        NotifyHandlers(e);
    }

    protected void NotifyHandlers(string propertyName)
    {
        if (!isTrackingEnabled) return;
        NotifyHandlers(new PropertyChangedEventArgs(propertyName));
    }

    protected void NotifyHandlers(PropertyChangedEventArgs e)
    {
        if (!isTrackingEnabled) return;
        if (!ScopeHandlers.TryGetValue(Scope, out var handlers)) return;
        Console.WriteLine($"Property changed in scope '{Scope}': {e.PropertyName}");
        lock (handlers)
        {
            foreach (var handler in handlers)
            {
                handler(this, e);
            }
        }
    }
}