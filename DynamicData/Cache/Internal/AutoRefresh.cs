using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;


namespace DynamicData.Cache.Internal
{
    internal class AutoRefresh<TObject, TKey, TAny>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TKey, IObservable<TAny>> _reevaluator;
        private readonly TimeSpan? _buffer;
        private readonly IScheduler _scheduler;

        public AutoRefresh(IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TKey, IObservable<TAny>> reevaluator,
            TimeSpan? buffer = null,
            IScheduler scheduler = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _reevaluator = reevaluator ?? throw new ArgumentNullException(nameof(reevaluator));
            _buffer = buffer;
            _scheduler = scheduler;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var shared = _source.Publish();

                //monitor each item observable and create change
                var changes = shared.MergeMany((t, k) =>
                {
                    return _reevaluator(t, k).Select(_ => new Change<TObject, TKey>(ChangeReason.Refresh, k, t));
                });

                //create a changeset, either buffered or one item at the time
                IObservable<IChangeSet<TObject, TKey>> refreshChanges;
                if (_buffer == null)
                {
                    refreshChanges = changes.Select(c => new ChangeSet<TObject, TKey>(new[] { c }));
                }
                else
                {
                    refreshChanges = changes.Buffer(_buffer.Value, _scheduler ?? Scheduler.Default)
                        .Where(list => list.Any())
                        .Select(items => new ChangeSet<TObject, TKey>(items));
                }


                //publish refreshes and underlying changes
                var locker = new object();
                var publisher = shared.Synchronize(locker)
                    .Merge(refreshChanges.Synchronize(locker))
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
        }
    }

}
