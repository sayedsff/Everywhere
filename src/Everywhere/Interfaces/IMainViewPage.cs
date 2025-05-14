using Lucide.Avalonia;

namespace Everywhere.Interfaces;

public interface IMainViewPage
{
    string Title { get; }

    LucideIconKind Icon { get; }
}