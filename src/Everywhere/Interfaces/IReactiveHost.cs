using ShadUI;

namespace Everywhere.Interfaces;

public interface IReactiveHost
{
    DialogHost DialogHost { get; }

    ToastHost ToastHost { get; }
}