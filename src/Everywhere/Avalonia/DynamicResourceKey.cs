using Avalonia.Controls;

namespace Everywhere.Avalonia;

public class DynamicResourceKey(object key) : IObservable<object?>
{
    /// <summary>
    /// so why axaml DOES NOT SUPPORT {Binding .^} ???????
    /// </summary>
    public DynamicResourceKey Self => this;

    public IDisposable Subscribe(IObserver<object?> observer) =>
        Application.Current!.Resources.GetResourceObservable(key).Subscribe(observer);

    public static implicit operator DynamicResourceKey(string key) => new(key);
}

public class DynamicResourceKeyWrapper<T>(object key, T value) : DynamicResourceKey(key)
{
    public T Value => value;

    public override bool Equals(object? obj)
    {
        return obj is DynamicResourceKeyWrapper<T> other && EqualityComparer<T>.Default.Equals(value, other.Value);
    }

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}