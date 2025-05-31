using System.Diagnostics.CodeAnalysis;

namespace Everywhere.Utils;

public struct LazySlim<T>(Func<T> factory)
{
    [field: AllowNull, MaybeNull]
    public T Value
    {
        get
        {
            if (isValueCreated) return field!;
            field = factory.Invoke();
            isValueCreated = true;
            return field!;
        }
    }

    private bool isValueCreated;
}

public struct ExpirationLazySlim<T>(Func<T> factory, TimeSpan expirationTime)
{
    [field: AllowNull, MaybeNull]
    public T Value
    {
        get
        {
            if (DateTime.UtcNow - creationTime < expirationTime)
            {
                Console.WriteLine("Using cached value.");
                return field!;
            }

            if (creationTime != DateTime.MinValue)
            {
                Console.WriteLine("Cached value expired, creating a new one.");
            }

            field = factory.Invoke();
            creationTime = DateTime.UtcNow;
            return field!;
        }
    }

    private DateTime creationTime = DateTime.MinValue;
}