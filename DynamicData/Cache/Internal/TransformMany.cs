using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>
    {
        private readonly IObservable<IChangeSet<TSource, TSourceKey>> _source;
        private readonly Func<TSource, IEnumerable<TDestination>> _manyselector;
        private readonly Func<TDestination, TDestinationKey> _keySelector;

        public TransformMany(IObservable<IChangeSet<TSource, TSourceKey>> source,
                                                         Func<TSource, IEnumerable<TDestination>> manyselector,
                                                         Func<TDestination, TDestinationKey> keySelector)
        {
            _source = source;
            _manyselector = manyselector;
            _keySelector = keySelector;
        }

        public IObservable<IChangeSet<TDestination, TDestinationKey>> Run()
        {
            return _source.Transform((source, key) =>
                {
                    var many = _manyselector(source)
                        .Select(m => new DestinationContainer(m, _keySelector(m)))
                        .ToArray();

                    return new ManyContainer(source, key, many);
                })
                .Select(changes =>
                {
                    var items = new DestinationEnumerator(changes);
                    return new ChangeSet<TDestination, TDestinationKey>(items);
                });
        }

        private class DestinationEnumerator : IEnumerable<Change<TDestination, TDestinationKey>>
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
                        case ChangeReason.Evaluate:
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
            public TSource Source { get; }
            public TSourceKey SourceKey { get; }
            public IEnumerable<DestinationContainer> Destination { get; }

            public ManyContainer(TSource source, TSourceKey sourceKey, IEnumerable<DestinationContainer> destination)
            {
                Source = source;
                SourceKey = sourceKey;
                Destination = destination;
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

            private static readonly IEqualityComparer<DestinationContainer> s_keyComparerInstance = new KeyEqualityComparer();

            public static IEqualityComparer<DestinationContainer> KeyComparer
            {
                get { return s_keyComparerInstance; }
            }

            #endregion
        }
    }
}
