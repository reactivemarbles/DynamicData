using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class AutoRefresh<TObject, TAny>
    {
        private readonly IObservable<IChangeSet<TObject>> _source;
        private readonly Func<TObject,  IObservable<TAny>> _reevaluator;
        private readonly TimeSpan? _buffer;
        private readonly IScheduler _scheduler;

        public AutoRefresh(IObservable<IChangeSet<TObject>> source,
            Func<TObject,  IObservable<TAny>> reevaluator,
            TimeSpan? buffer = null,
            IScheduler scheduler = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _reevaluator = reevaluator ?? throw new ArgumentNullException(nameof(reevaluator));
            _buffer = buffer;
            _scheduler = scheduler;
        }

        public IObservable<IChangeSet<TObject>> Run()
        {
            return Observable.Create<IChangeSet<TObject>>(observer =>
            {
                var locker = new object();

                var allItems = new List<TObject>();

                var shared = _source
                    .Synchronize(locker)
                    .Clone(allItems) //clone all items so we can look up the index when a change has been made
                    .Publish();

                //monitor each item observable and create change
                var itemHasChanged = shared.MergeMany((t) => _reevaluator(t).Select(x => t));

                //create a changeset, either buffered or one item at the time
                IObservable<IEnumerable<TObject>> itemsChanged;
                if (_buffer == null)
                {
                    itemsChanged = itemHasChanged.Select(t => new[] {t});
                }
                else
                {
                    itemsChanged = itemHasChanged.Buffer(_buffer.Value, _scheduler ?? Scheduler.Default)
                        .Where(list => list.Any());
                }

                IObservable<IChangeSet<TObject>> requiresRefresh = itemsChanged
                    .Synchronize(locker)
                    .Select(items =>
                    {
                        //catch all the indices of items which have been refreshed
                        return allItems.IndexOfMany(items, (t, idx) => new Change<TObject>(ListChangeReason.Refresh, t, idx)).ToArray();
                    }).Select(changes => new ChangeSet<TObject>(changes));


                //publish refreshes and underlying changes
                var publisher = shared
                    .Merge(requiresRefresh)
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }
    }
}
