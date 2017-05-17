using System;
using DynamicData.Annotations;

namespace DynamicData
{
    internal static class Constants
    {
        public const string EvaluateIsDead = "Use Refresh: Same thing but better semantics";
    }

    /// <summary>
    /// Obsolete methods: Kept in system to prevent breaking changes for now
    /// </summary>
    public static class ObsoleteEx
    {
        /// <summary>
        /// Action to apply a batch update to a cache. Multiple update methods can be invoked within a single batch operation.
        /// These operations are invoked within the cache's lock and is therefore thread safe.
        /// The result of the action will produce a single changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="updateAction">The update action.</param>
        /// <param name="errorHandler">Optionally pass in an error handler</param>
        [Obsolete("Prefer Edit() as it provides a consistent semantics with ISourceList<T>")]
        public static void BatchUpdate<TObject, TKey>([NotNull] this ISourceCache<TObject, TKey> source,
                                                      [NotNull] Action<ISourceUpdater<TObject, TKey>> updateAction,
                                                      Action<Exception> errorHandler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            source.Edit(updateAction, errorHandler);
        }

        /// <summary>
        /// Action to apply a batch update to a cache. Multiple update methods can be invoked within a single batch operation.
        /// These operations are invoked within the cache's lock and is therefore thread safe.
        /// The result of the action will produce a single changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="updateAction">The update action.</param>
        /// <param name="errorHandler">Optionally pass in an error handler</param>
        [Obsolete("Prefer Edit() as it provides a consistent semantics with ISourceList<T>")]
        public static void BatchUpdate<TObject, TKey>([NotNull] this IIntermediateCache<TObject, TKey> source,
                                                      [NotNull] Action<ICacheUpdater<TObject, TKey>> updateAction,
                                                      Action<Exception> errorHandler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            source.Edit(updateAction, errorHandler);
        }
    }
}
