using Avalonia.Controls;

namespace Everywhere.Avalonia;

public class DynamicResourceKey(object key) : IObservable<object?>
{
    public IDisposable Subscribe(IObserver<object?> observer) =>
        Application.Current!.Resources.GetResourceObservable(key).Subscribe(observer);

    public static implicit operator DynamicResourceKey(string key) => new(key);
}