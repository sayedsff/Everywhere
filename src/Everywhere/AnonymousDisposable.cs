namespace Everywhere;

public class AnonymousDisposable(Action disposeAction) : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        disposeAction();
    }
}