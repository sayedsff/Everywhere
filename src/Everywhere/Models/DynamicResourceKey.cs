using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Reactive;
using Everywhere.Utils;
using MessagePack;

namespace Everywhere.Models;

/// <summary>
/// This class is used to create a dynamic resource key for axaml Binding.
/// </summary>
/// <param name="key"></param>
[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
[Union(0, typeof(DynamicResourceKey))]
[Union(1, typeof(DirectResourceKey))]
[Union(2, typeof(FormattedDynamicResourceKey))]
public partial class DynamicResourceKey(object key) : IObservable<object?>
{
    /// <summary>
    /// so why axaml DOES NOT SUPPORT {Binding .^} ???????
    /// </summary>
    public DynamicResourceKey Self => this;

    [Key(0)]
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

[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class DirectResourceKey(object key) : DynamicResourceKey(key)
{
    private static readonly IDisposable NullDisposable = new AnonymousDisposable(() => { });

    public override IDisposable Subscribe(IObserver<object?> observer)
    {
        observer.OnNext(Key);
        return NullDisposable;
    }
}

[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class FormattedDynamicResourceKey(object key, params object?[] args) : DynamicResourceKey(key)
{
    [Key(1)]
    private object?[] Args => args;

    public override IDisposable Subscribe(IObserver<object?> observer) =>
        Application.Current!.Resources.GetResourceObservable(Key).Subscribe(
            new AnonymousObserver<object?>(o =>
            {
                observer.OnNext(string.Format(o?.ToString() ?? string.Empty, args));
            }));
}

[AttributeUsage(AttributeTargets.All)]
public class DynamicResourceKeyAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}