using System.Diagnostics.CodeAnalysis;

namespace Everywhere.Utilities;

/// <summary>
/// Represents a reference to an object that can be switched between a strong and a weak reference.
/// This allows the object to be garbage collected when the reference is inactive (weak) and no other strong references exist,
/// but prevents garbage collection when the reference is active (strong).
/// This implementation is optimized for concurrent reads using ReaderWriterLockSlim.
/// </summary>
/// <typeparam name="T">The type of the object being referenced. Must be a reference type.</typeparam>
public class ResilientReference<T> where T : class
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private T? _strongReference;
    private WeakReference<T>? _weakReference;
    private bool _isActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientReference{T}"/> class.
    /// </summary>
    /// <param name="target">The initial object to reference.</param>
    /// <param name="isActive">A value indicating whether the reference should initially be active (strong). Default is true.</param>
    public ResilientReference(T? target = null, bool isActive = true)
    {
        _isActive = isActive;
        if (target == null) return;

        if (_isActive)
        {
            _strongReference = target;
        }
        else
        {
            _weakReference = new WeakReference<T>(target);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the reference is active (strong).
    /// When set to <c>true</c>, the reference becomes strong.
    /// When set to <c>false</c>, the reference becomes weak.
    /// </summary>
    public bool IsActive
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _isActive;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        set
        {
            _lock.EnterWriteLock();
            try
            {
                if (_isActive == value) return;

                if (value) // Switching to Active (Strong)
                {
                    if (_weakReference != null && _weakReference.TryGetTarget(out var target))
                    {
                        _strongReference = target;
                    }
                    _weakReference = null;
                }
                else // Switching to Inactive (Weak)
                {
                    if (_strongReference != null)
                    {
                        _weakReference = new WeakReference<T>(_strongReference);
                    }
                    _strongReference = null;
                }

                _isActive = value;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Gets or sets the target object.
    /// When getting, if the reference is weak and the object has been collected, returns <c>null</c>.
    /// </summary>
    public T? Target
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                if (_isActive)
                {
                    return _strongReference;
                }

                if (_weakReference != null && _weakReference.TryGetTarget(out var target))
                {
                    return target;
                }

                return null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        set
        {
            _lock.EnterWriteLock();
            try
            {
                if (value == null)
                {
                    _strongReference = null;
                    _weakReference = null;
                    return;
                }

                if (_isActive)
                {
                    _strongReference = value;
                    _weakReference = null;
                }
                else
                {
                    _strongReference = null;
                    _weakReference = new WeakReference<T>(value);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Tries to retrieve the target object.
    /// </summary>
    /// <param name="target">When this method returns, contains the target object if it is available.</param>
    /// <returns><c>true</c> if the target object was retrieved; otherwise, <c>false</c>.</returns>
    public bool TryGetTarget([MaybeNullWhen(false)] out T target)
    {
        target = Target;
        return target != null;
    }
}
