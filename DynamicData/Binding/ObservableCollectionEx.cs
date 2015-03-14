using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Binding
{
    /// <summary>
    /// Extensions to convert an observable collection into a dynamic stream
    /// </summary>
    public static class ObservableCollectionEx
    {
		/// <summary>
		/// Convert an observable collection into an observable change set
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="source">The source.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentNullException">source</exception>
		public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this  ObservableCollection<T> source)
        {

	        return Observable.Create<IChangeSet<T>>
		        (
			        observer =>
			        {
				        Func<ChangeSet<T>> initialChangeSet = () =>
				        {
					        var items = source.Select((t, index) => new Change<T>(ChangeReason.Add, t, index));
					        return new ChangeSet<T>(items);
				        };

				        //populate local cache, otherwise there is no way to deal with a reset
				        var cloneOfList = new SourceList<T>();

				        var sourceUpdates = Observable
					        .FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
						        h => source.CollectionChanged += h,
						        h => source.CollectionChanged -= h)
					        .Select
					        (
						        args =>
						        {
							        var changes = args.EventArgs;

							        switch (changes.Action)
							        {
								        case NotifyCollectionChangedAction.Add:
									        return changes.NewItems.OfType<T>()
										        .Select((t, index) => new Change<T>(ChangeReason.Add, t, index + changes.NewStartingIndex));

								        case NotifyCollectionChangedAction.Remove:
									        return changes.OldItems.OfType<T>()
										        .Select((t, index) => new Change<T>(ChangeReason.Remove, t, index + changes.OldStartingIndex));

								        case NotifyCollectionChangedAction.Replace:
								        {
									        return changes.NewItems.OfType<T>()
										        .Select((t, idx) =>
										        {
											        var old = changes.OldItems[idx];
											        return new Change<T>(ChangeReason.Update, t, (T) old, idx, idx);
										        });
								        }
								        case NotifyCollectionChangedAction.Reset:
								        {
									        //Clear all from the cache and reload
									        var removes = source.Select((t, index) => new Change<T>(ChangeReason.Remove, t, index)).Reverse();
									        return removes.Concat(initialChangeSet());
								        }
								        default:
									        return null;
							        }
						        })
					        .Where(updates => updates != null)
					        .Select(updates => (IChangeSet<T>) new ChangeSet<T>(updates));

				        var initialChanges = initialChangeSet();
				        var cacheLoader = Observable.Return(initialChanges).Concat(sourceUpdates).PopulateInto(cloneOfList);
				        var subscriber = cloneOfList.Connect().SubscribeSafe(observer);
				        return new CompositeDisposable(cacheLoader, subscriber, cloneOfList);
			        });
        }

        /// <summary>
        /// Convert an observable collection into a dynamic stream of change sets
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this  ObservableCollection<TObject> source, Func<TObject, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (keySelector == null) throw new ArgumentNullException("keySelector");

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        //populate local cache, otherwise there is no way to deal with a reset
                        var cloneOfList = new SourceCache<TObject, TKey>(keySelector);

                        Func<ChangeSet<TObject, TKey>> initialChangeSet = () =>
                        {
                            var items = source.Select(t => new Change<TObject, TKey>(ChangeReason.Add, keySelector(t), t));
                            return new ChangeSet<TObject, TKey>(items);
                        };

                        var sourceUpdates = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                                           h => source.CollectionChanged += h,
                                           h => source.CollectionChanged -= h)
                           .Select
                            (
                                args =>
                                {
                                    var changes = args.EventArgs;

                                    switch (changes.Action)
                                    {
                                        case NotifyCollectionChangedAction.Add:
                                            return changes.NewItems.OfType<TObject>()
                                                .Select(t => new Change<TObject, TKey>(ChangeReason.Add, keySelector(t), t));

                                        case NotifyCollectionChangedAction.Remove:
                                            return changes.OldItems.OfType<TObject>()
                                                .Select(t => new Change<TObject, TKey>(ChangeReason.Remove, keySelector(t), t));

                                        case NotifyCollectionChangedAction.Replace:
                                        {
                                            return changes.NewItems.OfType<TObject>()
                                                .Select((t, idx) =>
                                                {
                                                    var old = changes.OldItems[idx];
                                                    return new Change<TObject, TKey>(ChangeReason.Update, keySelector(t), t, (TObject)old);
                                                });
                                        }
                                        case NotifyCollectionChangedAction.Reset:
                                        {
                                            //Clear all from the cache and reload
                                            var removes = cloneOfList.KeyValues.Select(t => new Change<TObject, TKey>(ChangeReason.Remove, t.Key, t.Value)).ToArray();
                                            return removes.Concat(initialChangeSet());
                                        }
                                        default:
                                            return null;
                                    }
                                })
                            .Where(updates => updates != null)
                            .Select(updates => (IChangeSet<TObject, TKey>)new ChangeSet<TObject, TKey>(updates));


                        var initialChanges = initialChangeSet();
                        var cacheLoader=Observable.Return(initialChanges).Concat(sourceUpdates).PopulateInto(cloneOfList);
                        var subscriber = cloneOfList.Connect().SubscribeSafe(observer);
                        return new CompositeDisposable(cacheLoader, subscriber, cloneOfList);
                    }

                );
        }
    }
}