// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Kernel;

/// <summary>
/// Enumerable extensions.
/// </summary>
public static class EnumerableEx
{
    /// <summary>
    /// Casts the enumerable to an array if it is already an array.  Otherwise call ToArray.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The array of items.</returns>
    public static T[] AsArray<T>(this IEnumerable<T> source)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source as T[] ?? source.ToArray();
    }

    /// <summary>
    /// Casts the enumerable to a List if it is already a List.  Otherwise call ToList.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The list.</returns>
    public static List<T> AsList<T>(this IEnumerable<T> source)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source as List<T> ?? source.ToList();
    }

    /// <summary>
    /// Returns any duplicated values from the source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="valueSelector">The value selector.</param>
    /// <returns>The enumerable of items.</returns>
    public static IEnumerable<T> Duplicates<T, TValue>(this IEnumerable<T> source, Func<T, TValue> valueSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        valueSelector.ThrowArgumentNullExceptionIfNull(nameof(valueSelector));

        return source.GroupBy(valueSelector).Where(group => group.Count() > 1).SelectMany(t => t);
    }

    /// <summary>
    /// Finds the index of many items as specified in the secondary enumerable.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="itemsToFind">The items to find in the source enumerable.</param>
    /// <returns>
    /// A result as specified by the result selector.
    /// </returns>
    public static IEnumerable<ItemWithIndex<T>> IndexOfMany<T>(this IEnumerable<T> source, IEnumerable<T> itemsToFind) => source.IndexOfMany(itemsToFind, (t, idx) => new ItemWithIndex<T>(t, idx));

    /// <summary>
    /// Finds the index of many items as specified in the secondary enumerable.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="itemsToFind">The items to find.</param>
    /// <param name="resultSelector">The result selector.</param>
    /// <returns>A result as specified by the result selector.</returns>
    public static IEnumerable<TResult> IndexOfMany<TObject, TResult>(this IEnumerable<TObject> source, IEnumerable<TObject> itemsToFind, Func<TObject, int, TResult> resultSelector)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        itemsToFind.ThrowArgumentNullExceptionIfNull(nameof(itemsToFind));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        var indexed = source.Select((element, index) => new { Element = element, Index = index });
        return itemsToFind.Join(indexed, left => left, right => right.Element, (_, right) => right).Select(x => resultSelector(x.Element, x.Index));
    }

    internal static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source) => source ?? Enumerable.Empty<T>();

    internal static IEnumerable<T> EnumerateOne<T>(this T source)
    {
        yield return source;
    }

    internal static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
        {
            action(item);
        }
    }

    internal static void ForEach<TObject>(this IEnumerable<TObject> source, Action<TObject, int> action)
    {
        var i = 0;
        foreach (var item in source)
        {
            action(item, i);
            i++;
        }
    }

#if !WINDOWS_UWP
    internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new(source);

#endif

    /// <summary>
    /// Returns an object with it's current index.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>The enumerable of items with their indexes.</returns>
    internal static IEnumerable<ItemWithIndex<T>> WithIndex<T>(this IEnumerable<T> source) => source.Select((item, index) => new ItemWithIndex<T>(item, index));
}
