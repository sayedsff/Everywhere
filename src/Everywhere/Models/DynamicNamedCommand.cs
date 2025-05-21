using System.Windows.Input;
using Lucide.Avalonia;

namespace Everywhere.Models;

public record DynamicNamedCommand(
    LucideIconKind Icon,
    DynamicResourceKey HeaderKey,
    DynamicResourceKey? DescriptionKey = null,
    ICommand? Command = null,
    object? CommandParameter = null);