// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
#if WINUI3UWP
using DynamicData.Binding.WinUI3UWP;
#else
using System.Collections.ObjectModel;
#endif
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Binding;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class TransformMany<TSource, TDestination>
    {
        private readonly Func<TSource, IObservable<IChangeSet<TDestination>>>? _childChanges;

        private readonly IEqualityComparer<TDestination> _equalityComparer;

        private readonly Func<TSource, IEnumerable<TDestination>> _manySelector;

        private readonly IObservable<IChangeSet<TSource>> _source;

        public TransformMany(IObservable<IChangeSet<TSource>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
            : this(
                source,
                manySelector,
                equalityComparer,
                t => Observable.Defer(
                    () =>
                        {
                            var subsequentChanges = manySelector(t).ToObservableChangeSet();

                            if (manySelector(t).Count > 0)
                            {
                                return subsequentChanges;
                            }

                            return Observable.Return(ChangeSet<TDestination>.Empty).Concat(subsequentChanges);
                        }))
        {
        }

        public TransformMany(IObservable<IChangeSet<TSource>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
            : this(
                source,
                manySelector,
                equalityComparer,
                t => Observable.Defer(
                    () =>
                        {
                            var subsequentChanges = manySelector(t).ToObservableChangeSet();

                            if (manySelector(t).Count > 0)
                            {
                                return subsequentChanges;
                            }

                            return Observable.Return(ChangeSet<TDestination>.Empty).Concat(subsequentChanges);
                        }))
        {
        }

        public TransformMany(IObservable<IChangeSet<TSource>> source, Func<TSource, IObservableList<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
            : this(
                source,
                s => new ManySelectorFunc(s, x => manySelector(x).Items),
                equalityComparer,
                t => Observable.Defer(
                    () =>
                        {
                            var subsequentChanges = manySelector(t).Connect();

                            if (manySelector(t).Count > 0)
                            {
                                return subsequentChanges;
                            }

                            return Observable.Return(ChangeSet<TDestination>.Empty).Concat(subsequentChanges);
                        }))
        {
        }

        public TransformMany(IObservable<IChangeSet<TSource>> source, Func<TSource, IEnumerable<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, Func<TSource, IObservable<IChangeSet<TDestination>>>? childChanges = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _manySelector = manySelector;
            _childChanges = childChanges;
            _equalityComparer = equalityComparer ?? EqualityComparer<TDestination>.Default;
        }

        public IObservable<IChangeSet<TDestination>> Run()
        {
            if (_childChanges is not null)
            {
                return CreateWithChangeSet();
            }

            return Observable.Create<IChangeSet<TDestination>>(
                observer =>
                    {
                        // NB: ChangeAwareList is used internally by dd to capture changes to a list and ensure they can be replayed by subsequent operators
                        var result = new ChangeAwareList<TDestination>();

                        return _source.Transform(item => new ManyContainer(_manySelector(item).ToArray()), true).Select(
                            changes =>
                                {
                                    var destinationChanges = new DestinationEnumerator(changes, _equalityComparer);
                                    result.Clone(destinationChanges, _equalityComparer);
                                    return result.CaptureChanges();
                                }).NotEmpty().SubscribeSafe(observer);
                    });
        }

        private IObservable<IChangeSet<TDestination>> CreateWithChangeSet()
        {
            if (_childChanges is null)
            {
                throw new InvalidOperationException("_childChanges must not be null.");
            }

            return Observable.Create<IChangeSet<TDestination>>(
                observer =>
                    {
                        var result = new ChangeAwareList<TDestination>();

                        var transformed = _source.Transform(
                            t =>
                                {
                                    var locker = new object();
                                    var collection = _manySelector(t);
                                    var changes = _childChanges(t).Synchronize(locker).Skip(1);
                                    return new ManyContainer(collection, changes);
                                }).Publish();

                        var outerLock = new object();
                        var initial = transformed.Synchronize(outerLock).Select(changes => new ChangeSet<TDestination>(new DestinationEnumerator(changes, _equalityComparer)));

                        var subsequent = transformed.MergeMany(x => x.Changes).Synchronize(outerLock);

                        var init = initial.Select(
                            changes =>
                                {
                                    result.Clone(changes, _equalityComparer);
                                    return result.CaptureChanges();
                                });

                        var subsequentSelection = subsequent.RemoveIndex().Select(
                            changes =>
                                {
                                    result.Clone(changes, _equalityComparer);
                                    return result.CaptureChanges();
                                });

                        var allChanges = init.Merge(subsequentSelection);

                        return new CompositeDisposable(allChanges.SubscribeSafe(observer), transformed.Connect());
                    });
        }

        // make this an instance
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
                            {
                                yield return new Change<TDestination>(change.Reason, destination);
                            }

                            break;

                        case ListChangeReason.AddRange:
                            {
                                var items = change.Range.SelectMany(m => m.Destination);
                                yield return new Change<TDestination>(change.Reason, items);
                            }

                            break;

                        case ListChangeReason.Replace:
                        case ListChangeReason.Refresh:
                            {
                                // this is difficult as we need to discover adds and removes (and perhaps replaced)
                                var currentItems = change.Item.Current.Destination.AsArray();
                                var previousItems = change.Item.Previous.Value.Destination.AsArray();

                                var adds = currentItems.Except(previousItems, _equalityComparer);

                                // I am not sure whether it is possible to translate the original change into a replace
                                foreach (var destination in previousItems.Except(currentItems, _equalityComparer))
                                {
                                    yield return new Change<TDestination>(ListChangeReason.Remove, destination);
                                }

                                foreach (var destination in adds)
                                {
                                    yield return new Change<TDestination>(ListChangeReason.Add, destination);
                                }
                            }

                            break;

                        case ListChangeReason.Remove:
                            foreach (var destination in change.Item.Current.Destination)
                            {
                                yield return new Change<TDestination>(change.Reason, destination);
                            }

                            break;

                        case ListChangeReason.RemoveRange:
                            {
                                foreach (var destination in change.Range.SelectMany(m => m.Destination))
                                {
                                    yield return new Change<TDestination>(ListChangeReason.Remove, destination);
                                }
                            }

                            break;

                        case ListChangeReason.Moved:
                            // do nothing as the original index has no bearing on the destination index
                            break;

                        case ListChangeReason.Clear:
                            {
                                var items = change.Range.SelectMany(m => m.Destination);
                                yield return new Change<TDestination>(change.Reason, items);
                            }

                            break;

                        default:
                            throw new IndexOutOfRangeException("Unknown list reason " + change);
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
            public ManyContainer(IEnumerable<TDestination> destination, IObservable<IChangeSet<TDestination>>? changes = null)
            {
                Destination = destination;
                Changes = changes ?? Observable.Empty<IChangeSet<TDestination>>();
            }

            public IObservable<IChangeSet<TDestination>> Changes { get; }

            public IEnumerable<TDestination> Destination { get; }
        }

        private class ManySelectorFunc : IEnumerable<TDestination>
        {
            private readonly TSource _source;

            private readonly Func<TSource, IEnumerable<TDestination>> _selector;

            public ManySelectorFunc(TSource source, Func<TSource, IEnumerable<TDestination>> selector)
            {
                _source = source;
                _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            }

            public IEnumerator<TDestination> GetEnumerator() => _selector(_source).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => _selector(_source).GetEnumerator();
        }
    }
}