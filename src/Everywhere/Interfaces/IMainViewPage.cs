using Everywhere.Models;
using Lucide.Avalonia;

namespace Everywhere.Interfaces;

public interface IMainViewPage
{
    DynamicResourceKey Title { get; }

    LucideIconKind Icon { get; }
}