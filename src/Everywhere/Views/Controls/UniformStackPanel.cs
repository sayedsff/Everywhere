using Avalonia.Controls;
using Avalonia.Layout;

namespace Everywhere.Views;

public class UniformStackPanel : StackPanel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var orientation = Orientation;
        double totalWidth = 0, totalHeight = 0;

        var effectiveChildrenCount = 0;
        foreach (var child in Children)
        {
            var childConstraint = orientation == Orientation.Horizontal
                ? new Size(double.PositiveInfinity, availableSize.Height)
                : new Size(availableSize.Width, double.PositiveInfinity);

            child.Measure(childConstraint);
            var size = orientation == Orientation.Horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
            if (size > 0) effectiveChildrenCount++;
        }

        if (effectiveChildrenCount == 0) return new Size();

        var spacing = Spacing;
        var totalSpacing = spacing * (effectiveChildrenCount - 1);
        var availableLengthForChildren = orientation == Orientation.Horizontal
            ? availableSize.Width - totalSpacing
            : availableSize.Height - totalSpacing;

        var uniformLength = availableLengthForChildren / effectiveChildrenCount;

        foreach (var child in Children)
        {
            var childConstraint = orientation == Orientation.Horizontal
                ? new Size(uniformLength, availableSize.Height)
                : new Size(availableSize.Width, uniformLength);

            child.Measure(childConstraint);
            var childSize = child.DesiredSize;
            var size = orientation == Orientation.Horizontal ? childSize.Width : childSize.Height;

            if (size > 0)
            {
                if (orientation == Orientation.Horizontal)
                {
                    totalWidth += uniformLength;
                    totalHeight = double.IsFinite(availableSize.Height)
                        ? availableSize.Height
                        : Math.Max(totalHeight, childSize.Height);
                }
                else
                {
                    totalHeight += uniformLength;
                    totalWidth = double.IsFinite(availableSize.Width)
                        ? availableSize.Width
                        : Math.Max(totalWidth, childSize.Width);
                }
            }
        }

        if (orientation == Orientation.Horizontal) totalWidth += totalSpacing;
        else totalHeight += totalSpacing;

        return new Size(totalWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var orientation = Orientation;
        var spacing = Spacing;
        var offset = 0.0;

        var effectiveChildrenCount = Children.Count(child =>
            orientation == Orientation.Horizontal
                ? child.DesiredSize.Width > 0
                : child.DesiredSize.Height > 0);

        if (effectiveChildrenCount == 0)
            return finalSize;

        var totalSpacing = spacing * (effectiveChildrenCount - 1);
        var availableLengthForChildren = orientation == Orientation.Horizontal
            ? finalSize.Width - totalSpacing
            : finalSize.Height - totalSpacing;

        var uniformLength = availableLengthForChildren / effectiveChildrenCount;

        foreach (var child in Children)
        {
            var size = orientation == Orientation.Horizontal
                ? child.DesiredSize.Width
                : child.DesiredSize.Height;

            if (size > 0)
            {
                var childBounds = orientation == Orientation.Horizontal
                    ? new Rect(offset, 0, uniformLength, finalSize.Height)
                    : new Rect(0, offset, finalSize.Width, uniformLength);

                child.Arrange(childBounds);
                offset += uniformLength + spacing;
            }
        }

        return finalSize;
    }
}