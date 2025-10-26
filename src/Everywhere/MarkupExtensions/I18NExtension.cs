using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Everywhere.MarkupExtensions;

public class I18NExtension : MarkupExtension
{
    [AssignBinding]
    public required object Key { get; set; }

    [Content, AssignBinding]
    public object[] Arguments { get; set; } = [];

    public IValueConverter? Converter { get; set; }

    public object? ConverterParameter { get; set; }

    public CultureInfo? ConverterCulture { get; set; }

    /// <summary>
    /// Whether to resolve the resource key immediately. If true, the extension will return the resolved value directly.
    /// If false, it will return a binding that resolves the value at runtime.
    /// </summary>
    public bool Resolve { get; set; }

    public I18NExtension() { }

    [SetsRequiredMembers]
    public I18NExtension(object key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var target = serviceProvider.GetService<IProvideValueTarget>();

        if (Key is IBinding binding)
        {
            return new MultiBinding
            {
                Bindings = [binding],
                Converter = Resolve ? null : new BindingResolver(target) // only use BindingResolver when not resolving immediately
            };
        }

        var dynamicResourceKey = Key switch
        {
            DynamicResourceKeyBase key => key,
            _ when Arguments is { Length: > 0 } args => new FormattedDynamicResourceKey(
                Key,
                args.Select(arg => arg switch
                {
                    DynamicResourceKeyBase key => key,
                    _ => new DynamicResourceKey(arg)
                }).ToList()),
            _ => new DynamicResourceKey(Key)
        };
        return Resolve ?
            dynamicResourceKey.ToString() ?? string.Empty :
            new Binding
            {
                Path = $"{nameof(DynamicResourceKeyBase.Self)}^",
                Source = dynamicResourceKey,
                Converter = Converter,
                ConverterParameter = ConverterParameter,
                ConverterCulture = ConverterCulture,
            };
    }

    private sealed class BindingResolver : IObserver<object?>, IMultiValueConverter
    {
        private readonly WeakReference<object>? _targetObject;
        private readonly WeakReference<object>? _targetProperty;
        private IDisposable? _subscription;

        public BindingResolver(IProvideValueTarget? target)
        {
            if (target is null) return;
            _targetObject = new WeakReference<object>(target.TargetObject);
            _targetProperty = new WeakReference<object>(target.TargetProperty);
        }

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            _subscription?.Dispose();
            if (values is not [DynamicResourceKeyBase key]) return null;

            _subscription = key.Subscribe(this);
            return key.ToString(); // return resolved string immediately. If it changes, OnNext will be called to update the target.
        }

        public void OnNext(object? value)
        {
            if (_targetObject?.TryGetTarget(out var targetObject) is not true) return;
            if (_targetProperty?.TryGetTarget(out var targetProperty) is not true) return;

            if (targetProperty is not IPropertyInfo { CanSet: true } propertyInfo) return;
            propertyInfo.Set(targetObject, value);
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }
}