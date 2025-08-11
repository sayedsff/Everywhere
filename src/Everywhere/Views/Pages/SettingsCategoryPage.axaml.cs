using System.Reflection;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Everywhere.Attributes;
using Everywhere.Models;
using Lucide.Avalonia;
using ZLinq;

namespace Everywhere.Views.Pages;

/// <summary>
/// Represents a settings category page that displays a list of settings items.
/// It dynamically creates settings items based on the properties of a specified settings category.
/// </summary>
public partial class SettingsCategoryPage : UserControl, IMainViewPage
{
    public int Index { get; }

    public DynamicResourceKey Title { get; }

    public LucideIconKind Icon { get; }

    public static readonly DirectProperty<SettingsCategoryPage, SettingsItem[]?> ItemsProperty =
        AvaloniaProperty.RegisterDirect<SettingsCategoryPage, SettingsItem[]?>(nameof(Items), o => o.Items);

    public SettingsItem[]? Items { get; private set; }

    private readonly Settings _settings;
    private readonly PropertyInfo _categoryPropertyInfo;

    public SettingsCategoryPage(int index, Settings settings, PropertyInfo categoryPropertyInfo)
    {
        Index = index;
        _settings = settings;
        _categoryPropertyInfo = categoryPropertyInfo;

        var settingsCategoryAttribute = categoryPropertyInfo.GetCustomAttribute<SettingsCategoryAttribute>().NotNull();
        Title = new DynamicResourceKey($"SettingsCategory_{settingsCategoryAttribute.Header}_Header");
        Icon = settingsCategoryAttribute.Icon;

        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (Items is not null) return;
        Items = CreateItems(_categoryPropertyInfo.Name, _categoryPropertyInfo.Name, _categoryPropertyInfo.PropertyType);
        RaisePropertyChanged(ItemsProperty, null, Items);
    }

    private SettingsItem[] CreateItems(string bindingPath, string groupName, Type ownerType)
    {
        return ownerType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .AsValueEnumerable()
            .Where(p => p is { CanRead: true, CanWrite: true })
            .Where(p => p.GetCustomAttribute<HiddenSettingsItemAttribute>() is null)
            .Select(p => CreateItem(bindingPath, groupName, p))
            .OfType<SettingsItem>()
            .ToArray();
    }

