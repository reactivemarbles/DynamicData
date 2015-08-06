using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Controllers;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    //TODO: Implement seperate ClearAndReplace and CalculateDiffSet filters??

    internal class MutableFilter<T>
	{
		private readonly List<ItemWithMatch> _allWithMatch = new List<ItemWithMatch>();
        private readonly List<T> _all = new List<T>();
        private readonly ChangeAwareList<T> _filtered = new ChangeAwareList<T>();


	    private readonly FilterPolicy _filterPolicy;
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly IObservable<Func<T, bool>> _predicates;


        private Func<T, bool> _predicate=t=>false;

		public MutableFilter([NotNull] IObservable<IChangeSet<T>> source, 
            [NotNull] IObservable<Func<T, bool>> predicates,
            FilterPolicy filterPolicy= FilterPolicy.ClearAndReplace)
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (predicates == null) throw new ArgumentNullException(nameof(predicates));
			_source = source;
            _predicates = predicates;
		    _filterPolicy = filterPolicy;
		}
		
		public IObservable<IChangeSet<T>> Run()
		{
			return Observable.Create<IChangeSet<T>>(observer =>
			{
				var locker = new object();


				//requery wehn controller either fires changed or requery event
				var refresher = _predicates.Synchronize(locker)
					.Select(predicate =>
					{
						Requery(predicate);
						return _filtered.CaptureChanges();
					});
				
				var shared = _source.Synchronize(locker).Publish();

				//take current filter state of all items
			    IDisposable updateall;

			    if (_filterPolicy == FilterPolicy.ClearAndReplace)
			    {
                    updateall = shared.Synchronize(locker)
                                    .Subscribe(_all.Clone);
                }
			    else
			    {
                    updateall = shared.Synchronize(locker)
                                    .Transform(t => new ItemWithMatch(t, _predicate(t)))
                                    .Subscribe(_allWithMatch.Clone);
                }
                
				//filter result list
				var filter = shared.Synchronize(locker)
									.Select(changes =>
									{
										_filtered.Filter(changes, _predicate);
										return _filtered.CaptureChanges();
									});

				var subscriber = refresher.Merge(filter).NotEmpty().SubscribeSafe(observer);

				return new CompositeDisposable(updateall, subscriber, shared.Connect());
			});
		}

        //TODO: Need to account for re-evaluate (as it is not mutually excluse to clear and replace)

		private void Requery(Func<T, bool> predicate)
		{
			_predicate = predicate;

		    if (_filterPolicy == FilterPolicy.ClearAndReplace)
		    {
                _filtered.Clear();
                _filtered.AddRange(_all.Where(_predicate));

                return;
		    }


			var newState = _allWithMatch.Select(item =>
			{
				var match = _predicate(item.Item);
				var wasMatch = item.IsMatch;

				//reflect filtered state
				if (item.IsMatch != match) item.IsMatch = match;

				return new
				{
					Item = item,
					IsMatch = match,
					WasMatch = wasMatch
				};
			}).ToList();

			//reflect items which are no longer matched
			var noLongerMatched = newState.Where(state => !state.IsMatch && state.WasMatch).Select(state => state.Item.Item);
            _filtered.RemoveMany(noLongerMatched);

            //reflect new matches in the list
            var newMatched = newState.Where(state => state.IsMatch && !state.WasMatch).Select(state => state.Item.Item);
			_filtered.AddRange(newMatched);
		}

		private class ItemWithMatch
		{
			public T Item { get; }
			public bool IsMatch { get; set; }

			public ItemWithMatch(T item, bool isMatch)
			{
				Item = item;
				IsMatch = isMatch;
			}
		}

	}
}