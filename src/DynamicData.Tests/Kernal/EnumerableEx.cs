using System;
using System.Collections.Generic;

namespace DynamicData.Tests.Cache;

public static class EnumerableEx
{
    public static IEnumerable<TResult> CurrentNextZip<T, TResult>(this IEnumerable<T> source, Func<T, T?, TResult> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var enumerator = source.GetEnumerator();
        if (enumerator.MoveNext())
        {
            var curr = enumerator.Current;

            while (enumerator.MoveNext())
            {
                var next = enumerator.Current;
                yield return selector(curr, next);
                curr = next;
            }

            yield return selector(curr, default);
        }
    }

    public static IEnumerable<TResult> PrevCurrentNextZip<T, TResult>(this IEnumerable<T> source, Func<T?, T, T?, TResult> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var enumerator = source.GetEnumerator();
        if (enumerator.MoveNext())
        {
            var prev = default(T);
            var curr = enumerator.Current;

            while (enumerator.MoveNext())
            {
                var next = enumerator.Current;
                yield return selector(prev, curr, next);
                prev = curr;
                curr = next;
            }

            yield return selector(prev, curr, default);
        }
    }

    public static IEnumerable<TResult> PrevCurrentZip<T, TResult>(this IEnumerable<T> source, Func<T?, T, TResult> selector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var enumerator = source.GetEnumerator();
        if (enumerator.MoveNext())
        {
            var prev = default(T);
            var curr = enumerator.Current;

            while (enumerator.MoveNext())
            {
                yield return selector(prev, curr);
                prev = curr;
            }

            yield return selector(prev, curr);
        }
    }
}
