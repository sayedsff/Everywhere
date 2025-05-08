using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Avalonia;

namespace Everywhere.Models;

public class SettingsItemGroup(string name, IReadOnlyList<SettingsItemBase> items)
{
    public DynamicResourceKey HeaderKey => $"SettingsGroup_{name}_Header";
    public IReadOnlyList<SettingsItemBase> Items => items;
}

public abstract class SettingsItemBase(string name) : ObservableObject
{
    public DynamicResourceKey HeaderKey => $"Settings_{name}_Header";

    public DynamicResourceKey DescriptionKey => $"Settings_{name}_Description";
}

public abstract class SettingsItemBase<T>(string name, Func<T> getter, Action<T> setter) : SettingsItemBase(name)
{
    public T Value
    {
        get => getter();
        set
        {
            setter(value);
            OnPropertyChanged();
        }
    }

    public void NotifyValueChanged()
    {
        OnPropertyChanged(nameof(Value));
    }
}

public class SettingsBooleanItem(
    string name,
    Func<bool?> getter,
    Action<bool?> setter,
    bool canBeNull
) : SettingsItemBase<bool?>(name, getter, setter)
{
    public bool CanBeNull => canBeNull;
}

public class SettingsStringItem(
    string name,
    Func<string> getter,
    Action<string> setter,
    string? watermark,
    int maxLength,
    bool isMultiline,
    bool isPassword
) : SettingsItemBase<string>(name, getter, setter)
{
    public string? Watermark => watermark;

    public int MaxLength => maxLength;

    public bool IsMultiline => isMultiline;

    public char PasswordChar => isPassword ? '*' : '\0';
}

public class SettingsIntegerItem(
    string name,
    Func<int> getter,
    Action<int> setter,
    int minValue,
    int maxValue
) : SettingsItemBase<int>(name, getter, setter)
{
    public int MinValue => minValue;

    public int MaxValue => maxValue;
}

public class SettingsDoubleItem(
    string name,
    Func<double> getter,
    Action<double> setter,
    double minValue,
    double maxValue,
    double step
) : SettingsItemBase<double>(name, getter, setter)
{
    public double MinValue => minValue;

    public double MaxValue => maxValue;

    public double Step => step;
}

public class SettingsSelectionItem(
    string name,
    Func<object?> getter,
    Action<object?> setter,
    Func<IEnumerable<DynamicResourceKeyWrapper<object?>>> itemsSource
) : SettingsItemBase<object?>(name, getter, setter)
{
    public IEnumerable<DynamicResourceKeyWrapper<object?>> ItemsSource => itemsSource();

    /// <summary>
    /// ComboBox.SelectedValue is not working correctly!
    /// </summary>
    public DynamicResourceKeyWrapper<object?> SelectedItem
    {
        get => ItemsSource.First(i => Equals(i.Value, Value));
        set => Value = value.Value;
    }
}

public class SettingsHotkeyItem(
    string name,
    Func<Hotkey> getter,
    Action<Hotkey> setter
) : SettingsItemBase<Hotkey>(name, getter, setter)
{
}