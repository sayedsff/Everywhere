using System.Reflection;
using Everywhere.Attributes;
using Everywhere.Models;

namespace Everywhere.ViewModels;

public class SettingsPageViewModel : ReactiveViewModelBase
{
    public IReadOnlyList<SettingsItemGroup> Groups { get; }

    public SettingsPageViewModel(Settings settings)
    {
        Groups = typeof(Settings)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType.IsAssignableTo(typeof(SettingsBase)))
            .Select(
                p =>
                {
                    var group = p.GetValue(settings);
                    return new SettingsItemGroup(
                        p.Name,
                        p.PropertyType
                            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                            .Where(pp => pp is { CanRead: true, CanWrite: true })
                            .Select<PropertyInfo, SettingsItemBase>(
                                pp =>
                                {
                                    if (pp.GetCustomAttribute<SettingsSelectionItemAttribute>() is { } selectionAttribute)
                                    {
                                        var itemsSourceProperty = p.PropertyType
                                            .GetProperty(
                                                selectionAttribute.PropertyName,
                                                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                                            .NotNull($"{selectionAttribute.PropertyName} Property for ItemsSource is not found");
                                        if (!itemsSourceProperty.PropertyType.IsGenericType ||
                                            !itemsSourceProperty.PropertyType.GetGenericTypeDefinition().IsAssignableTo(typeof(IEnumerable<>)) ||
                                            itemsSourceProperty.PropertyType.GenericTypeArguments.Length != 1 ||
                                            itemsSourceProperty.PropertyType.GenericTypeArguments[0] != pp.PropertyType)
                                        {
                                            throw new NotSupportedException(
                                                $"{selectionAttribute.PropertyName} Property for ItemsSource has an invalid type");
                                        }
                                        if (itemsSourceProperty.GetMethod is not { } itemsSourceGetter)
                                        {
                                            throw new NotSupportedException(
                                                $"{selectionAttribute.PropertyName} Property for ItemsSource is not readable");
                                        }
                                        return new SettingsSelectionItem(
                                            $"{p.Name}_{pp.Name}",
                                            () => pp.GetValue(group),
                                            v => pp.SetValue(group, v),
                                            () =>
                                            {
                                                var itemsSource = itemsSourceGetter.IsStatic ?
                                                    itemsSourceProperty.GetValue(null) :
                                                    itemsSourceProperty.GetValue(group);
                                                return itemsSource.NotNull<IEnumerable>().Cast<object?>().Select(
                                                    x => new DynamicResourceKeyWrapper<object?>($"SettingsSelectionItem_{p.Name}_{pp.Name}_{x}", x));
                                            });
                                    }

                                    if (pp.PropertyType == typeof(bool))
                                    {
                                        return new SettingsBooleanItem(
                                            $"{p.Name}_{pp.Name}",
                                            () => pp.GetValue(group).To<bool>(),
                                            v => pp.SetValue(group, v),
                                            false);
                                    }

                                    if (pp.PropertyType == typeof(bool?))
                                    {
                                        return new SettingsBooleanItem(
                                            $"{p.Name}_{pp.Name}",
                                            () => pp.GetValue(group).To<bool?>(),
                                            v => pp.SetValue(group, v),
                                            true);
                                    }

                                    if (pp.PropertyType == typeof(string))
                                    {
                                        var attribute = pp.GetCustomAttribute<SettingsStringItemAttribute>();
                                        return new SettingsStringItem(
                                            $"{p.Name}_{pp.Name}",
                                            () => pp.GetValue(group).NotNull<string>(),
                                            v => pp.SetValue(group, v),
                                            attribute?.Watermark,
                                            attribute?.MaxLength ?? int.MaxValue,
                                            attribute?.IsMultiline ?? false,
                                            attribute?.IsPassword ?? false);
                                    }

                                    if (pp.PropertyType == typeof(int))
                                    {
                                        var attribute = pp.GetCustomAttribute<SettingsIntegerItemAttribute>();
                                        return new SettingsIntegerItem(
                                            $"{p.Name}_{pp.Name}",
                                            () => pp.GetValue(group).To<int>(),
                                            v => pp.SetValue(group, v),
                                            attribute?.Min ?? int.MinValue,
                                            attribute?.Max ?? int.MaxValue);
                                    }

                                    if (pp.PropertyType == typeof(double))
                                    {
                                        var attribute = pp.GetCustomAttribute<SettingsDoubleItemAttribute>();
                                        return new SettingsDoubleItem(
                                            $"{p.Name}_{pp.Name}",
                                            () => pp.GetValue(group).To<double>(),
                                            v => pp.SetValue(group, v),
                                            attribute?.Min ?? double.NegativeInfinity,
                                            attribute?.Max ?? double.PositiveInfinity,
                                            attribute?.Step ?? 0.1d);
                                    }

                                    if (pp.PropertyType == typeof(Hotkey))
                                    {
                                        return new SettingsHotkeyItem(
                                            $"{p.Name}_{pp.Name}",
                                            () => pp.GetValue(group).To<Hotkey>(),
                                            v => pp.SetValue(group, v));
                                    }

                                    throw new NotSupportedException();
                                })
                            .ToReadOnlyList());
                }).ToReadOnlyList();
    }
}