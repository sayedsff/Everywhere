using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Everywhere.AttachedProperties;

/// <summary>
/// <see cref="Control.DataTemplates"/> says they are 'Gets or sets'.
/// But maybe I'm blind, I cannot see the set method.
/// So I can only use this AttachedProperty to set it.
/// </summary>
public class DataTemplatesAttach : AvaloniaObject
{
    public static readonly AttachedProperty<DataTemplates> DataTemplatesProperty =
        AvaloniaProperty.RegisterAttached<DataTemplatesAttach, Control, DataTemplates>("DataTemplates");

    public static void SetDataTemplates(Control obj, DataTemplates value) => obj.SetValue(DataTemplatesProperty, value);

    public static DataTemplates GetDataTemplates(Control obj) => obj.GetValue(DataTemplatesProperty);

    static DataTemplatesAttach()
    {
        DataTemplatesProperty.Changed.AddClassHandler<Control>(HandleDataTemplatesChanged);
    }

    private static void HandleDataTemplatesChanged(Control sender, AvaloniaPropertyChangedEventArgs args)
    {
        sender.DataTemplates.Clear();
        if (args.NewValue is DataTemplates dataTemplates) sender.DataTemplates.AddRange(dataTemplates);
    }
}