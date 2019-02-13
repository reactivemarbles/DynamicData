using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Binding;

namespace DynamicData.List.Internal
{
    internal class FilterOnObservable<TObject, TProperty>
    {
        private readonly IObservable<IChangeSet<TObject>> _source;
        private readonly Func<TObject,  IObservable<TProperty>> _reevaluator;
        private readonly Func<TObject, TProperty, bool> _filter;
        private readonly TimeSpan? _buffer;
        private readonly IScheduler _scheduler;

        public FilterOnObservable(IObservable<IChangeSet<TObject>> source,
            Func<TObject,  IObservable<TProperty>> reevaluator,
            Func<TObject, TProperty, bool> filter,
            TimeSpan? buffer = null,
            IScheduler scheduler = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _reevaluator = reevaluator ?? throw new ArgumentNullException(nameof(reevaluator));
            _filter = filter;
            _buffer = buffer;
            _scheduler = scheduler;
        }

        private class ObjWithPropValue : IEquatable<ObjWithPropValue>
        {
            public TObject Obj;
            public TProperty Prop;

            public ObjWithPropValue(TObject obj, TProperty prop)
            {
                Obj = obj;
                Prop = prop;
            }

            public bool Equals(ObjWithPropValue other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return EqualityComparer<TObject>.Default.Equals(Obj, other.Obj);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ObjWithPropValue) obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<TObject>.Default.GetHashCode(Obj);
            }
        }

        public IObservable<IChangeSet<TObject>> Run()
        {
            return Observable.Create<IChangeSet<TObject>>(observer =>
            {
                var locker = new object();

                var allItems = new List<ObjWithPropValue>();

                var shared = _source
                    .Synchronize(locker)
                    .Transform(v => new ObjWithPropValue(v, default))
                    .Clone(allItems) //clone all items so we can look up the index when a change has been made
                    .Publish();

                //monitor each item observable and create change, carry the value of the observable property
                IObservable<ObjWithPropValue> itemHasChanged = shared.MergeMany(v => _reevaluator(v.Obj)
                    .Select(prop => new ObjWithPropValue(v.Obj, prop)));

                //create a changeset, either buffered or one item at the time
                IObservable<IEnumerable<ObjWithPropValue>> itemsChanged;
                if (_buffer == null)
                {
                    itemsChanged = itemHasChanged.Select(t => new[] {t});
                }
                else
                {
                    itemsChanged = itemHasChanged.Buffer(_buffer.Value, _scheduler ?? Scheduler.Default)
                        .Where(list => list.Any());
                }

                IObservable<IChangeSet<ObjWithPropValue>> requiresRefresh = itemsChanged.Synchronize(locker)
                    .Select(items =>
                    {
                        //catch all the indices of items which have been refreshed
                        return IndexOfMany(allItems,
                            items,
                            v => v.Obj,
                            (t, idx) => new Change<ObjWithPropValue>(ListChangeReason.Refresh, t, idx));
                    })
                    .Select(changes => new ChangeSet<ObjWithPropValue>(changes));


                //publish refreshes and underlying changes
                var publisher = shared
                    .Merge(requiresRefresh)
                    .Filter(v => _filter(v.Obj, v.Prop))
                    .Transform(v => v.Obj)
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
