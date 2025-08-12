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
            .GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public)
            .AsValueEnumerable()
            .Where(p => p is { CanRead: true })
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
                [!SettingsSelectionItem.ItemsSourceProperty] = MakeBinding(
                    selectionAttribute.ItemsSourceBindingPath,
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
                IsNullable = false
            };
        }
        else if (itemPropertyInfo.PropertyType == typeof(bool?))
        {
            result = new SettingsBooleanItem(name)
            {
                IsNullable = true
            };
        }
        else if (itemPropertyInfo.PropertyType == typeof(string))
        {
            var attribute = attributeOwner.GetCustomAttribute<SettingsStringItemAttribute>();
            result = new SettingsStringItem(name)
            {
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
                        MakeBinding($"{itemPropertyInfo.Name}.{nameof(Customizable<>.DefaultValue)}", BindingMode.OneWay);
                }

                result = new SettingsCustomizableItem(name, bindingValueItem)
                {
                    [!SettingsCustomizableItem.ResetCommandProperty] =
                        MakeBinding($"{itemPropertyInfo.Name}.{nameof(Customizable<>.ResetCommand)}"),
                };
            }
        }
        else if (itemPropertyInfo.PropertyType == typeof(KeyboardHotkey))
        {
            result = new SettingsKeyboardHotkeyItem(name);
        }
        else if (itemPropertyInfo.PropertyType.IsEnum)
        {
            result = SettingsSelectionItem.FromEnum(itemPropertyInfo.PropertyType, name);
        }
        else if (itemPropertyInfo.PropertyType.IsAssignableTo(typeof(Control)))
        {
            var control = itemPropertyInfo.GetValue(null).NotNull<Control>();
            control.DataContext = _settings;
            result = new SettingsControlItem(name, control);
        }

        if (result is null) return null;

        result[!SettingsItem.ValueProperty] = MakeBinding(itemPropertyInfo.Name);

        if (itemPropertyInfo.GetCustomAttribute<SettingsItemAttribute>() is { } settingsItemAttribute)
        {
            if (settingsItemAttribute.IsEnabledBindingPath is { } isEnabledBindingPath)
            {
                result[!SettingsItem.IsEnabledProperty] = MakeBinding(isEnabledBindingPath, BindingMode.OneWay);
            }

            if (settingsItemAttribute.IsVisibleBindingPath is { } isVisibleBindingPath)
            {
                result[!SettingsItem.IsVisibleProperty] = MakeBinding(isVisibleBindingPath, BindingMode.OneWay);
            }
        }

        if (itemPropertyInfo.GetCustomAttribute<SettingsItemsAttribute>() is { } settingsItemsAttribute)
        {
            result.IsExpanded = settingsItemsAttribute.IsExpanded;
            result[!SettingsItem.IsExpandableProperty] = MakeBinding(
                itemPropertyInfo.Name,
                BindingMode.OneWay,
                ObjectConverters.IsNotNull);
            result.Items.AddRange(CreateItems($"{bindingPath}.{itemPropertyInfo.Name}", name, itemPropertyInfo.PropertyType));
        }

        return result;

        IBinding MakeBinding(string relativePath, BindingMode mode = BindingMode.TwoWay, IValueConverter? converter = null)
        {
            IBinding MakeSimpleBinding(string path, BindingMode bindingMode, IValueConverter? valueConverter)
            {
                string finalPath;
                if (path.StartsWith("!!")) finalPath = $"!!{bindingPath}.{path[2..]}";
                else if (path.StartsWith('!')) finalPath = $"!{bindingPath}.{path[1..]}";
                else finalPath = $"{bindingPath}.{path}";

                return new Binding(finalPath, bindingMode)
                {
                    Source = _settings,
                    Converter = valueConverter
                };
            }

            IBinding ParseExpression(string expression)
            {
                while (true)
                {
                    expression = expression.Trim();

                    // 1. Handle bracketed expressions
                    if (expression.StartsWith('(') && expression.EndsWith(')'))
                    {
                        var parenthesisCount = 0;
                        var isWrapped = true;
                        for (var i = 0; i < expression.Length - 1; i++)
                        {
                            parenthesisCount += expression[i] switch
                            {
                                '(' => 1,
                                ')' => -1,
                                _ => 0
                            };
                            if (parenthesisCount == 0)
                            {
                                isWrapped = false;
                                break;
                            }
                        }
                        if (isWrapped)
                        {
                            expression = expression.Substring(1, expression.Length - 2);
                            continue;
                        }
                    }

                    // 2. Find the highest priority operator: ||
                    var parenCountOr = 0;
                    for (var i = expression.Length - 1; i >= 0; i--)
                    {
                        var c = expression[i];
                        switch (c)
                        {
                            case ')':
                                parenCountOr++;
                                break;
                            case '(':
                                parenCountOr--;
                                break;
                            case '|' when i > 0 && expression[i - 1] == '|' && parenCountOr == 0:
                            {
                                var multiBinding = new MultiBinding
                                {
                                    Converter = BoolConverters.Or,
                                    Mode = BindingMode.OneWay
                                };
                                multiBinding.Bindings.Add(ParseExpression(expression.Substring(0, i - 1)));
                                multiBinding.Bindings.Add(ParseExpression(expression.Substring(i + 1)));
                                return multiBinding;
                            }
                        }
                    }

                    // 3. Find the next highest priority operator: &&
                    var parenCountAnd = 0;
                    for (var i = expression.Length - 1; i >= 0; i--)
                    {
                        var c = expression[i];
                        switch (c)
                        {
                            case ')':
                                parenCountAnd++;
                                break;
                            case '(':
                                parenCountAnd--;
                                break;
                            case '&' when i > 0 && expression[i - 1] == '&' && parenCountAnd == 0:
                            {
                                var multiBinding = new MultiBinding
                                {
                                    Converter = BoolConverters.And,
                                    Mode = BindingMode.OneWay
                                };
                                multiBinding.Bindings.Add(ParseExpression(expression.Substring(0, i - 1)));
                                multiBinding.Bindings.Add(ParseExpression(expression.Substring(i + 1)));
                                return multiBinding;
                            }
                        }
                    }

                    // 4. Handle logical NOT operator
                    expression = expression.Trim();
                    if (!expression.StartsWith('!')) return MakeSimpleBinding(expression, BindingMode.OneWay, null);

                    var innerExpression = expression[1..];
                    var innerBinding = ParseExpression(innerExpression);

                    var notBinding = new MultiBinding
                    {
                        // Not converter: returns true if any of the values are false
                        Converter = new FuncMultiValueConverter<bool, bool>(values => values.Any(y => !y)),
                        Mode = BindingMode.OneWay
                    };
                    notBinding.Bindings.Add(innerBinding);
                    return notBinding;
                }
            }

            var path = relativePath.Trim('.');

            if (!path.Contains("||") && !path.Contains("&&")) return MakeSimpleBinding(path, mode, converter);
            var logicalBinding = ParseExpression(path);

            if (converter != null)
            {
                return new Binding
                {
                    Source = logicalBinding,
                    Converter = converter,
                    Mode = mode
                };
            }

            return logicalBinding;
        }
    }
}

public class SettingsCategoryPageFactory(Settings settings) : IMainViewPageFactory
{
    public IEnumerable<IMainViewPage> CreatePages() => typeof(Settings)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.GetCustomAttribute<SettingsCategoryAttribute>() is not null)
        .Select((p, i) => new SettingsCategoryPage(i, settings, p));
}