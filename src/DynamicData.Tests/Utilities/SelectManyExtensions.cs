using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicData.Tests.Utilities
{
    /// <summary>
    /// see http://www.superstarcoders.com/blogs/posts/recursive-select-in-c-sharp-and-linq.aspx
    /// </summary>
    public static class SelectManyExtensions
    {
        public static IEnumerable<T> SelectManyRecursive<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> selector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (selector == null) throw new ArgumentNullException("selector");

            T[] selectManyRecursive = source as T[] ?? source.ToArray();
            return !selectManyRecursive.Any()
                ? selectManyRecursive
                : selectManyRecursive.Concat(
                    selectManyRecursive
                        .SelectMany(i => selector(i).EmptyIfNull())
                        .SelectManyRecursive(selector)
                    );
        }

        public static IEnumerable<TSource> RecursiveSelect<TSource>(this IEnumerable<TSource> source,
                                                                    Func<TSource, IEnumerable<TSource>> childSelector)
        {
            return RecursiveSelect(source, childSelector, element => element);
        }

        public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
                                                                             Func<TSource, IEnumerable<TSource>>
                                                                                 childSelector,
                                                                             Func<TSource, TResult> selector)
        {
            return RecursiveSelect(source, childSelector, (element, index, depth) => selector(element));
        }

        public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
                                                                             Func<TSource, IEnumerable<TSource>>
                                                                                 childSelector,
                                                                             Func<TSource, int, TResult> selector)
        {
            return RecursiveSelect(source, childSelector, (element, index, depth) => selector(element, index));
        }

        public static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
                                                                             Func<TSource, IEnumerable<TSource>>
                                                                                 childSelector,
                                                                             Func<TSource, int, int, TResult> selector)
        {
            return RecursiveSelect(source, childSelector, selector, 0);
        }

        private static IEnumerable<TResult> RecursiveSelect<TSource, TResult>(this IEnumerable<TSource> source,
                                                                              Func<TSource, IEnumerable<TSource>>
                                                                                  childSelector,
                                                                              Func<TSource, int, int, TResult> selector,
                                                                              int depth)
        {
            return source.SelectMany((element, index) => Enumerable.Repeat(selector(element, index, depth), 1)
                                                                   .Concat(
                                                                       RecursiveSelect(
                                                                           childSelector(element) ??
                                                                           Enumerable.Empty<TSource>(),
                                                                           childSelector, selector, depth + 1)));
        }

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> source)
        {
            return source ?? Enumerable.Empty<T>();
        }
    }
}
