using System.Windows.Input;
using Lucide.Avalonia;

namespace Everywhere.Common;

public record DynamicNamedCommand(
    LucideIconKind Icon,
    DynamicResourceKey HeaderKey,
    DynamicResourceKey? DescriptionKey = null,
    ICommand? Command = null,
    object? CommandParameter = null);