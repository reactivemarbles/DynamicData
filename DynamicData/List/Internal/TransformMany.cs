using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class TransformMany<TSource, TDestination>
    {
        private readonly IObservable<IChangeSet<TSource>> _source;
        private readonly Func<TSource, IEnumerable<TDestination>> _manyselector;
        private readonly Func<TSource, IObservable<IChangeSet<TDestination>>> _childChanges;
        private readonly IEqualityComparer<TDestination> _equalityComparer;


        public TransformMany([NotNull] IObservable<IChangeSet<TSource>> source,
            [NotNull] Func<TSource, IEnumerable<TDestination>> manyselector,
            IEqualityComparer<TDestination> equalityComparer = null,
            Func<TSource, IObservable<IChangeSet<TDestination>>> childChanges = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _manyselector = manyselector;
            _childChanges = childChanges;
            _equalityComparer = equalityComparer ?? EqualityComparer<TDestination>.Default;
        }

        public IObservable<IChangeSet<TDestination>> Run()
        {
            if (_childChanges != null)
                return CreateWithChangeset();

            return _source.Transform(item => new ManyContainer(item, _manyselector(item)))
                .Select(changes => new ChangeSet<TDestination>(new DestinationEnumerator(changes, _equalityComparer))).NotEmpty();
        }
          
        private IObservable<IChangeSet<TDestination>> CreateWithChangeset()
        {
            return Observable.Create<IChangeSet<TDestination>>(observer =>
            {
                var result = new ChangeAwareList<TDestination>();

                var transformed = _source.Transform(t =>
                {
                    var locker = new object();
                    var collection = _manyselector(t);
                    var changes = _childChanges(t).Synchronize(locker).Skip(1);
                    return new ManyContainer(t, collection, changes);
                })
                .Publish();

                var outerLock = new object();
                var intial = transformed
                    .Synchronize(outerLock)
                    .Select(changes => new ChangeSet<TDestination>(new DestinationEnumerator(changes, _equalityComparer)));

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

        private sealed class ManyContainer
        {
            public TSource Source { get; }
            public IEnumerable<TDestination> Destination { get; }
            public IObservable<IChangeSet<TDestination>> Changes { get; }

            public ManyContainer(TSource source, IEnumerable<TDestination> destination, IObservable<IChangeSet<TDestination>> changes = null)
            {
                Source = source;
                Destination = destination;
                Changes = changes;
            }
        }
        //make this an instance
        private sealed class DestinationEnumerator : IEnumerable<Change<TDestination>>
        {
            private readonly IChangeSet<ManyContainer> _changes;
            private readonly IEqualityComparer<TDestination> _equalityComparer;

            public DestinationEnumerator(IChangeSet<ManyContainer> changes, IEqualityComparer<TDestination> equalityComparer)
            {
                _changes = changes;
                _equalityComparer = equalityComparer;
            }

            public IEnumerator<Change<TDestination>> GetEnumerator()
            {
                foreach (var change in _changes)
                {

                    switch (change.Reason)
                    {
                        case ListChangeReason.Add:
                            foreach (var destination in change.Item.Current.Destination)
                                yield return new Change<TDestination>(change.Reason, destination);
                            break;
                        case ListChangeReason.AddRange:
                        {
                            var items = change.Range.SelectMany(m => m.Destination);
                            yield return new Change<TDestination>(change.Reason, items);
                        }
                            break;
                        case ListChangeReason.Replace:
                        {
                            //this is difficult as we need to discover adds and removes (and perhaps replaced)
                            var currentItems = change.Item.Current.Destination.AsArray();
                            var previousItems = change.Item.Previous.Value.Destination.AsArray();

                            var adds = currentItems.Except(previousItems, _equalityComparer);
                            var removes = previousItems.Except(currentItems, _equalityComparer);

                            //I am not sure whether it is possible to translate the original change into a replace
                            foreach (var destination in removes)
                                yield return new Change<TDestination>(ListChangeReason.Remove, destination);

                            foreach (var destination in adds)
                                yield return new Change<TDestination>(ListChangeReason.Add, destination);
                        }
                            break;
                        case ListChangeReason.Remove:
                            foreach (var destination in change.Item.Current.Destination)
                                yield return new Change<TDestination>(change.Reason, destination);
                            break;
                        case ListChangeReason.RemoveRange:
                        {
                            var toRemove = change.Range.SelectMany(m => m.Destination);

                            foreach (var destination in toRemove)
                                yield return new Change<TDestination>(ListChangeReason.Remove, destination);
                        }
                            break;
                        case ListChangeReason.Moved:
                            //do nothing as the original index has no bearing on the destination index
                            break;

                        case ListChangeReason.Clear:
                        {
                            var items = change.Range.SelectMany(m => m.Destination);
                            yield return new Change<TDestination>(change.Reason, items);
                        }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
