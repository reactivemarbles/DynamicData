// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the TransformManyAsync class.
/// </summary>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="transformer">The transformer value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
/// <param name="comparer">The comparer value.</param>
/// <param name="errorHandler">The errorHandler value.</param>
internal sealed class TransformManyAsync<TSource, TKey, TDestination, TDestinationKey>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> transformer, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer, Action<Error<TSource, TKey>>? errorHandler = null)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer => new Subscription(source, transformer, observer, equalityComparer, comparer, errorHandler));
    // Maintains state for a single subscription

/// <summary>
/// Provides members for the Subscription class.
/// </summary>
private sealed class Subscription : CacheParentSubscription<ChangeSetCache<TDestination, TDestinationKey>, TKey, IChangeSet<TDestination, TDestinationKey>, IChangeSet<TDestination, TDestinationKey>>
    {
        /// <summary>
        /// The _cache field.
        /// </summary>
        private readonly Cache<ChangeSetCache<TDestination, TDestinationKey>, TKey> _cache = new();

        /// <summary>
        /// The _changeSetMergeTracker field.
        /// </summary>
        private readonly ChangeSetMergeTracker<TDestination, TDestinationKey> _changeSetMergeTracker;

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="transform">The transform value.</param>
        /// <param name="observer">The observer value.</param>
        /// <param name="equalityComparer">The equalityComparer value.</param>
        /// <param name="comparer">The comparer value.</param>
        /// <param name="errorHandler">The errorHandler value.</param>
        public Subscription(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> transform, IObserver<IChangeSet<TDestination, TDestinationKey>> observer, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer, Action<Error<TSource, TKey>>? errorHandler = null)
            : base(observer)
        {
            // Transform Helper
            async Task<IObservable<IChangeSet<TDestination, TDestinationKey>>> ErrorHandlingTransform(TSource obj, TKey key)
            {
                try
                {
                    return await transform(obj, key).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    errorHandler.Invoke(new Error<TSource, TKey>(e, obj, key));
                    return Observable.Empty<IChangeSet<TDestination, TDestinationKey>>();
                }
            }

            ChangeSetCache<TDestination, TDestinationKey> Transformer(TSource obj, TKey key) =>
                new(MakeChildObservable(Observable.Defer(() => transform(obj, key))));

            ChangeSetCache<TDestination, TDestinationKey> SafeTransformer(TSource obj, TKey key) =>
                new(MakeChildObservable(Observable.Defer(() => ErrorHandlingTransform(obj, key))));

            _changeSetMergeTracker = new(() => _cache.Items, comparer, equalityComparer);

            if (errorHandler is null)
            {
                CreateParentSubscription(source.Transform(Transformer));
            }
            else
            {
                CreateParentSubscription(source.Transform(SafeTransformer));
            }
        }

        /// <summary>
        /// Executes the ParentOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        protected override void ParentOnNext(IChangeSet<ChangeSetCache<TDestination, TDestinationKey>, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one that
                    // Will update the cache and emit the changes
                    case ChangeReason.Add or ChangeReason.Update:
                        _cache.AddOrUpdate(change.Current, change.Key);
                        AddChildSubscription(change.Current.Source, change.Key);
                        if (change.Previous.HasValue)
                        {
                            _changeSetMergeTracker.RemoveItems(change.Previous.Value.Cache.KeyValues);
                        }
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        _cache.Remove(change.Key);
                        RemoveChildSubscription(change.Key);
                        _changeSetMergeTracker.RemoveItems(change.Current.Cache.KeyValues);
                        break;
                }
            }
        }

        /// <summary>
        /// Executes the ChildOnNext operation.
        /// </summary>
        /// <param name="child">The child value.</param>
        /// <param name="parentKey">The parentKey value.</param>
        protected override void ChildOnNext(IChangeSet<TDestination, TDestinationKey> child, TKey parentKey) =>
            _changeSetMergeTracker.ProcessChangeSet(child);

        /// <summary>
        /// Executes the EmitChanges operation.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        protected override void EmitChanges(IObserver<IChangeSet<TDestination, TDestinationKey>> observer) =>
            _changeSetMergeTracker.EmitChanges(observer);
    }
}
