using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Everywhere.ValueConverters;

public class PlacementToCornerRadiusConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [double small, CornerRadius medium, double large, PlacementMode mode, bool isExpanded])
            throw new ArgumentOutOfRangeException(nameof(values));

        if (isExpanded) return medium;

        return mode switch
        {
            PlacementMode.TopEdgeAlignedLeft => new CornerRadius(large, large, large, small),
            PlacementMode.TopEdgeAlignedRight => new CornerRadius(large, large, small, large),
            PlacementMode.BottomEdgeAlignedLeft => new CornerRadius(small, large, large, large),
            PlacementMode.BottomEdgeAlignedRight => new CornerRadius(large, small, large, large),
            PlacementMode.LeftEdgeAlignedTop => new CornerRadius(large, small, large, large),
            PlacementMode.LeftEdgeAlignedBottom => new CornerRadius(large, large, small, large),
            PlacementMode.RightEdgeAlignedTop => new CornerRadius(small, large, large, large),
            PlacementMode.RightEdgeAlignedBottom => new CornerRadius(large, large, large, small),
            _ => new CornerRadius(0)
        };
    }
}