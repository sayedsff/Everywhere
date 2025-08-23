using Lucide.Avalonia;

namespace Everywhere.Views;

public interface IMainViewPage
{
    int Index { get; }

    DynamicResourceKey Title { get; }

    LucideIconKind Icon { get; }
}

public interface IMainViewPageFactory
{
    IEnumerable<IMainViewPage> CreatePages();
}