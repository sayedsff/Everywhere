using ShadUI;

namespace Everywhere.Views;

public interface IReactiveHost
{
    DialogHost DialogHost { get; }

    ToastHost ToastHost { get; }
}