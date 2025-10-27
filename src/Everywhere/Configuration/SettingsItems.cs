using System.Reflection;
using Avalonia.Collections;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ZLinq;

namespace Everywhere.Configuration;

/// <summary>
/// Factory class for creating settings items based on the properties of a target object.
/// </summary>
public class SettingsItems : AvaloniaList<SettingsItem>
{
    /// <summary>
    /// The owner object for which the settings items are created.
    /// </summary>
    /// TODO: Support setting owner and changing settings dynamically.
    public object Owner { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsItems"/> class for the specified settings category.
    /// </summary>
    /// <param name="category"></param>
    public SettingsItems(SettingsCategory category) : this(category, CreateItems(category, string.Empty, category.Header, category.GetType())) { }

    private SettingsItems(object owner, IEnumerable<SettingsItem> collection) : base(collection)
    {
        Owner = owner;
    }

    /// <summary>
    /// Creates settings items for the specified target object.
    /// </summary>
    /// <param name="target"></param>
    /// <param name="groupName"></param>
    /// <returns></returns>
    public static SettingsItems CreateForObject<T>(T target, string groupName) where T : class
    {
        return new SettingsItems(target, CreateItems(target, string.Empty, groupName, typeof(T)));
    }

    private static IEnumerable<SettingsItem> CreateItems(object target, string bindingPath, string groupName, Type ownerType)
    {
        return ownerType
            .GetProperties(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p is { CanRead: true })
            .Where(p => p.GetCustomAttribute<HiddenSettingsItemAttribute>() is null)
            .Select(p => CreateItem(target, bindingPath, groupName, p))
            .OfType<SettingsItem>();
    }

    private static SettingsItem? CreateItem(
        object target,
        string bindingPath,
        string ownerName,
        PropertyInfo itemPropertyInfo,
        MemberInfo? attributeOwner = null)
    {
        SettingsItem? result;

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

                        IDataTemplate? contentTemplate;
                        if (selectionAttribute.DataTemplateKey is { } dataTemplateKey &&
                            Application.Current?.Resources.TryGetResource(dataTemplateKey, null, out var resource) is true)
                        {
                            contentTemplate = resource as IDataTemplate;
                        }
                        else
                        {
                            contentTemplate = null;
                        }

                        if (!selectionAttribute.I18N)
                        {
                            return enumerable
                                .AsValueEnumerable()
                                .Select(k => new SettingsSelectionItem.Item(new DirectResourceKey(k), k, contentTemplate))
                                .ToList();
                        }

                        var keyPrefix = $"{nameof(SettingsSelectionItem)}_{ownerName}{bindingPath.Replace('.', '_')}_{itemPropertyInfo.Name}";
                        return enumerable
                            .AsValueEnumerable()
                            .Select(k => new SettingsSelectionItem.Item(new DynamicResourceKey($"{keyPrefix}_{k}"), k, contentTemplate))
                            .ToList();
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
                TextWrapping = attribute?.IsMultiline is true ? TextWrapping.Wrap : TextWrapping.NoWrap,
                PasswordChar = (attribute?.IsPassword ?? false) ? '*' : '\0',
                Height = attribute?.Height ?? double.NaN
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
                target,
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
        else if (itemPropertyInfo.PropertyType.IsEnum)
        {
            result = SettingsSelectionItem.FromEnum(itemPropertyInfo.PropertyType, name);
        }
        else if (itemPropertyInfo.PropertyType.IsAssignableTo(typeof(ISettingsControl)))
        {
            var settingsControl = itemPropertyInfo.GetMethod is { IsStatic: true } ?
                itemPropertyInfo.GetValue(null).NotNull<ISettingsControl>() :
                itemPropertyInfo.GetValue(target).NotNull<ISettingsControl>();
            var control = settingsControl.CreateControl();
            control.DataContext = target;
            result = new SettingsControlItem(name, control);
        }
        else
        {
            result = SettingsTypedItem.TryCreate(itemPropertyInfo.PropertyType, name);
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
            result.Items.AddRange(CreateItems(target, $"{bindingPath}.{itemPropertyInfo.Name}", name, itemPropertyInfo.PropertyType));
        }

        return result;

        IBinding MakeBinding(string relativePath, BindingMode mode = BindingMode.TwoWay, IValueConverter? converter = null)
        {
            IBinding MakeSimpleBinding(string path, BindingMode bindingMode, IValueConverter? valueConverter)
            {
                string finalPath;
                var trimmedPath = $"{bindingPath}.".TrimStart('.');
                if (path.StartsWith("!!")) finalPath = $"!!{trimmedPath}{path[2..]}";
                else if (path.StartsWith('!')) finalPath = $"!{trimmedPath}{path[1..]}";
                else finalPath = $"{trimmedPath}{path}";

                return new Binding(finalPath, bindingMode)
                {
                    Source = target,
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
                            if (parenthesisCount != 0) continue;
                            isWrapped = false;
                            break;
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
                                multiBinding.Bindings.Add(ParseExpression(expression[..(i - 1)]));
                                multiBinding.Bindings.Add(ParseExpression(expression[(i + 1)..]));
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
                                multiBinding.Bindings.Add(ParseExpression(expression[..(i - 1)]));
                                multiBinding.Bindings.Add(ParseExpression(expression[(i + 1)..]));
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