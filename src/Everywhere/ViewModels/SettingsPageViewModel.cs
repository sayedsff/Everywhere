using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Attributes;
using Everywhere.Avalonia;
using Everywhere.Models;

namespace Everywhere.ViewModels;

public class SettingsItemDataTemplate : IDataTemplate
{
    public required IDataTemplate BooleanSettingsItemTemplate { get; init; }
    public required IDataTemplate StringSettingsItemTemplate { get; init; }
    public required IDataTemplate FloatSettingsItemTemplate { get; init; }
    public required IDataTemplate IntegerSettingsItemTemplate { get; init; }
    public required IDataTemplate SelectionSettingsItemTemplate { get; init; }

    public bool Match(object? data) => data is SettingsPageViewModel.SettingsItem;

    public Control? Build(object? param)
    {
        if (param is not SettingsPageViewModel.SettingsItem item) throw new InvalidOperationException($"Invalid parameter type {param?.GetType()}");
        if (param is SettingsPageViewModel.SelectionSettingsItem) return SelectionSettingsItemTemplate.Build(param);
        if (item.ValueType == typeof(bool)) return BooleanSettingsItemTemplate.Build(param);
        if (item.ValueType == typeof(string)) return StringSettingsItemTemplate.Build(param);
        if (item.ValueType == typeof(float)) return FloatSettingsItemTemplate.Build(param);
        if (item.ValueType == typeof(int)) return IntegerSettingsItemTemplate.Build(param);
        throw new InvalidOperationException($"Unsupported type {item.ValueType}");
    }
}

public class SettingsPageViewModel : ReactiveViewModelBase
{
    public class SettingsItem(string name, Type valueType, Func<object?> getter, Action<object?> setter) : ObservableObject
    {
        public DynamicResourceKey HeaderKey => $"Settings_{name}_Header";

        public DynamicResourceKey DescriptionKey => $"Settings_{name}_Description";

        public Type ValueType { get; } = valueType;

        public object? Value
        {
            get => getter();
            set => setter(value);
        }

        public void NotifyValueChanged()
        {
            OnPropertyChanged(nameof(Value));
        }
    }

    public class SelectionSettingsItem(
        string name,
        Type valueType,
        Func<object?> getter,
        Action<object?> setter,
        Func<IEnumerable<string>> itemsGetter
    ) : SettingsItem(name, valueType, getter, setter)
    {
        public IEnumerable<string> Items => itemsGetter();
    }

    public IReadOnlyList<SettingsItem> Items { get; }

    public SettingsPageViewModel(Settings settings)
    {
        Items = typeof(Settings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p is { CanRead: true, CanWrite: true })
            .Select(p =>
            {
                if (p.GetCustomAttribute<SelectionSettingsItemAttribute>() is { PropertyName: { } propertyName })
                {
                    var itemsSourceProperty = typeof(Settings).GetProperty(propertyName);
                    if (itemsSourceProperty is null)
                    {
                        throw new InvalidOperationException($"Property {propertyName} not found");
                    }
                    if (!itemsSourceProperty.PropertyType.IsAssignableTo(typeof(IEnumerable<string>)))
                    {
                        throw new InvalidOperationException($"Property {propertyName} must be IEnumerable<string>");
                    }

                    return new SelectionSettingsItem(
                        p.Name,
                        p.PropertyType,
                        () => p.GetValue(settings),
                        v => p.SetValue(settings, v),
                        () => (IEnumerable<string>)itemsSourceProperty.GetValue(settings)!);
                }

                return new SettingsItem(
                    p.Name,
                    p.PropertyType,
                    () => p.GetValue(settings),
                    v => p.SetValue(settings, v));
            })
            .ToReadOnlyList();
    }
}