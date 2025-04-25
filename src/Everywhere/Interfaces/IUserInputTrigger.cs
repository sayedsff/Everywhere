namespace Everywhere.Interfaces;

public interface IUserInputTrigger
{
    event Action PointerActionTriggered;

    event Action KeyboardActionTriggered;
}