// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the TransformMany class.
/// </summary>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
/// <typeparam name="TSource">The type of the TSource value.</typeparam>
/// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="manySelector">The manySelector value.</param>
/// <param name="keySelector">The keySelector value.</param>
/// <param name="childChanges">The childChanges value.</param>
internal sealed class TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector, Func<TSource, IObservable<IChangeSet<TDestination, TDestinationKey>>>? childChanges = null)
    where TDestination : notnull
    where TDestinationKey : notnull
    where TSource : notnull
    where TSourceKey : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    public TransformMany(IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        : this(
            source,
            manySelector,
            keySelector,
            t => Observable.Defer(
                () =>
                {
                    var subsequentChanges = manySelector(t).ToObservableChangeSet(keySelector);

                    if (manySelector(t).Count > 0)
                    {
                        return subsequentChanges;
                    }

                    return Observable.Return(ChangeSet<TDestination, TDestinationKey>.Empty).Concat(subsequentChanges);
                }))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    public TransformMany(IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        : this(
            source,
            manySelector,
            keySelector,
            t => Observable.Defer(
                () =>
                {
                    var subsequentChanges = manySelector(t).ToObservableChangeSet(keySelector);

                    if (manySelector(t).Count > 0)
                    {
                        return subsequentChanges;
                    }

                    return Observable.Return(ChangeSet<TDestination, TDestinationKey>.Empty).Concat(subsequentChanges);
                }))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    public TransformMany(IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IObservableCache<TDestination, TDestinationKey>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        : this(
            source,
            x => manySelector(x).Items,
            keySelector,
            t => Observable.Defer(
                () =>
                {
                    var subsequentChanges = Observable.Create<IChangeSet<TDestination, TDestinationKey>>(o => manySelector(t).Connect().Subscribe(o));

                    if (manySelector(t).Count > 0)
                    {
                        return subsequentChanges;
                    }

                    return Observable.Return(ChangeSet<TDestination, TDestinationKey>.Empty).Concat(subsequentChanges);
                }))
    {
    }

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => childChanges is null ? Create() : CreateWithChangeSet();

    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TDestination, TDestinationKey>> Create() => source.Transform(
            (t, _) =>
            {
                var destination = manySelector(t).Select(m => new DestinationContainer(m, keySelector(m))).ToArray();
                return new ManyContainer(() => destination);
            },
            true).Select(changes => new ChangeSet<TDestination, TDestinationKey>(new DestinationEnumerator(changes)));

    /// <summary>
    /// Executes the CreateWithChangeSet operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TDestination, TDestinationKey>> CreateWithChangeSet()
    {
        if (childChanges is null)
        {
            throw new InvalidOperationException("The childChanges is null and should not be.");
        }

        return Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
            observer =>
            {
                var result = new ChangeAwareCache<TDestination, TDestinationKey>();

                var transformed = source.Transform(
                    (t, _) =>
                    {
                        // Only skip initial for first time Adds where there is initial data records
                        var locker = InternalEx.NewLock();
                        var changes = childChanges(t).Synchronize(locker).Skip(1);
                        return new ManyContainer(
                            () =>
                            {
                                var collection = manySelector(t);
                                lock (locker)
                                {
                                    return collection.Select(m => new DestinationContainer(m, keySelector(m))).ToArray();
                                }
                            },
                            changes);
                    }).Publish();

                var queue = new SharedDeliveryQueue();
                var initial = transformed.SynchronizeSafe(queue).Select(changes => new ChangeSet<TDestination, TDestinationKey>(new DestinationEnumerator(changes)));

                var subsequent = transformed.MergeMany(x => x.Changes).SynchronizeSafe(queue);

                var allChanges = initial.Merge(subsequent).Select(
                    changes =>
                    {
                        result.Clone(changes);
                        return result.CaptureChanges();
                    });

                return new CompositeDisposable(allChanges.SubscribeSafe(observer), transformed.Connect(), queue);
            });
    }

/// <summary>
/// Provides members for the DestinationContainer class.
/// </summary>
/// <param name="item">The item value.</param>
/// <param name="key">The key value.</param>
private sealed class DestinationContainer(TDestination item, TDestinationKey key)
    {
        /// <summary>
        /// Gets the KeyComparer value.
        /// </summary>
        public static IEqualityComparer<DestinationContainer> KeyComparer { get; } = new KeyEqualityComparer();

        /// <summary>
        /// Gets the Item value.
        /// </summary>
        public TDestination Item { get; } = item;

        /// <summary>
        /// Gets the Key value.
        /// </summary>
        public TDestinationKey Key { get; } = key;

/// <summary>
/// Provides members for the KeyEqualityComparer class.
/// </summary>
private sealed class KeyEqualityComparer : IEqualityComparer<DestinationContainer>
        {
            /// <summary>
            /// Executes the Equals operation.
            /// </summary>
            /// <param name="x">The x value.</param>
            /// <param name="y">The y value.</param>
            /// <returns>The result of the operation.</returns>
            public bool Equals(DestinationContainer? x, DestinationContainer? y)
            {
                if (x is null && y is null)
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return EqualityComparer<TDestinationKey?>.Default.Equals(x.Key, y.Key);
            }

            /// <summary>
            /// Executes the GetHashCode operation.
            /// </summary>
            /// <param name="obj">The obj value.</param>
            /// <returns>The result of the operation.</returns>
            public int GetHashCode(DestinationContainer obj) => EqualityComparer<TDestinationKey?>.Default.GetHashCode(obj.Key);
        }
    }

/// <summary>
/// Provides members for the DestinationEnumerator class.
/// </summary>
/// <param name="changes">The changes value.</param>
private sealed class DestinationEnumerator(IChangeSet<ManyContainer, TSourceKey> changes) : IEnumerable<Change<TDestination, TDestinationKey>>
    {
        /// <summary>
        /// Executes the GetEnumerator operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IEnumerator<Change<TDestination, TDestinationKey>> GetEnumerator()
        {
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                    case ChangeReason.Remove:
                    case ChangeReason.Refresh:
                        {
                            foreach (var destination in change.Current.Destination)
                            {
                                yield return new Change<TDestination, TDestinationKey>(change.Reason, destination.Key, destination.Item);
                            }
                        }

                        break;
                    case ChangeReason.Update:
                        {
                            var previousItems = change.Previous.Value.Destination.AsArray();
                            var currentItems = change.Current.Destination.AsArray();

                            var removes = previousItems.Except(currentItems, DestinationContainer.KeyComparer);
                            var adds = currentItems.Except(previousItems, DestinationContainer.KeyComparer);
                            var updates = currentItems.Intersect(previousItems, DestinationContainer.KeyComparer);

                            foreach (var destination in removes)
                            {
                                yield return new Change<TDestination, TDestinationKey>(ChangeReason.Remove, destination.Key, destination.Item);
                            }

                            foreach (var destination in adds)
                            {
                                yield return new Change<TDestination, TDestinationKey>(ChangeReason.Add, destination.Key, destination.Item);
                            }

                            foreach (var destination in updates)
                            {
                                var current = currentItems.First(d => d.Key.Equals(destination.Key));
                                var previous = previousItems.First(d => d.Key.Equals(destination.Key));

                                // Do not update is items are the same reference
                                if (!ReferenceEquals(current.Item, previous.Item))
                                {
                                    yield return new Change<TDestination, TDestinationKey>(ChangeReason.Update, destination.Key, current.Item, previous.Item);
                                }
                            }
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Executes the GetEnumerator operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

/// <summary>
/// Provides members for the ManyContainer class.
/// </summary>
/// <param name="initial">The initial value.</param>
/// <param name="changes">The changes value.</param>
private sealed class ManyContainer(Func<IEnumerable<DestinationContainer>> initial, IObservable<IChangeSet<TDestination, TDestinationKey>>? changes = null)
    {
        /// <summary>
        /// Gets the Changes value.
        /// </summary>
        public IObservable<IChangeSet<TDestination, TDestinationKey>> Changes { get; } = changes ?? Observable.Empty<IChangeSet<TDestination, TDestinationKey>>();

        /// <summary>
        /// Gets the Destination value.
        /// </summary>
        public IEnumerable<DestinationContainer> Destination => initial();
    }
}
