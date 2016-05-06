#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using DynamicData.Controllers;
using DynamicData.Internal;
using DynamicData.Kernel;

#endregion

namespace DynamicData
{
    /// <summary>
    /// The entry point for the dynamic data sub system
    /// </summary>
    public static class ObservableCacheEx
    {
        #region Populate changetset from observables



        /// <summary>
        /// Converts the observable to an observable changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache</param>
        /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(
            this IObservable<TObject> source,
            Func<TObject, TKey> keySelector,
            Func<TObject, TimeSpan?> expireAfter = null,
            int limitSizeTo = -1,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var cache = new SourceCache<TObject, TKey>(keySelector);
                var sourceSubscriber = source.Subscribe(cache.AddOrUpdate);

                var expirer = expireAfter != null
                    ? cache.ExpireAfter(expireAfter, scheduler ?? Scheduler.Default).Subscribe()
                    : Disposable.Empty;

                var sizeLimiter = limitSizeTo > 0
                    ? cache.LimitSizeTo(limitSizeTo, scheduler).Subscribe()
                    : Disposable.Empty;

                var notifier = cache.Connect().SubscribeSafe(observer);

                return new CompositeDisposable(cache, sourceSubscriber, notifier, expirer, sizeLimiter);
            });
        }


        /// <summary>
        /// Converts the observable to an observable changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache</param>
        /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns>An observable changeset</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector,
                                                                                                  Func<TObject, TimeSpan?> expireAfter = null,
                                                                                                  int limitSizeTo = -1,
                                                                                                  IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var cache = new SourceCache<TObject, TKey>(keySelector);
                var sourceSubscriber = source.Subscribe(cache.AddOrUpdate);

                var expirer = expireAfter != null
                    ? cache.ExpireAfter(expireAfter, scheduler ?? Scheduler.Default).Subscribe((kvp) => { }, observer.OnError)
                    : Disposable.Empty;

                var sizeLimiter = limitSizeTo > 0
                    ? cache.LimitSizeTo(limitSizeTo, scheduler).Subscribe((kvp) => { }, observer.OnError)
                    : Disposable.Empty;

                var notifier = cache.Connect().SubscribeSafe(observer);

                return new CompositeDisposable(cache, sourceSubscriber, notifier, expirer, sizeLimiter);
            });
        }

        #endregion

        #region Populate into an observable cache

        /// <summary>
        /// Populates a source into the specified cache.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="detination">The detination.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// detination
        /// </exception>
        public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ISourceCache<TObject, TKey> detination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (detination == null) throw new ArgumentNullException(nameof(detination));

            return source.Subscribe(changes => detination.Edit(updater => updater.Update(changes)));
        }

        /// <summary>
        /// Populates a source into the specified cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="detination">The detination.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// detination</exception>
        public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IIntermediateCache<TObject, TKey> detination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (detination == null) throw new ArgumentNullException(nameof(detination));

            return source.Subscribe(changes => detination.Edit(updater => updater.Update(changes)));
        }

        /// <summary>
        /// Populate a cache from an obserable stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observable">The observable.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// keySelector
        /// </exception>
        public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<IEnumerable<TObject>> observable)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return observable.Subscribe(source.AddOrUpdate);
        }

        /// <summary>
        /// Populate a cache from an obserable stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observable">The observable.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// keySelector
        /// </exception>
        public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<TObject> observable)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return observable.Subscribe(source.AddOrUpdate);
        }

        #endregion

        #region AsObservableCache /Connect

        /// <summary>
        /// Converts the source to an read only observable cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new AnomynousObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Converts the source to an read only observable cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new AnomynousObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Creates a stream using the specified controlled filter.
        /// The controlled filter enables dynamic inline changing of the filter.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filterController">The controlled filter.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">filterController</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Connect<TObject, TKey>(this IObservableCache<TObject, TKey> source, FilterController<TObject> filterController)
        {
            if (filterController == null) throw new ArgumentNullException(nameof(filterController));
            return source.Connect().Filter(filterController);
        }

        #endregion

        #region Size / time limiters

        /// <summary>
        /// Limits the number of records in the cache to the size specified.  When the size is reached
        /// the oldest items are removed from the cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sizeLimit">The size limit.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        /// <exception cref="System.ArgumentException">Size limit must be greater than zero</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> LimitSizeTo<TObject, TKey>(this ISourceCache<TObject, TKey> source,
                                                                                                       int sizeLimit, IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sizeLimit <= 0) throw new ArgumentException("Size limit must be greater than zero");

            return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(observer =>
            {
                long orderItemWasAdded = -1;
                var sizeLimiter = new SizeLimiter<TObject, TKey>(sizeLimit);

                return source.Connect()
                             .FinallySafe(observer.OnCompleted)
                             .ObserveOn(scheduler ?? Scheduler.Default)
                             .Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now, Interlocked.Increment(ref orderItemWasAdded)))
                             .Subscribe(changes =>
                             {
                                 var result = sizeLimiter.CloneAndReturnExpiredOnly(changes);
                                 if (result.Count == 0) return;
                                 source.Edit(updater => result.ForEach(c => updater.Remove(c.Key)));
                             });
            });
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="scheduler">The scheduler to perform the work on.</param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ExpireAfter<TObject, TKey>(this ISourceCache<TObject, TKey> source,
                                                                                                       Func<TObject, TimeSpan?> timeSelector, IScheduler scheduler = null)
        {
            return source.ExpireAfter(timeSelector, null, scheduler);
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="interval">A polling interval.  Since multiple timer subscriptions can be expensive,
        /// it may be worth setting the interval .
        /// </param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ExpireAfter<TObject, TKey>(this ISourceCache<TObject, TKey> source,
                                                                                                       Func<TObject, TimeSpan?> timeSelector, TimeSpan? interval = null)
        {
            return ExpireAfter(source, timeSelector, interval, Scheduler.Default);
        }

        /// <summary>
        /// Automatically removes items from the cache after the time specified by
        /// the time selector elapses. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="pollingInterval">A polling interval.  Since multiple timer subscriptions can be expensive,
        /// it may be worth setting the interval.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ExpireAfter<TObject, TKey>(this ISourceCache<TObject, TKey> source,
                                                                                                       Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));

            return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(observer =>
            {
                scheduler = scheduler ?? Scheduler.Default;
                return source.Connect()
                             .ForExpiry(timeSelector, pollingInterval, scheduler)
                             .FinallySafe(observer.OnCompleted)
                             .Subscribe(toRemove =>
                             {
                                 try
                                 {
                                     //remove from cache and notify which items have been auto removed
                                     var keyValuePairs = toRemove as KeyValuePair<TKey, TObject>[] ?? toRemove.ToArray();
                                     if (keyValuePairs.Length == 0) return;
                                     source.Remove(keyValuePairs.Select(kv => kv.Key));
                                     observer.OnNext(keyValuePairs);
                                 }
                                 catch (Exception ex)
                                 {
                                     observer.OnError(ex);
                                 }
                             });
            });
        }

        #endregion

        #region Convenience update methods

        /// <summary>
        /// Adds or updates the cache with the specified item.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.AddOrUpdate(item));
        }

        /// <summary>
        /// <summary>
        /// Adds or updates the cache with the specified items.
        /// </summary>
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void AddOrUpdate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.AddOrUpdate(items));
        }

        /// <summary>
        /// Removes the specified item from the cache. 
        /// 
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Remove(item));
        }

        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Remove(key));
        }

        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void RemoveKey<TObject, TKey>(this ISourceCache<TObject, TKey> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.RemoveKey(key));
        }

        /// <summary>
        /// Removes the specified items from the cache. 
        /// 
        /// Any items not contained in the cache are ignored
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Remove(items));
        }

        /// <summary>
        /// Removes the specified keys from the cache. 
        /// 
        /// Any keys not contained in the cache are ignored
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Remove(keys));
        }

        /// <summary>
        /// Removes the specified keys from the cache. 
        /// 
        /// Any keys not contained in the cache are ignored
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void RemoveKeys<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TKey> keys)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.RemoveKeys(keys));
        }

        /// <summary>
        /// Clears all data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Clear<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Clear());
        }

        /// <summary>
        /// Signal observers to re-evaluate the specified item.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Evaluate<TObject, TKey>(this ISourceCache<TObject, TKey> source, TObject item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Evaluate(item));
        }

        /// <summary>
        /// Signal observers to re-evaluate the specified items.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Evaluate<TObject, TKey>(this ISourceCache<TObject, TKey> source, IEnumerable<TObject> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Evaluate(items));
        }

        /// <summary>
        /// Removes the specified key from the cache.
        /// If the item is not contained in the cache then the operation does nothing.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Remove(key));
        }

        /// <summary>
        /// Removes the specified keys from the cache. 
        /// 
        /// Any keys not contained in the cache are ignored
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keys">The keys.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Remove<TObject, TKey>(this IIntermediateCache<TObject, TKey> source, IEnumerable<TKey> keys)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Remove(keys));
        }

        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Clear<TObject, TKey>(this IIntermediateCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Clear());
        }

        #endregion
    }
}
