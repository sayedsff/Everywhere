using System.Diagnostics.CodeAnalysis;

namespace Everywhere.Extensions;

public static class CastExtension
{
    /// <summary>
    /// 等价于(T?)obj，谁不喜欢Fluent API呢，还不用加括号
    /// </summary>
    /// <remarks>
    /// 之所以用To而不用Cast，是因为Cast与Linq的Cast有重名
    /// </remarks>
    /// <param name="obj"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(obj))] 
    public static T? To<T>(this object? obj)
    {
        return (T?)obj;
    }

    /// <summary>
    /// 等价于obj as T，谁不喜欢Fluent API呢，还不用加括号
    /// </summary>
    /// <param name="obj"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T? As<T>(this object? obj) where T : class
    {
        return obj as T;
    }
}