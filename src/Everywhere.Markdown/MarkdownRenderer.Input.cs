// using System.Collections;
// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Controls.Documents;
// using Avalonia.Input;
// using ZLinq;
//
// namespace Everywhere.Markdown;
//
// public partial class MarkdownRenderer
// {
//     private static SelectableTextBlock? GetSelectableTextBlock(PointerEventArgs e)
//     {
//         var element = e.Source as StyledElement;
//         while (element != null)
//         {
//             switch (element)
//             {
//                 case SelectableTextBlock stb:
//                     return stb;
//                 case MarkdownRenderer:
//                     return null;
//                 default:
//                     element = element.Parent;
//                     break;
//             }
//         }
//         return null;
//     }
//
//     private static IEnumerable<SelectableTextBlock> DfsWhile(SelectableTextBlock current, Predicate<SelectableTextBlock> condition, bool reversed)
//     {
//         if (reversed)
//         {
//             var node = GetNextElement(current);
//             while (node != null)
//             {
//                 if (node is SelectableTextBlock stb)
//                 {
//                     if (!condition(stb)) yield break;
//                     yield return stb;
//                 }
//
//                 node = GetNextElement(node);
//             }
//         }
//         else
//         {
//             var node = GetPreviousElement(current);
//             while (node != null)
//             {
//                 if (node is SelectableTextBlock stb)
//                 {
//                     if (!condition(stb)) yield break;
//                     yield return stb;
//                 }
//
//                 node = GetPreviousElement(node);
//             }
//         }
//
//         static IList GetChildren(StyledElement element)
//         {
//             return element switch
//             {
//                 Panel panel => panel.Children,
//                 Span span => span.Inlines,
//                 _ => Array.Empty<StyledElement>()
//             };
//         }
//
//         StyledElement? GetNextElement(StyledElement element)
//         {
//             var children = GetChildren(element);
//             if (children.Count > 0)
//                 return Cast(children[0]);
//             while (element.Parent != null)
//             {
//                 var siblings = GetChildren(element.Parent);
//                 var idx = siblings.IndexOf(element);
//                 if (idx < siblings.Count - 1)
//                     return Cast(siblings[idx + 1]);
//                 element = element.Parent;
//             }
//             return null;
//         }
//
//         StyledElement? GetPreviousElement(StyledElement element)
//         {
//             if (element.Parent == null) return null;
//             var siblings = GetChildren(element.Parent);
//             var idx = siblings.IndexOf(element);
//             if (idx <= 0) return element.Parent;
//
//             var node = Cast(siblings[idx - 1]);
//             var children = GetChildren(node);
//             while (children.Count > 0)
//                 node = Cast(children[^1]);
//             return node;
//         }
//
//         static StyledElement Cast(object? obj) =>
//             obj as StyledElement ?? throw new InvalidCastException($"Expected StyledElement, got {obj?.GetType().Name ?? "null"}");
//     }
//
//     private SelectableTextBlock? startSelectingTextBlock;
//     private Rect pointerDownSelectableTextBlockBounds;
//     private int globalSelectionStart;
//     private readonly List<SelectableTextBlock> selectionTextBlocks = [];
//
//     private SelectableTextBlock? GetSelectableTextBlock(PointerEventArgs e, Point pointerPosition)
//     {
//         var result = GetSelectableTextBlock(e);
//         if (result is not null) return result;
//
//         if (documentNode.SelectableTextBlockBounds.Count == 0) return null;
//
//         // pointer is inside a SelectableTextBlock?
//         result = documentNode.SelectableTextBlockBounds
//             .AsValueEnumerable()
//             .FirstOrDefault(kv => kv.Value.Contains(pointerPosition))
//             .Key;
//         // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
//         if (result is not null) return result;
//
//         // same line?
//         result = documentNode.SelectableTextBlockBounds
//             .AsValueEnumerable()
//             .FirstOrDefault(kv => kv.Value.Y <= pointerPosition.Y && kv.Value.Bottom >= pointerPosition.Y)
//             .Key;
//         // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
//         if (result is not null) return result;
//
//         // no SelectableTextBlock found, return the last one in the document
//         return documentNode.SelectableTextBlockBounds
//             .AsValueEnumerable()
//             .Select(kv => kv.Key)
//             .OrderByDescending(stb => TranslateBoundsToGlobal(stb).Bottom)
//             .First();
//     }
//
//     private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
//     {
//         var clickInfo = e.GetCurrentPoint(this);
//         startSelectingTextBlock = GetSelectableTextBlock(e, clickInfo.Position);
//         if (startSelectingTextBlock is null)
//         {
//             // if no SelectableTextBlock was found, we do not handle the event
//             return;
//         }
//         pointerDownSelectableTextBlockBounds = TranslateBoundsToGlobal(startSelectingTextBlock);
//
//         var text = startSelectingTextBlock.Inlines is { Count: > 0 } inline ? inline.Text : startSelectingTextBlock.Text;
//         if (text != null && clickInfo.Properties.IsLeftButtonPressed)
//         {
//             var padding = startSelectingTextBlock.Padding;
//             var point = e.GetPosition(startSelectingTextBlock) - new Point(padding.Left, padding.Top);
//             var clickToSelect = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
//
//             var wordSelectionStart = documentNode.GlobalTextPositionToLocal(startSelectingTextBlock, globalSelectionStart);
//             wordSelectionStart = Math.Clamp(wordSelectionStart, 0, text.Length);
//
//             var hit = startSelectingTextBlock.TextLayout.HitTestPoint(point);
//             var index = hit.TextPosition;
//
//             switch (e.ClickCount)
//             {
//                 case 1:
//                 {
//                     if (clickToSelect)
//                     {
//                         var previousWord = StringUtils.PreviousWord(text, index);
//
//                         if (index > wordSelectionStart)
//                         {
//                             SetCurrentValue(SelectableTextBlock.SelectionEndProperty, StringUtils.NextWord(text, index));
//                         }
//
//                         if (index < wordSelectionStart || previousWord == wordSelectionStart)
//                         {
//                             SetCurrentValue(SelectableTextBlock.SelectionStartProperty, previousWord);
//                         }
//                     }
//                     else
//                     {
//                         startSelectingTextBlock.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, index);
//                         startSelectingTextBlock.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, index);
//                         globalSelectionStart = documentNode.LocalTextPositionToGlobal(startSelectingTextBlock, index);
//                     }
//
//                     break;
//                 }
//                 case 2:
//                 {
//                     if (!StringUtils.IsStartOfWord(text, index))
//                     {
//                         startSelectingTextBlock.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, StringUtils.PreviousWord(text, index));
//                     }
//
//                     globalSelectionStart = documentNode.LocalTextPositionToGlobal(startSelectingTextBlock, startSelectingTextBlock.SelectionStart);
//
//                     if (!StringUtils.IsEndOfWord(text, index))
//                     {
//                         startSelectingTextBlock.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, StringUtils.NextWord(text, index));
//                     }
//
//                     break;
//                 }
//                 case 3:
//                 {
//                     startSelectingTextBlock.SelectAll();
//                     globalSelectionStart = documentNode.LocalTextPositionToGlobal(startSelectingTextBlock, startSelectingTextBlock.SelectionStart);
//                     break;
//                 }
//             }
//         }
//
//         e.Pointer.Capture(this);
//         e.Handled = true;
//     }
//
//     private void HandlePointerMoved(object? sender, PointerEventArgs e)
//     {
//         // selection should not change during pointer move if the user right clicks
//         var clickInfo = e.GetCurrentPoint(this);
//         if (startSelectingTextBlock is not null && Equals(e.Pointer.Captured, this) && clickInfo.Properties.IsLeftButtonPressed)
//         {
//             var endSelectingTextBlock = GetSelectableTextBlock(e, clickInfo.Position);
//             if (endSelectingTextBlock is null) return;
//
//             var text = endSelectingTextBlock.Inlines is { Count: > 0 } inline ? inline.Text : endSelectingTextBlock.Text;
//             var padding = endSelectingTextBlock.Padding;
//
//             var point = e.GetPosition(endSelectingTextBlock) - new Point(padding.Left, padding.Top);
//
//             point = new Point(
//                 Math.Clamp(point.X, 0, Math.Max(endSelectingTextBlock.TextLayout.WidthIncludingTrailingWhitespace, 0)),
//                 Math.Clamp(point.Y, 0, Math.Max(endSelectingTextBlock.TextLayout.Height, 0)));
//
//             var hit = endSelectingTextBlock.TextLayout.HitTestPoint(point);
//             var textPosition = hit.TextPosition;
//
//             if (Equals(endSelectingTextBlock, startSelectingTextBlock))
//             {
//                 // simple
//             }
//             else
//             {
//                 //      reverse      |      reverse      |      reverse
//                 // ------------------+-------------------+-------------------
//                 //      reverse      |   pointerDownSTB  |
//                 // ------------------+-------------------+-------------------
//                 //                   |                   |
//                 var reverse =
//                     clickInfo.Position.Y < pointerDownSelectableTextBlockBounds.Y ||
//                     clickInfo.Position.X < pointerDownSelectableTextBlockBounds.X &&
//                     clickInfo.Position.Y <= pointerDownSelectableTextBlockBounds.Bottom;
//             }
//         }
//     }
//
//     private void HandlePointerReleased(object? sender, PointerReleasedEventArgs e)
//     {
//         if (!Equals(e.Pointer.Captured, this)) return;
//         e.Pointer.Capture(null);
//
//         // if (startSelectingTextBlock is null) return;
//         //
//         // if (e.InitialPressMouseButton == MouseButton.Right)
//         // {
//         //     var padding = Padding;
//         //
//         //     var point = e.GetPosition(this) - new Point(padding.Left, padding.Top);
//         //
//         //     var hit = TextLayout.HitTestPoint(point);
//         //
//         //     var caretIndex = hit.TextPosition;
//         //
//         //     // see if mouse clicked inside current selection
//         //     // if it did not, we change the selection to where the user clicked
//         //     var firstSelection = Math.Min(SelectionStart, SelectionEnd);
//         //     var lastSelection = Math.Max(SelectionStart, SelectionEnd);
//         //     var didClickInSelection = SelectionStart != SelectionEnd &&
//         //         caretIndex >= firstSelection && caretIndex <= lastSelection;
//         //     if (!didClickInSelection)
//         //     {
//         //         SetCurrentValue(SelectionStartProperty, caretIndex);
//         //         SetCurrentValue(SelectionEndProperty, caretIndex);
//         //     }
//         // }
//     }
// }