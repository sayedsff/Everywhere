using IconPacks.Avalonia.Material;

namespace Everywhere.Interfaces;

public interface IMainViewPage
{
    string Title { get; }

    PackIconMaterialKind Icon { get; }
}