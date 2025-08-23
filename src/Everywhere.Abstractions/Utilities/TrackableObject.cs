using System.ComponentModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using MessagePack;

namespace Everywhere.Utilities;

public delegate void TrackableObjectPropertyChangedEventHandler<in TScope>(TScope sender, PropertyChangedEventArgs e);

public class TrackableObject<TScope>(bool isTrackingEnabled = false) : ObservableObject where TScope : TrackableObject<TScope>
{
    private static readonly HashSet<TrackableObjectPropertyChangedEventHandler<TScope>> ScopeHandlers = [];

    public static IDisposable AddPropertyChangedEventHandler(TrackableObjectPropertyChangedEventHandler<TScope> handler)
    {
        lock (ScopeHandlers)
        {
            ScopeHandlers.Add(handler);
        }

        return new AnonymousDisposable(() =>
        {
            lock (ScopeHandlers)
            {
                ScopeHandlers.Remove(handler);
            }
        });
    }

    [JsonIgnore]
    [IgnoreMember]
    public bool IsTrackingEnabled { get; set; } = isTrackingEnabled;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        NotifyHandlers(e);
    }

    protected void NotifyHandlers(string propertyName)
    {
        if (!IsTrackingEnabled) return;
        NotifyHandlers(new PropertyChangedEventArgs(propertyName));
    }

    protected void NotifyHandlers(PropertyChangedEventArgs e)
    {
        if (!IsTrackingEnabled) return;

        lock (ScopeHandlers)
        {
            foreach (var handler in ScopeHandlers)
            {
                handler((TScope)this, e);
            }
        }
    }
}