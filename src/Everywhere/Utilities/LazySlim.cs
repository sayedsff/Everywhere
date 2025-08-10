using System.Diagnostics.CodeAnalysis;

namespace Everywhere.Utilities;

/// <summary>
/// A lightweight alternative to <see cref="Lazy{T}"/> that is not thread-safe.
/// Warn that this is a struct, so it should be used with care to avoid unnecessary copies.
/// </summary>
/// <param name="factory"></param>
/// <typeparam name="T"></typeparam>
public struct LazySlim<T>(Func<T> factory)
{
    [field: AllowNull, MaybeNull]
    public T Value
    {
        get
        {
            if (_isValueCreated) return field!;
            field = factory.Invoke();
            _isValueCreated = true;
            return field!;
        }
    }

    private bool _isValueCreated;
}

/// <summary>
/// A lightweight alternative to <see cref="Lazy{T}"/> that is not thread-safe and supports expiration.
/// Warn that this is a struct, so it should be used with care to avoid unnecessary copies
/// and that the value may be recreated after expiration.
/// </summary>
/// <param name="factory"></param>
/// <param name="expirationTime"></param>
/// <typeparam name="T"></typeparam>
public struct ExpirationLazySlim<T>(Func<T> factory, TimeSpan expirationTime)
{
    [field: AllowNull, MaybeNull]
    public T Value
    {
        get
        {
            if (DateTime.UtcNow - _creationTime < expirationTime)
            {
                Console.WriteLine("Using cached value.");
                return field!;
            }

            if (_creationTime != DateTime.MinValue)
            {
                Console.WriteLine("Cached value expired, creating a new one.");
            }

            field = factory.Invoke();
            _creationTime = DateTime.UtcNow;
            return field!;
        }
    }

    private DateTime _creationTime = DateTime.MinValue;
}