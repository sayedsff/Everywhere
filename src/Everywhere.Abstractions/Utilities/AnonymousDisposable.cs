using System.ComponentModel;

namespace Everywhere.Utilities;

public class AnonymousDisposable(Action disposeAction) : IDisposable
{
    public static IDisposable Empty { get; } = new AnonymousDisposable(static () => { });

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        disposeAction();
    }

    public static AnonymousDisposable FromNotifyPropertyChanged(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        source.PropertyChanged += handler;
        return new AnonymousDisposable(() => source.PropertyChanged -= handler);
    }
}