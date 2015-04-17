using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
	internal class SizeLimiter<T>
	{
		private readonly IObservable<IChangeSet<T>> _source;
		private readonly int _sizeLimit;
		private readonly bool _reportExpiredItemsOnly;
		private readonly IScheduler _scheduler;

		public SizeLimiter([NotNull] IObservable<IChangeSet<T>> source, int sizeLimit, bool reportExpiredItemsOnly, IScheduler scheduler = null)
		{
			if (source == null) throw new ArgumentNullException("source");
			if (sizeLimit <= 0) throw new ArgumentException("sizeLimit cannot be zero");
			_source = source;
			_sizeLimit = sizeLimit;
			_reportExpiredItemsOnly = reportExpiredItemsOnly;
			_scheduler = scheduler ?? Scheduler.Default;
		}

		/// <summary>
		/// Runs this instance.
		/// </summary>
		/// <returns></returns>
		public IObservable<IChangeSet<T>> Run()
		{
			return Observable.Create<IChangeSet<T>>(observer =>
			{
				var locker = new object();
				var dateTime =  DateTime.Now;
				int index = -1;

				var items = new ChangeAwareList<ExpirableItem<T>>();

				var expirableItems = _source
					.ObserveOn(_scheduler)
					.Synchronize(locker)
					.Do(_ => dateTime = DateTime.Now)
					.Select(changes => changes.Transform(t => new ExpirableItem<T>(t, dateTime, Interlocked.Increment(ref index))))
					.Subscribe(changes =>
					{
						//maintain local list 
						items.Clone(changes);

						//clear changes as if we only want reported change only
						if (_reportExpiredItemsOnly)
							items.ClearChanges();

						//check for items beyond size limit
						var itemstoexpire = items
							.OrderByDescending(exp => exp.ExpireAt)
							.Skip(_sizeLimit)
							.ToList();

						//TODO: Expire using remove range as it is more efficient
						itemstoexpire.ForEach(item=> items.Remove(item));

						//fire notifications
						var notifications = items.CaptureChanges().Transform(exp=>exp.Item);

						if (notifications.Count!=0)
							observer.OnNext(notifications);
					});



				return new CompositeDisposable(expirableItems);
			});
		}

	}
}