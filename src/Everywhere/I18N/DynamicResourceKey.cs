using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Avalonia.Controls;
using Avalonia.Reactive;
using Everywhere.Utilities;
using MessagePack;
using ZLinq;

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
    /// <summary>
    /// so why axaml DOES NOT SUPPORT {Binding .^} ???????
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public DynamicResourceKeyBase Self => this;

    public abstract IDisposable Subscribe(IObserver<object?> observer);
}

public class EmptyDynamicResourceKey : DynamicResourceKeyBase
{
    public static EmptyDynamicResourceKey Shared { get; } = new();

    public override IDisposable Subscribe(IObserver<object?> observer)
    {
        observer.OnNext(null);
        return new AnonymousDisposable(() => { });
    }

    public override string ToString() => string.Empty;
}

/// <summary>
/// This class is used to create a dynamic resource key for axaml Binding.
/// </summary>
/// <param name="key"></param>
[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class DynamicResourceKey(object key) : DynamicResourceKeyBase
{
    [Key(0)]
    public object Key { get; } = key;

    protected IObservable<object?> GetObservable() => LocaleManager.Shared.GetResourceObservable(Key, NotFoundConverter);

    private object? NotFoundConverter(object? value) => value is UnsetValueType ? Key : value;

    public override IDisposable Subscribe(IObserver<object?> observer) =>
        GetObservable().Subscribe(observer);

    [return: NotNullIfNotNull(nameof(key))]
    public static implicit operator DynamicResourceKey?(string? key) => key == null ? null : new DynamicResourceKey(key);

    public static bool Exists(object key) => LocaleManager.Shared.TryGetResource(key, null, out _);

    public static bool TryResolve(object key, [NotNullWhen(true)] out string? result)
    {
        if (LocaleManager.Shared.TryGetResource(key, null, out var resource))
        {
            result = resource?.ToString() ?? string.Empty;
            return true;
        }

        result = null;
        return false;
    }

    public static string Resolve(object key) =>
        (LocaleManager.Shared.TryGetResource(key, null, out var resource) ? resource?.ToString() : key.ToString()) ?? string.Empty;

    public override string? ToString() => Resolve(Key);
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
public partial class FormattedDynamicResourceKey(object key, params IReadOnlyList<DynamicResourceKeyBase> args) : DynamicResourceKey(key)
{
    [Key(1)]
    private IReadOnlyList<DynamicResourceKeyBase> Args { get; } = args;

    public override IDisposable Subscribe(IObserver<object?> observer)
    {
        var formatter = new AnonymousObserver<object?>(_ => observer.OnNext(ToString()));
        var disposeCollector = new DisposeCollector();
        disposeCollector.Add(GetObservable().Subscribe(formatter));
        Args.ForEach(arg => disposeCollector.Add(arg.Subscribe(formatter)));
        return disposeCollector;
    }

    public override string ToString()
    {
        var resolvedKey = Resolve(Key);
        return string.IsNullOrEmpty(resolvedKey) ?
            string.Empty :
            string.Format(resolvedKey, Args.AsValueEnumerable().Select(a => a.ToString()).ToList().AsSpan());
    }
}

/// <summary>
/// Aggregates multiple dynamic resource keys into one.
/// </summary>
/// <param name="keys"></param>
[MessagePackObject(OnlyIncludeKeyedMembers = true, AllowPrivate = true)]
public partial class AggregateDynamicResourceKey(IReadOnlyList<DynamicResourceKeyBase> keys, string separator = ", ") : DynamicResourceKeyBase
{
    [Key(0)]
    private IReadOnlyList<DynamicResourceKeyBase> Keys { get; } = keys;

    [Key(1)]
    private string Separator { get; } = separator;

    public override IDisposable Subscribe(IObserver<object?> observer)
    {
        var formatter = new AnonymousObserver<object?>(_ => observer.OnNext(ToString()));
        var disposeCollector = new DisposeCollector();
        Keys.OfType<DynamicResourceKey>().ForEach(key => disposeCollector.Add(key.Subscribe(formatter)));
        return disposeCollector;
    }

    public override string ToString()
    {
        if (Keys is not { Count: > 0 })
        {
            return string.Empty;
        }

        var resolvedKeys = new object?[Keys.Count];
        for (var i = 0; i < Keys.Count; i++)
        {
            if (Keys[i] is DynamicResourceKey dynamicKey) resolvedKeys[i] = dynamicKey.ToString();
            else resolvedKeys[i] = Keys[i];
        }

        return string.Join(Separator, resolvedKeys);
    }
}

/// <summary>
/// This can be deserialized from JSON and used as a dynamic resource key.
/// </summary>
/// <remarks>
/// JSON example:
/// "Key": {
///     "default": "Hello, World!",
///     "zh-hans": "你好，世界！",
///     "jp": "こんにちは、世界！"
/// }
/// </remarks>
[Serializable]
public class JsonDynamicResourceKey : Dictionary<string, string>, IObservable<object?>
{
    public IDisposable Subscribe(IObserver<object?> observer)
    {
        LocaleManager.LocaleChanged += HandleLocaleChanged;
        PostValue(LocaleManager.CurrentLocale);

        return new AnonymousDisposable(() =>
        {
            LocaleManager.LocaleChanged -= HandleLocaleChanged;
        });

        void PostValue(string locale)
        {
            if (TryGetValue(locale, out var value))
            {
                observer.OnNext(value);
            }
            else if (TryGetValue("default", out var defaultValue))
            {
                observer.OnNext(defaultValue);
            }
            else
            {
                observer.OnNext(Values.FirstOrDefault());
            }
        }

        void HandleLocaleChanged(string? oldLocale, string newLocale)
        {
            PostValue(newLocale);
        }
    }
}

[AttributeUsage(AttributeTargets.All)]
public class DynamicResourceKeyAttribute(string headerKey, string? descriptionKey = null) : Attribute
{
    public string HeaderKey { get; } = headerKey;

    /// <summary>
    /// The optional description key.
    /// </summary>
    public string? DescriptionKey { get; } = descriptionKey;
}