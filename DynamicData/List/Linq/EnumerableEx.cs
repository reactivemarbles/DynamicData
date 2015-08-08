using System;
using System.Collections.Generic;
using DynamicData.Annotations;
using DynamicData.Internal;

namespace DynamicData.Linq
{
    internal static class EnumerableEx
    {
        /// <summary>
        /// Remove the index from the changes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IEnumerable<Change<T>> YieldWithoutIndex<T>(this IEnumerable<Change<T>>  source)
        {
            return new WithoutIndexEnumerator<T>(source);
        }

        /// <summary>
        /// Returns a flattend source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">source</exception>
        internal static IEnumerable<UnifiedChange<T>> Unified<T>([NotNull] this IChangeSet<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new UnifiedChangeEnumerator<T>(source);
        }


        /// <summary>
        /// Returns a flattend source with the index
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">source</exception>
        internal static IEnumerable<ItemChange<T>> Flatten<T>([NotNull] this IChangeSet<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new ItemChangeEnumerator<T>(source);
        }
    }
}