    private SettingsItem? CreateItem(
        string bindingPath,
        string ownerName,
        PropertyInfo itemPropertyInfo,
        MemberInfo? attributeOwner = null)
    {
        SettingsItem? result = null;

        attributeOwner ??= itemPropertyInfo;
        var name = $"{ownerName}_{itemPropertyInfo.Name}";

        if (attributeOwner.GetCustomAttribute<SettingsSelectionItemAttribute>() is { } selectionAttribute)
        {
            result = new SettingsSelectionItem(name)
            {
                [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                [!SettingsSelectionItem.ItemsSourceProperty] = MakeBinding(
                    $"{bindingPath}.{selectionAttribute.ItemsSource}",
                    BindingMode.OneWay,
                    new FuncValueConverter<object?, IEnumerable<SettingsSelectionItem.Item>?>(x =>
                    {
                        if (x is not IEnumerable enumerable) return null;

                        if (!selectionAttribute.I18N)
                        {
                            return enumerable
                                .AsValueEnumerable()
                                .Select(k => new SettingsSelectionItem.Item(new DirectResourceKey(k), k))
                                .ToArray();
                        }

                        var keyPrefix = $"SettingsSelectionItem_{bindingPath.Replace('.', '_')}_{itemPropertyInfo.Name}";
                        return enumerable
                            .AsValueEnumerable()
                            .Select(k => new SettingsSelectionItem.Item(new DynamicResourceKey($"{keyPrefix}_{k}"), k))
                            .ToArray();
                    }))
            };
        }
        else if (itemPropertyInfo.PropertyType == typeof(bool))
        {
            result = new SettingsBooleanItem(name)
            {
                [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                IsNullable = false
            };
        }
        else if (itemPropertyInfo.PropertyType == typeof(bool?))
        {
            result = new SettingsBooleanItem(name)
            {
                [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                IsNullable = true
            };
        }
        else if (itemPropertyInfo.PropertyType == typeof(string))
        {
            var attribute = attributeOwner.GetCustomAttribute<SettingsStringItemAttribute>();
            result = new SettingsStringItem(name)
            {
                [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                Watermark = attribute?.Watermark,
                MaxLength = attribute?.MaxLength ?? int.MaxValue,
                IsMultiline = attribute?.IsMultiline ?? false,
                PasswordChar = (attribute?.IsPassword ?? false) ? '*' : '\0'
            };
        }
        else if (itemPropertyInfo.PropertyType == typeof(int))
        {
            var attribute = attributeOwner.GetCustomAttribute<SettingsIntegerItemAttribute>();
            result = new SettingsIntegerItem(name)
            {
                [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                MinValue = attribute?.Min ?? int.MinValue,
                MaxValue = attribute?.Max ?? int.MaxValue,
                IsSliderVisible = attribute?.IsSliderVisible ?? true
            };
        }
        else if (itemPropertyInfo.PropertyType == typeof(double))
        {
            var attribute = attributeOwner.GetCustomAttribute<SettingsDoubleItemAttribute>();
            result = new SettingsDoubleItem(name)
            {
                [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
                MinValue = attribute?.Min ?? double.NegativeInfinity,
                MaxValue = attribute?.Max ?? double.PositiveInfinity,
                Step = attribute?.Step ?? 0.1d,
                IsSliderVisible = attribute?.IsSliderVisible ?? true
            };
        }
        else if (itemPropertyInfo.PropertyType.IsGenericType &&
                 itemPropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Customizable<>))
        {
            var bindingValueProperty = itemPropertyInfo.PropertyType.GetProperty(nameof(Customizable<>.BindableValue)).NotNull();
            var bindingValueItem = CreateItem(
                $"{bindingPath}.{itemPropertyInfo.Name}",
                name,
                bindingValueProperty,
                itemPropertyInfo);
            if (bindingValueItem is null)
            {
                result = null;
            }
            else
            {
                if (bindingValueItem is SettingsStringItem settingsStringItem)
                {
                    settingsStringItem[!SettingsStringItem.WatermarkProperty] =
                        MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}.{nameof(Customizable<>.DefaultValue)}", BindingMode.OneWay);
                }

                result = new SettingsCustomizableItem(name, bindingValueItem)
                {
                    [!SettingsCustomizableItem.ResetCommandProperty] =
                        MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}.{nameof(Customizable<>.ResetCommand)}"),
                };
            }
        }
        else if (itemPropertyInfo.PropertyType == typeof(KeyboardHotkey))
        {
            result = new SettingsKeyboardHotkeyItem(name)
            {
                [!SettingsItem.ValueProperty] = MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"),
            };
        }
        else if (itemPropertyInfo.PropertyType.IsEnum)
        {
            result = SettingsSelectionItem.FromEnum(itemPropertyInfo.PropertyType, name, MakeBinding($"{bindingPath}.{itemPropertyInfo.Name}"));
        }

        if (result is null) return null;

        if (itemPropertyInfo.GetCustomAttribute<SettingsItemsAttribute>() is { } settingsItemsAttribute)
        {
            result.IsExpanded = settingsItemsAttribute.IsExpanded;
            result[!SettingsItem.IsExpandableProperty] = MakeBinding(
                $"{bindingPath}.{itemPropertyInfo.Name}",
                BindingMode.OneWay,
                ObjectConverters.IsNotNull);
            result.Items.AddRange(CreateItems($"{bindingPath}.{itemPropertyInfo.Name}", name, itemPropertyInfo.PropertyType));
        }

        return result;
    }

    private Binding MakeBinding(string path, BindingMode mode = BindingMode.TwoWay, IValueConverter? converter = null) => new(path, mode)
    {
        Source = _settings,
        Converter = converter
    };
}

public class SettingsCategoryPageFactory(Settings settings) : IMainViewPageFactory
{
    public IEnumerable<IMainViewPage> CreatePages() => typeof(Settings)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.GetCustomAttribute<SettingsCategoryAttribute>() is not null)
        .Select((p, i) => new SettingsCategoryPage(i, settings, p));
}