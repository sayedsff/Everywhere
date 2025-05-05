using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using Everywhere.Avalonia;
using Everywhere.I18N;
using ObservableCollections;

namespace Everywhere.ViewModels;

public class SettingsPageViewModel : ReactiveViewModelBase
{
    public abstract class SettingsItemBase : ObservableObject
    {
        public abstract DynamicResourceKey HeaderKey { get; }

        public abstract DynamicResourceKey DescriptionKey { get; }
    }

    public class SettingsItem<T>(string name, Func<T> getter, Action<T> setter) : SettingsItemBase
    {
        public override DynamicResourceKey HeaderKey => name + "_Header";
        public override DynamicResourceKey DescriptionKey => name + "_Description";

        public T Value
        {
            get => getter();
            set => setter(value);
        }

        public void NotifyValueChanged()
        {
            OnPropertyChanged(nameof(Value));
        }
    }

    public class SettingsSelectionItem(string name, Func<string> getter, Action<string> setter, Func<IEnumerable<string>> itemsGetter) :
        SettingsItem<string>(name, getter, setter)
    {
        public IEnumerable<string> Items => itemsGetter();
    }

    [field: AllowNull, MaybeNull]
    public NotifyCollectionChangedSynchronizedViewList<object> Items =>
        field ??= items.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    private readonly ObservableList<object> items = [];

    protected internal override Task ViewLoaded(CancellationToken cancellationToken)
    {
        items.Reset(
        [
            new SettingsSelectionItem(
                "Settings_Language",
                () => LocaleManager.CurrentLocale,
                value => LocaleManager.CurrentLocale = value,
                () => LocaleManager.AvailableLocaleNames)
        ]);
        return base.ViewLoaded(cancellationToken);
    }
}