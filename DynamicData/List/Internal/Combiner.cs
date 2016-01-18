using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal sealed class Combiner<T>
    {
        private readonly IList<ReferenceCountTracker<T>> _sourceLists = new List<ReferenceCountTracker<T>>();
        private readonly ChangeAwareListWithRefCounts<T> _resultList = new ChangeAwareListWithRefCounts<T>();
        private readonly object _locker = new object();
        private readonly ICollection<IObservable<IChangeSet<T>>> _source;
        private readonly CombineOperator _type;

        public Combiner([NotNull] ICollection<IObservable<IChangeSet<T>>> source, CombineOperator type)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _source = source;
            _type = type;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var disposable = new CompositeDisposable();
                lock (_locker)
                {
                    foreach (var item in _source)
                    {
                        var list = new ReferenceCountTracker<T>();
                        _sourceLists.Add(list);

                        disposable.Add(item.Synchronize(_locker).Subscribe(changes =>
                        {
                            CloneSourceList(list, changes);

                            var notifications = UpdateResultList(changes);
                            if (notifications.Count!=0)
                                observer.OnNext(notifications);
                        }));
                    }
                }
                return disposable;
            });
        }

        private void CloneSourceList(ReferenceCountTracker<T> tracker, IChangeSet<T> changes)
        {
            changes.ForEach(change =>
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        tracker.Add(change.Item.Current);
                        break;
                    case ListChangeReason.AddRange:
                        foreach (var t in change.Range)
                            tracker.Add(t);
                        break;
                    case ListChangeReason.Replace:
                        tracker.Remove(change.Item.Previous.Value);
                        tracker.Add(change.Item.Current);
                        break;
                    case ListChangeReason.Remove:
                        tracker.Remove(change.Item.Current);
                        break;
                    case ListChangeReason.RemoveRange:
                    case ListChangeReason.Clear:
                        foreach (var t in change.Range)
                            tracker.Remove(t);
                        break;
                    //case ListChangeReason.Clear:
                    //    tracker.Clear();
                    //    break;
                }
            });
        }

        private IChangeSet<T> UpdateResultList(IChangeSet<T> changes)
        {
            //child caches have been updated before we reached this point.
            changes.Flatten().ForEach(change =>
            {
                var item = change.Current;
                var isInResult = _resultList.Contains(item);
                var shouldBeInResult = MatchesConstraint(item);

                if (shouldBeInResult)
                {
                    if (!isInResult)
                        _resultList.Add(item);
                }
                else
                {
                    if (isInResult)
                        _resultList.Remove(item);
                }

            });
            return _resultList.CaptureChanges();
        }

        private bool MatchesConstraint(T item)
        {
            switch (_type)
            {
                case CombineOperator.And:
                {
                    return _sourceLists.All(s => s.Contains(item));
                }
                case CombineOperator.Or:
                {
                    return _sourceLists.Any(s => s.Contains(item));
                }
                case CombineOperator.Xor:
                {
                    return _sourceLists.Count(s => s.Contains(item)) == 1;
                }
                case CombineOperator.Except:
                {
                    var first = _sourceLists[0].Contains(item);
                    var others = _sourceLists.Skip(1).Any(s => s.Contains(item));
                    return first && !others;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}