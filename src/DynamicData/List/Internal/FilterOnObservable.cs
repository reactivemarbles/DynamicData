// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal class FilterOnObservable<TObject>
{
    private readonly TimeSpan? _buffer;

    private readonly Func<TObject, IObservable<bool>> _filter;

    private readonly IScheduler? _scheduler;

    private readonly IObservable<IChangeSet<TObject>> _source;

    public FilterOnObservable(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<bool>> filter, TimeSpan? buffer = null, IScheduler? scheduler = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        _buffer = buffer;
        _scheduler = scheduler;
    }

    public IObservable<IChangeSet<TObject>> Run()
    {
        return Observable.Create<IChangeSet<TObject>>(
            observer =>
            {
                var locker = new object();

                var allItems = new List<ObjWithFilterValue>();

                var shared = _source.Synchronize(locker).Transform(v => new ObjWithFilterValue(v, true)) // we default to true (include all items)
                    .Clone(allItems) // clone all items so we can look up the index when a change has been made
                    .Publish();

                // monitor each item observable and create change, carry the value of the observable property
                var itemHasChanged = shared.MergeMany(v => _filter(v.Obj).Select(prop => new ObjWithFilterValue(v.Obj, prop)));

                // create a change set, either buffered or one item at the time
                var itemsChanged = _buffer is null ?
                    itemHasChanged.Select(t => new[] { t }) :
                    itemHasChanged.Buffer(_buffer.Value, _scheduler ?? Scheduler.Default).Where(list => list.Count > 0);

                var requiresRefresh = itemsChanged.Synchronize(locker).Select(
                    items =>
                    {
                        // catch all the indices of items which have been refreshed
                        return IndexOfMany(allItems, items, v => v.Obj, (t, idx) => new Change<ObjWithFilterValue>(ListChangeReason.Refresh, t, idx));
                    }).Select(changes => new ChangeSet<ObjWithFilterValue>(changes));

                // publish refreshes and underlying changes
                var publisher = shared.Merge(requiresRefresh).Filter(v => v.Filter)
                    .Transform(v => v.Obj)
                    .SuppressRefresh() // suppress refreshes from filter, avoids excessive refresh messages for no-op filter updates
                    .NotEmpty()
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
    }

    private static IEnumerable<TResult> IndexOfMany<TObj, TObjectProp, TResult>(IEnumerable<TObj> source, IEnumerable<TObj> itemsToFind, Func<TObj, TObjectProp> objectPropertyFunc, Func<TObj, int, TResult> resultSelector)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (itemsToFind is null)
        {
            throw new ArgumentNullException(nameof(itemsToFind));
        }

        if (resultSelector is null)
        {
            throw new ArgumentNullException(nameof(resultSelector));
        }

        var indexed = source.Select((element, index) => new { Element = element, Index = index });
        return itemsToFind.Join(indexed, objectPropertyFunc, right => objectPropertyFunc(right.Element), (left, right) => resultSelector(left, right.Index));
    }

    private readonly struct ObjWithFilterValue : IEquatable<ObjWithFilterValue>
    {
        public readonly TObject Obj;

        public readonly bool Filter;

        public ObjWithFilterValue(TObject obj, bool filter)
        {
            Obj = obj;
            Filter = filter;
        }

        private static IEqualityComparer<ObjWithFilterValue> ObjComparer { get; } = new ObjEqualityComparer();

        public bool Equals(ObjWithFilterValue other)
        {
            // default equality does _not_ include Filter value, as that would cause the Filter operator that is used later to fail
            return ObjComparer.Equals(this, other);
        }

        public override bool Equals(object? obj)
        {
            return obj is ObjWithFilterValue value && Equals(value);
        }

        public override int GetHashCode()
        {
            return ObjComparer.GetHashCode(this);
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
                    return (obj.Obj is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(obj.Obj)) * 397;
                }
            }
        }
    }
}
