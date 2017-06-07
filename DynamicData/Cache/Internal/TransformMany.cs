using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>
    {
        private readonly IObservable<IChangeSet<TSource, TSourceKey>> _source;
        private readonly Func<TDestination, TDestinationKey> _keySelector;

        private readonly IObservable<IChangeSet<TDestination, TDestinationKey>> _run;

        public TransformMany(IObservable<IChangeSet<TSource, TSourceKey>> source,
            Func<TSource, IEnumerable<TDestination>> manyselector,
            Func<TDestination, TDestinationKey> keySelector)
        {
            _source = source;
            _keySelector = keySelector;
            _run = CreateObservable(manyselector);
        }

        public TransformMany(IObservable<IChangeSet<TSource, TSourceKey>> source,
            Func<TSource, ObservableCollection<TDestination>> manyselector,
            Func<TDestination, TDestinationKey> keySelector)
        {
            _source = source;
            _keySelector = keySelector;
            _run = CreateObservable(manyselector);
        }
           
        public IObservable<IChangeSet<TDestination, TDestinationKey>> Run()
        {
            return _run;
        }

        private IObservable<IChangeSet<TDestination, TDestinationKey>> CreateObservable(Func<TSource, IEnumerable<TDestination>> manyselector)
        {
            return _source.Transform((t, key) =>
                {
                    var many = manyselector(t)
                        .Select(m => new DestinationContainer(m, _keySelector(m)))
                        .ToArray();                              

                    return (IManyContainer) new ManyContainer(t, key, many);
                })
                .Select(changes => new ChangeSet<TDestination, TDestinationKey>(new DestinationEnumerator(changes)));
        }

        private IObservable<IChangeSet<TDestination, TDestinationKey>> CreateObservable(Func<TSource, ObservableCollection<TDestination>> manyselector)
        {
            return Observable.Create<IChangeSet<TDestination, TDestinationKey>>(observer =>
            {
                var result = new ChangeAwareCache<TDestination, TDestinationKey>();

                  var transformed = _source.Transform((t, key) =>
                {
                    //load the observable collection and any subsequent changes
                    var collection = manyselector(t);

                    var changes = collection.ToObservableChangeSet(_keySelector).Skip(1);     //maybe should not skip 1
                    return (IManyContainer) new ObservableContainer(t, key, () => collection.Select(m => new DestinationContainer(m, _keySelector(m))), changes);
                }).Publish();

                var intial = transformed
                    .Select(changes => new ChangeSet<TDestination, TDestinationKey>(new DestinationEnumerator(changes)))
                    .Select(changes => Process(result, changes, true));

                var subsequent = transformed.MergeMany(x => ((ObservableContainer) x).Changes);
                var allChanges = intial.Merge(subsequent);   //Add Defer()???

                var publisher = allChanges.SubscribeSafe(observer);

                return new CompositeDisposable(publisher, transformed.Connect());
            });
        }

        private IChangeSet<TDestination, TDestinationKey> Process(ChangeAwareCache<TDestination, TDestinationKey> target, IChangeSet<TDestination, TDestinationKey> changes, bool initial)
        {
            target.Clone(changes);
            return target.CaptureChanges();
        }

        private sealed class DestinationEnumerator : IEnumerable<Change<TDestination, TDestinationKey>>
        {
            private readonly IChangeSet<IManyContainer, TSourceKey> _changes;

            public DestinationEnumerator(IChangeSet<IManyContainer, TSourceKey> changes)
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

        private interface IManyContainer
        {
            TSource Source { get; }
            TSourceKey SourceKey { get; }
            IEnumerable<DestinationContainer> Destination { get; }
        }

        private sealed class ManyContainer : IManyContainer
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

        private sealed class ObservableContainer: IManyContainer
        {
            public TSource Source { get; }
            public TSourceKey SourceKey { get; }
            public IEnumerable<DestinationContainer> Destination => Initial();

            public Func<IEnumerable<DestinationContainer>> Initial { get; }
            public IObservable<IChangeSet<TDestination, TDestinationKey>> Changes { get; }

            public ObservableContainer(TSource source, TSourceKey sourceKey, 
                Func<IEnumerable<DestinationContainer>> initial, 
                IObservable<IChangeSet<TDestination, TDestinationKey>> changes)
            {
                Source = source;
                SourceKey = sourceKey;
                Initial = initial;
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
