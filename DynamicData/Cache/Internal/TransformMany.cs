using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>
    {
        private readonly IObservable<IChangeSet<TSource, TSourceKey>> _source;
        private readonly Func<TSource, IObservable<IChangeSet<TDestination, TDestinationKey>>> _childChanges;
        private readonly Func<TSource, IEnumerable<TDestination>> _manyselector;
        private readonly Func<TDestination, TDestinationKey> _keySelector;

        public TransformMany(IObservable<IChangeSet<TSource, TSourceKey>> source,
            Func<TSource, IEnumerable<TDestination>> manyselector,
            Func<TDestination, TDestinationKey> keySelector,
            Func<TSource, IObservable<IChangeSet<TDestination, TDestinationKey>>> childChanges = null)
        {
            _source = source;
            _manyselector = manyselector;
            _keySelector = keySelector;
            _childChanges = childChanges;
        }
           
        public IObservable<IChangeSet<TDestination, TDestinationKey>> Run()
        {
            return _childChanges == null 
                ? Create()
                : CreateWithChangeset();
        }

        private IObservable<IChangeSet<TDestination, TDestinationKey>> Create()
        {
            return _source.Transform((t, key) =>
                {
                    return new ManyContainer(()=> _manyselector(t).Select(m => new DestinationContainer(m, _keySelector(m))));
                })
                .Select(changes => new ChangeSet<TDestination, TDestinationKey>(new DestinationEnumerator(changes)));
        }

        private IObservable<IChangeSet<TDestination, TDestinationKey>> CreateWithChangeset()
        {
            return Observable.Create<IChangeSet<TDestination, TDestinationKey>>(observer =>
            {
                var result = new ChangeAwareCache<TDestination, TDestinationKey>();
              

                var transformed = _source.Transform((t, key) =>
                {
                    var locker = new object();
                    var collection = _manyselector(t);
                    var changes = _childChanges(t).Synchronize(locker).Skip(1);
                    return new ManyContainer(() =>
                    {
                        lock (locker)
                            return collection.Select(m => new DestinationContainer(m, _keySelector(m)));
                   
                    }, changes);
                }).Publish();

                var outerLock = new object();
                var intial = transformed
                    .Synchronize(outerLock)
                    .Select(changes => new ChangeSet<TDestination, TDestinationKey>(new DestinationEnumerator(changes)));

                var subsequent = transformed
                    .MergeMany(x => x.Changes)
                    .Synchronize(outerLock);

                var allChanges = intial.Merge(subsequent).Select(changes =>
                {
                    result.Clone(changes);
                    return result.CaptureChanges();
                });

                return new CompositeDisposable(allChanges.SubscribeSafe(observer), transformed.Connect());
            });
        }

        private sealed class DestinationEnumerator : IEnumerable<Change<TDestination, TDestinationKey>>
        {
            private readonly IChangeSet<ManyContainer, TSourceKey> _changes;

            public DestinationEnumerator(IChangeSet<ManyContainer, TSourceKey> changes)
            {
                _changes = changes;
            }

            public IEnumerator<Change<TDestination, TDestinationKey>> GetEnumerator()
            {
                foreach (var change in _changes)
                {
                    switch (change.Reason)
                    {
                        case ChangeReason.Add:
                        case ChangeReason.Remove:
                        case ChangeReason.Refresh:
                            {
                                foreach (var destination in change.Current.Destination)
                                    yield return new Change<TDestination, TDestinationKey>(change.Reason, destination.Key, destination.Item);
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
                                    yield return new Change<TDestination, TDestinationKey>(ChangeReason.Remove, destination.Key, destination.Item);

                                foreach (var destination in adds)
                                    yield return new Change<TDestination, TDestinationKey>(ChangeReason.Add, destination.Key, destination.Item);

                                foreach (var destination in updates)
                                {
                                    var current = currentItems.First(d => d.Key.Equals(destination.Key));
                                    var previous = previousItems.First(d => d.Key.Equals(destination.Key));

                                    //Do not update is items are the same reference
                                    if (!ReferenceEquals(current.Item, previous.Item))
                                        yield return new Change<TDestination, TDestinationKey>(ChangeReason.Update, destination.Key, current.Item, previous.Item);
                                }
                            }
                            break;
                    }
                }
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private sealed class ManyContainer
        {
            private readonly Func<IEnumerable<DestinationContainer>> _initial;

            public IObservable<IChangeSet<TDestination, TDestinationKey>> Changes { get; }
            public IEnumerable<DestinationContainer> Destination => _initial();

            public ManyContainer(Func<IEnumerable<DestinationContainer>> initial, IObservable<IChangeSet<TDestination, TDestinationKey>> changes = null)
            {
                _initial = initial;
                Changes = changes;
            }
        }

        private sealed class DestinationContainer
        {
            public TDestination Item { get; }
            public TDestinationKey Key { get; }

            public DestinationContainer(TDestination item, TDestinationKey key)
            {
                Item = item;
                Key = key;
            }

            #region Equality Comparer

            private sealed class KeyEqualityComparer : IEqualityComparer<DestinationContainer>
            {
                public bool Equals(DestinationContainer x, DestinationContainer y)
                {
                    if (ReferenceEquals(x, y)) return true;
                    if (ReferenceEquals(x, null)) return false;
                    if (ReferenceEquals(y, null)) return false;
                    if (x.GetType() != y.GetType()) return false;
                    return EqualityComparer<TDestinationKey>.Default.Equals(x.Key, y.Key);
                }

                public int GetHashCode(DestinationContainer obj)
                {
                    return EqualityComparer<TDestinationKey>.Default.GetHashCode(obj.Key);
                }
            }

            public static IEqualityComparer<DestinationContainer> KeyComparer { get; } = new KeyEqualityComparer();

            #endregion
        }
    }
}
