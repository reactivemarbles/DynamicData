using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Tests.Utilities;

/// <summary>
/// see http://www.superstarcoders.com/blogs/posts/recursive-select-in-c-sharp-and-linq.aspx
/// </summary>
public static class SelectManyExtensions
{
    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> source) => source ?? Enumerable.Empty<T>();

    public static IEnumerable<TSource> RecursiveSelect<TSource>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TSource>> childSelector) => RecursiveSelect(source, childSelector, element => element);

    public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TSource>> childSelector, Func<TSource, TResult> selector) => RecursiveSelect(source, childSelector, (element, index, depth) => selector(element));

    public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TSource>> childSelector, Func<TSource, int, TResult> selector) => RecursiveSelect(source, childSelector, (element, index, depth) => selector(element, index));

    public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TSource>> childSelector, Func<TSource, int, int, TResult> selector) => RecursiveSelect(source, childSelector, selector, 0);

    public static IEnumerable<T> SelectManyRecursive<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var selectManyRecursive = source as T[] ?? source.ToArray();
        return !selectManyRecursive.Any() ? selectManyRecursive : selectManyRecursive.Concat(selectManyRecursive.SelectMany(i => selector(i).EmptyIfNull()).SelectManyRecursive(selector));
    }

    private static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TSource>> childSelector, Func<TSource, int, int, TResult> selector, int depth) => source.SelectMany((element, index) => Enumerable.Repeat(selector(element, index, depth), 1).Concat(RecursiveSelect(childSelector(element) ?? Enumerable.Empty<TSource>(), childSelector, selector, depth + 1)));
}
