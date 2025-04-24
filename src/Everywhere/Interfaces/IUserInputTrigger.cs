using Avalonia.Input;

namespace Everywhere.Interfaces;

public interface IUserInputTrigger
{
    event Action ActionPanelRequested;
}