using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Reactive;
using Everywhere.Utils;
using MessagePack;

namespace Everywhere.I18N;

/// <summary>
/// MessagePack serializable base class for dynamic resource keys. Make them happy.
/// </summary>
[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
[Union(0, typeof(DynamicResourceKey))]
[Union(1, typeof(DirectResourceKey))]
[Union(2, typeof(FormattedDynamicResourceKey))]
[Union(3, typeof(AggregateDynamicResourceKey))]
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
    public static DynamicResourceKey Empty { get; } = new(string.Empty);

    /// <summary>
    /// so why axaml DOES NOT SUPPORT {Binding .^} ???????
    /// </summary>
    public DynamicResourceKey Self => this;

    [Key(0)]
    protected object Key { get; } = key;

    protected IObservable<object?> GetObservable() =>
        Application.Current!.Resources.GetResourceObservable(Key, NotFoundConverter);

    private object? NotFoundConverter(object? value) => value is UnsetValueType ? Key : value;

    public override IDisposable Subscribe(IObserver<object?> observer) =>
        GetObservable().Subscribe(observer);

    [return: NotNullIfNotNull(nameof(key))]
    public static implicit operator DynamicResourceKey?(string? key) => key == null ? null : new DynamicResourceKey(key);

    public static string? Resolve(object key) =>
        Application.Current!.Resources.TryGetResource(key, null, out var resource) ? resource?.ToString() : key.ToString();

    public override string? ToString() => Resolve(Key);
}

/// <summary>
/// Wrapper for dynamic resource keys that also holds a value.
/// </summary>
/// <param name="key"></param>
/// <param name="value"></param>
/// <typeparam name="T"></typeparam>
public class DynamicResourceKeyWrapper<T>(object key, T value) : DynamicResourceKey(key)
{
    public T Value { get; } = value;

    public override bool Equals(object? obj)
    {
        return obj is DynamicResourceKeyWrapper<T> other && EqualityComparer<T>.Default.Equals(Value, other.Value);
    }

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}

/// <summary>
/// Directly wraps a raw string for use in axaml.
/// This is useful for cases where you want to use a string as a resource key without any formatting or dynamic behavior.
/// </summary>
/// <param name="key"></param>
[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class DirectResourceKey(object key) : DynamicResourceKey(key)
{
    private static readonly IDisposable NullDisposable = new AnonymousDisposable(() => { });

    public override IDisposable Subscribe(IObserver<object?> observer)
    {
        observer.OnNext(Key);
        return NullDisposable;
    }

    public override string? ToString() => Key.ToString();
}

/// <summary>
/// This class is used to create a dynamic resource key for axaml Binding with formatted arguments.
/// It first resolves the resource key, then formats it with the provided arguments.
/// Arguments will be also resolved if they are dynamic resource keys.
/// </summary>
/// <param name="key"></param>
/// <param name="args"></param>
[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class FormattedDynamicResourceKey(object key, params DynamicResourceKeyBase[] args) : DynamicResourceKey(key)
{
    [Key(1)]
    private DynamicResourceKeyBase[] Args { get; } = args;

    public override IDisposable Subscribe(IObserver<object?> observer)
    {
        var formatter = new AnonymousObserver<object?>(_ => observer.OnNext(ToString()));
        var disposeCollector = new DisposeCollector();
        disposeCollector.Add(GetObservable().Subscribe(formatter));
        Args.OfType<DynamicResourceKey>().ForEach(arg => disposeCollector.Add(arg.Subscribe(formatter)));
        return disposeCollector;
    }

    public override string ToString()
    {
        var resolvedKey = Resolve(Key);
        if (string.IsNullOrEmpty(resolvedKey))
        {
            return string.Empty;
        }

        var resolvedArgs = new object?[Args.Length];
        for (var i = 0; i < Args.Length; i++)
        {
            if (Args[i] is DynamicResourceKey dynamicKey) resolvedArgs[i] = dynamicKey.ToString();
            else resolvedArgs[i] = Args[i];
        }

        return string.Format(resolvedKey, resolvedArgs);
    }
}

[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class AggregateDynamicResourceKey(params DynamicResourceKeyBase[] keys) : DynamicResourceKeyBase
{
    [Key(0)]
    private DynamicResourceKeyBase[] Keys { get; } = keys;

    public override IDisposable Subscribe(IObserver<object?> observer)
    {
        var formatter = new AnonymousObserver<object?>(_ => observer.OnNext(ToString()));
        var disposeCollector = new DisposeCollector();
        Keys.OfType<DynamicResourceKey>().ForEach(key => disposeCollector.Add(key.Subscribe(formatter)));
        return disposeCollector;
    }

    public override string ToString()
    {
        if (Keys is not { Length: > 0 })
        {
            return string.Empty;
        }

        var resolvedKeys = new object?[Keys.Length];
        for (var i = 0; i < Keys.Length; i++)
        {
            if (Keys[i] is DynamicResourceKey dynamicKey) resolvedKeys[i] = dynamicKey.ToString();
            else resolvedKeys[i] = Keys[i];
        }

        return string.Join(", ", resolvedKeys);
    }
}

[AttributeUsage(AttributeTargets.All)]
public class DynamicResourceKeyAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}