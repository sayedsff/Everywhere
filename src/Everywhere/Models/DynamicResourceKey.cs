using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Reactive;
using Everywhere.Utils;

namespace Everywhere.Models;

/// <summary>
/// This class is used to create a dynamic resource key for axaml Binding.
/// </summary>
/// <param name="key"></param>
public class DynamicResourceKey(object key) : IObservable<object?>
{
    /// <summary>
    /// so why axaml DOES NOT SUPPORT {Binding .^} ???????
    /// </summary>
    public DynamicResourceKey Self => this;

    protected object Key => key;

    public virtual IDisposable Subscribe(IObserver<object?> observer) =>
        Application.Current!.Resources.GetResourceObservable(key).Subscribe(observer);

    [return: NotNullIfNotNull(nameof(key))]
    public static implicit operator DynamicResourceKey?(string? key) => key == null ? null : new DynamicResourceKey(key);
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

public class DirectResourceKey(object key) : DynamicResourceKey(key)
{
    private static readonly IDisposable NullDisposable = new AnonymousDisposable(() => { });

    public override IDisposable Subscribe(IObserver<object?> observer)
    {
        observer.OnNext(Key);
        return NullDisposable;
    }
}

public class FormattedDynamicResourceKey(object key, params object?[] args) : DynamicResourceKey(key)
{
    public override IDisposable Subscribe(IObserver<object?> observer) =>
        Application.Current!.Resources.GetResourceObservable(Key).Subscribe(
            new AnonymousObserver<object?>(o =>
            {
                observer.OnNext(string.Format(o?.ToString() ?? string.Empty, args));
            }));
}