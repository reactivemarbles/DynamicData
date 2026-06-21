// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the TransformOnObservable class.
/// </summary>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="transform">The transform value.</param>
/// <param name="transformOnRefresh">The transformOnRefresh value.</param>
internal sealed class TransformOnObservable<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform, bool transformOnRefresh = false)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TKey>> Run() =>
        Observable.Create<IChangeSet<TDestination, TKey>>(observer => new Subscription(source, transform, observer, transformOnRefresh));
    // Maintains state for a single subscription

/// <summary>
/// Provides members for the Subscription class.
/// </summary>
private sealed class Subscription : CacheParentSubscription<TSource, TKey, TDestination, IChangeSet<TDestination, TKey>>
    {
        /// <summary>
        /// The _cache field.
        /// </summary>
        private readonly ChangeAwareCache<TDestination, TKey> _cache = new();

        /// <summary>
        /// The _transform field.
        /// </summary>
        private readonly Func<TSource, TKey, IObservable<TDestination>> _transform;

        /// <summary>
        /// The _transformOnRefresh field.
        /// </summary>
        private readonly bool _transformOnRefresh;

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="transform">The transform value.</param>
        /// <param name="observer">The observer value.</param>
        /// <param name="transformOnRefresh">The transformOnRefresh value.</param>
        public Subscription(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform, IObserver<IChangeSet<TDestination, TKey>> observer, bool transformOnRefresh)
            : base(observer)
        {
            _transform = transform;
            _transformOnRefresh = transformOnRefresh;
            CreateParentSubscription(source);
        }

        /// <summary>
        /// Executes the ParentOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        protected override void ParentOnNext(IChangeSet<TSource, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one that
                    // Will update the cache and emit the changes
                    case ChangeReason.Add or ChangeReason.Update:
                        AddTransformSubscription(change.Current, change.Key);
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        _cache.Remove(change.Key);
                        RemoveChildSubscription(change.Key);
                        break;

                    case ChangeReason.Refresh:
                        if (_transformOnRefresh)
                        {
                            AddTransformSubscription(change.Current, change.Key);
                        }
                        else
                        {
                            // Let the downstream decide what this means
                            _cache.Refresh(change.Key);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Executes the ChildOnNext operation.
        /// </summary>
        /// <param name="child">The child value.</param>
        /// <param name="parentKey">The parentKey value.</param>
        protected override void ChildOnNext(TDestination child, TKey parentKey) =>
            _cache.AddOrUpdate(child, parentKey);

        /// <summary>
        /// Executes the EmitChanges operation.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        protected override void EmitChanges(IObserver<IChangeSet<TDestination, TKey>> observer)
        {
            var changes = _cache.CaptureChanges();
            if (changes.Count > 0)
            {
                observer.OnNext(changes);
            }
        }

        /// <summary>
        /// Executes the AddTransformSubscription operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <param name="key">The key value.</param>
        private void AddTransformSubscription(TSource obj, TKey key) =>
            AddChildSubscription(MakeChildObservable(_transform(obj, key).DistinctUntilChanged()), key);
    }
}
