using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Reactive;
using Everywhere.Utils;
using MessagePack;

namespace Everywhere.Models;

/// <summary>
/// MessagePack serializable base class for dynamic resource keys. Make them happy.
/// </summary>
[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
[Union(0, typeof(DynamicResourceKey))]
[Union(1, typeof(DirectResourceKey))]
[Union(2, typeof(FormattedDynamicResourceKey))]
public abstract partial class DynamicResourceKeyBase : IObservable<object?>
{
    public abstract IDisposable Subscribe(IObserver<object?> observer);
}

/// <summary>
/// This class is used to create a dynamic resource key for axaml Binding.
/// </summary>
/// <param name="key"></param>
[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class DynamicResourceKey(object key) : DynamicResourceKeyBase
{
    /// <summary>
    /// so why axaml DOES NOT SUPPORT {Binding .^} ???????
    /// </summary>
    public DynamicResourceKey Self => this;

    [Key(0)]
    protected object Key => key;

    public IObservable<object?> GetObservable() =>
        Application.Current!.Resources.GetResourceObservable(key);

    public override IDisposable Subscribe(IObserver<object?> observer) =>
        GetObservable().Subscribe(observer);

    [return: NotNullIfNotNull(nameof(key))]
    public static implicit operator DynamicResourceKey?(string? key) => key == null ? null : new DynamicResourceKey(key);

    public static string? Resolve(object key) =>
        Application.Current!.Resources.TryGetResource(key, null, out var resource) ? resource?.ToString() : key.ToString();

    public override string? ToString() => Resolve(key);
}

/// <summary>
/// Wrapper for dynamic resource keys that also holds a value.
/// </summary>
/// <param name="key"></param>
/// <param name="value"></param>
/// <typeparam name="T"></typeparam>
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