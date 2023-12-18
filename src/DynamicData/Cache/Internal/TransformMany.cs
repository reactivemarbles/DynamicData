// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Binding;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector, Func<TSource, IObservable<IChangeSet<TDestination, TDestinationKey>>>? childChanges = null)
    where TDestination : notnull
    where TDestinationKey : notnull
    where TSource : notnull
    where TSourceKey : notnull
{
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

    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => childChanges is null ? Create() : CreateWithChangeSet();

    private IObservable<IChangeSet<TDestination, TDestinationKey>> Create() => source.Transform(
            (t, _) =>
            {
                var destination = manySelector(t).Select(m => new DestinationContainer(m, keySelector(m))).ToArray();
                return new ManyContainer(() => destination);
            },
            true).Select(changes => new ChangeSet<TDestination, TDestinationKey>(new DestinationEnumerator(changes)));

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
                        var locker = new object();
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

                var outerLock = new object();
                var initial = transformed.Synchronize(outerLock).Select(changes => new ChangeSet<TDestination, TDestinationKey>(new DestinationEnumerator(changes)));

                var subsequent = transformed.MergeMany(x => x.Changes).Synchronize(outerLock);

                var allChanges = initial.Merge(subsequent).Select(
                    changes =>
                    {
                        result.Clone(changes);
                        return result.CaptureChanges();
                    });

                return new CompositeDisposable(allChanges.SubscribeSafe(observer), transformed.Connect());
            });
    }

    private sealed class DestinationContainer(TDestination item, TDestinationKey key)
    {
        public static IEqualityComparer<DestinationContainer> KeyComparer { get; } = new KeyEqualityComparer();

        public TDestination Item { get; } = item;

        public TDestinationKey Key { get; } = key;

        private sealed class KeyEqualityComparer : IEqualityComparer<DestinationContainer>
        {
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

            public int GetHashCode(DestinationContainer obj) => EqualityComparer<TDestinationKey?>.Default.GetHashCode(obj.Key);
        }
    }

    private sealed class DestinationEnumerator(IChangeSet<ManyContainer, TSourceKey> changes) : IEnumerable<Change<TDestination, TDestinationKey>>
    {
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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ManyContainer(Func<IEnumerable<DestinationContainer>> initial, IObservable<IChangeSet<TDestination, TDestinationKey>>? changes = null)
    {
        public IObservable<IChangeSet<TDestination, TDestinationKey>> Changes { get; } = changes ?? Observable.Empty<IChangeSet<TDestination, TDestinationKey>>();

        public IEnumerable<DestinationContainer> Destination => initial();
    }
}
