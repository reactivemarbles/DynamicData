using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.List.Internal
{
    internal class FilterOnObservable<TObject>
    {
        private readonly IObservable<IChangeSet<TObject>> _source;
        private readonly Func<TObject, IObservable<bool>> _filter;
        private readonly TimeSpan? _buffer;
        private readonly IScheduler _scheduler;

        public FilterOnObservable(IObservable<IChangeSet<TObject>> source,
            Func<TObject,  IObservable<bool>> filter,
            TimeSpan? buffer = null,
            IScheduler scheduler = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _buffer = buffer;
            _scheduler = scheduler;
        }

        private readonly struct ObjWithFilterValue :IEquatable<ObjWithFilterValue>
        {
            public readonly TObject Obj;
            public readonly bool Filter;

            public ObjWithFilterValue(TObject obj, bool filter)
            {
                Obj = obj;
                Filter = filter;
            }

            private sealed class ObjFilterEqualityComparer : IEqualityComparer<ObjWithFilterValue>
            {
                public bool Equals(ObjWithFilterValue x, ObjWithFilterValue y)
                {
                    return EqualityComparer<TObject>.Default.Equals(x.Obj, y.Obj) && x.Filter == y.Filter;
                }

                public int GetHashCode(ObjWithFilterValue obj)
                {
                    unchecked
                    {
                        return (EqualityComparer<TObject>.Default.GetHashCode(obj.Obj) * 397) ^ obj.Filter.GetHashCode();
                    }
                }
            }

            private sealed class ObjEqualityComparer : IEqualityComparer<ObjWithFilterValue>
            {
                public bool Equals(ObjWithFilterValue x, ObjWithFilterValue y)
                {
                    return EqualityComparer<TObject>.Default.Equals(x.Obj, y.Obj);
                }

                public int GetHashCode(ObjWithFilterValue obj)
                {
                    unchecked
                    {
                        return (EqualityComparer<TObject>.Default.GetHashCode(obj.Obj) * 397);
                    }
                }
            }

            private static IEqualityComparer<ObjWithFilterValue> ObjComparer { get; } = new ObjEqualityComparer();
            
            public bool Equals(ObjWithFilterValue other)
            {
                // default equality does _not_ include Filter value, as that would cause the Filter operator that is used later to fail
                return ObjComparer.Equals(this, other);
            }
        }

        public IObservable<IChangeSet<TObject>> Run()
        {
            return Observable.Create<IChangeSet<TObject>>(observer =>
            {
                var locker = new object();

                var allItems = new List<ObjWithFilterValue>();

                var shared = _source
                    .Synchronize(locker)
                    // we default to true (include all items)
                    .Transform(v => new ObjWithFilterValue(v, true))
                    .Clone(allItems) //clone all items so we can look up the index when a change has been made
                    .Publish();

                //monitor each item observable and create change, carry the value of the observable property
                IObservable<ObjWithFilterValue> itemHasChanged = shared.MergeMany(v =>
                    _filter(v.Obj).Select(prop => new ObjWithFilterValue(v.Obj, prop)));

                //create a changeset, either buffered or one item at the time
                IObservable<IEnumerable<ObjWithFilterValue>> itemsChanged;
                if (_buffer == null)
                {
                    itemsChanged = itemHasChanged.Select(t => new[] {t});
                }
                else
                {
                    itemsChanged = itemHasChanged.Buffer(_buffer.Value, _scheduler ?? Scheduler.Default)
                        .Where(list => list.Any());
                }

                IObservable<IChangeSet<ObjWithFilterValue>> requiresRefresh = itemsChanged.Synchronize(locker)
                    .Select(items =>
                    {
                        //catch all the indices of items which have been refreshed
                        var indexOfMany = IndexOfMany(allItems,
                            items,
                            v => v.Obj,
                            (t, idx) => new Change<ObjWithFilterValue>(ListChangeReason.Refresh, t, idx));
                        return indexOfMany;
                    })
                    .Select(changes => new ChangeSet<ObjWithFilterValue>(changes));

                //publish refreshes and underlying changes
                var publisher = shared
                    .Merge(requiresRefresh)
                    .Filter(v => v.Filter)
                    .Transform(v => v.Obj)
                    // suppress refreshes from filter, avoids excessive refresh messages for no-op filter updates
                    .SupressRefresh()
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }

        /// <summary>
        /// Finds the index of many items as specified in the secondary enumerable.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="itemsToFind">The items to find.</param>
        /// <param name="objectPropertyFunc">Object property to join on</param>
        /// <param name="resultSelector">The result selector</param>
        /// <returns>A result as specified by the result selector</returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IEnumerable<TResult> IndexOfMany<TObject, TObjectProp, TResult>(
            [NotNull] IEnumerable<TObject> source, [NotNull] IEnumerable<TObject> itemsToFind,
            Func<TObject, TObjectProp> objectPropertyFunc, [NotNull] Func<TObject, int, TResult> resultSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (itemsToFind == null) throw new ArgumentNullException(nameof(itemsToFind));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            var indexed = source.Select((element, index) => new { Element = element, Index = index });
            return itemsToFind
                .Join(indexed, left => objectPropertyFunc(left), right => objectPropertyFunc(right.Element),
                    (left, right) => resultSelector(left, right.Index));
        }
    }
}
