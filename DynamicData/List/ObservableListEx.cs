using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DynamicData.Annotations;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace

namespace DynamicData
{
    /// <summary>
    /// Extensions for ObservableList
    /// </summary>
    public static class ObservableListEx
    {
        #region Populate change set from standard rx observable

        /// <summary>
        /// Converts the observable to an observable changeset.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<T> source,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return ToObservableChangeSet(source, null, -1, scheduler);
        }

        /// <summary>
        /// Converts the observable to an observable changeset, allowing time expiry to be specified
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<T> source,
            Func<T, TimeSpan?> expireAfter,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (expireAfter == null) throw new ArgumentNullException(nameof(expireAfter));

            return ToObservableChangeSet(source, expireAfter, -1, scheduler);
        }

        /// <summary>
        /// Converts the observable to an observable changeset, with a specified limit of how large the list can be.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<T> source,
            int limitSizeTo,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return ToObservableChangeSet(source, null, limitSizeTo, scheduler);
        }

        /// <summary>
        /// Converts the observable to an observable changeset, allowing size and time limit to be specified
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache</param>
        /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<T> source,
            Func<T, TimeSpan?> expireAfter,
            int limitSizeTo,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new ToObservableChangeSet<T>(source, expireAfter, limitSizeTo, scheduler).Run();
        }

        /// <summary>
        /// Converts the observable to an observable changeset.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<IEnumerable<T>> source,
            IScheduler scheduler = null)
        {
            return ToObservableChangeSet<T>(source, null, -1, scheduler);
        }

        /// <summary>
        /// Converts the observable to an observable changeset, allowing size and time limit to be specified
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<IEnumerable<T>> source,
            int limitSizeTo,
            IScheduler scheduler = null)
        {
            return ToObservableChangeSet<T>(source, null, limitSizeTo, scheduler);
        }

        /// <summary>
        /// Converts the observable to an observable changeset, allowing size to be specified
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<IEnumerable<T>> source,
            Func<T, TimeSpan?> expireAfter,
            IScheduler scheduler = null)
        {
            return ToObservableChangeSet<T>(source, expireAfter, 0, scheduler);
        }

        /// <summary>
        /// Converts the observable to an observable changeset, allowing size and time limit to be specified
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="expireAfter">Specify on a per object level the maximum time before an object expires from a cache</param>
        /// <param name="limitSizeTo">Remove the oldest items when the size has reached this limit</param>
        /// <param name="scheduler">The scheduler (only used for time expiry).</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this IObservable<IEnumerable<T>> source,
            Func<T, TimeSpan?> expireAfter,
            int limitSizeTo,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new ToObservableChangeSet<T>(source, expireAfter, limitSizeTo, scheduler).Run();
        }

        #endregion

        #region Auto Refresh

        /// <summary>
        /// Automatically refresh downstream operators when any property changes.
        /// </summary>
        /// <param name="source">The source observable</param>
        /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements have sucessive property changes</param>
        /// <param name="propertyChangeThrottle">When observing on multiple property changes, apply a throttle to prevent excessive refesh invocations</param>
        /// <param name="scheduler">The scheduler</param>
        /// <returns>An observable change set with additional refresh changes</returns>
        public static IObservable<IChangeSet<TObject>> AutoRefresh<TObject>(this IObservable<IChangeSet<TObject>> source,
            TimeSpan? changeSetBuffer = null,
            TimeSpan? propertyChangeThrottle = null,
            IScheduler scheduler = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.AutoRefreshOnObservable(t =>
            {
                if (propertyChangeThrottle == null)
                    return t.WhenAnyPropertyChanged();

                return t.WhenAnyPropertyChanged()
                    .Throttle(propertyChangeThrottle.Value, scheduler ?? Scheduler.Default);

            }, changeSetBuffer, scheduler);
        }

        /// <summary>
        /// Automatically refresh downstream operators when properties change.
        /// </summary>
        /// <param name="source">The source observable</param>
        /// <param name="propertyAccessor">Specify a property to observe changes. When it changes a Refresh is invoked</param>
        /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements have sucessive property changes</param>
        /// <param name="propertyChangeThrottle">When observing on multiple property changes, apply a throttle to prevent excessive refesh invocations</param>
        /// <param name="scheduler">The scheduler</param>
        /// <returns>An observable change set with additional refresh changes</returns>
        public static IObservable<IChangeSet<TObject>> AutoRefresh<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source,
            Expression<Func<TObject, TProperty>> propertyAccessor,
            TimeSpan? changeSetBuffer = null,
            TimeSpan? propertyChangeThrottle = null,
            IScheduler scheduler = null)
             where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            return source.AutoRefreshOnObservable(t =>
            {
                if (propertyChangeThrottle == null)
                    return t.WhenPropertyChanged(propertyAccessor, false);

                return t.WhenPropertyChanged(propertyAccessor,false)
                    .Throttle(propertyChangeThrottle.Value, scheduler ?? Scheduler.Default);

            }, changeSetBuffer, scheduler);
        }

        /// <summary>
        /// Automatically refresh downstream operator. The refresh is triggered when the observable receives a notification
        /// </summary>
        /// <param name="source">The source observable change set</param>
        /// <param name="reevaluator">An observable which acts on items within the collection and produces a value when the item should be refreshed</param>
        /// <param name="changeSetBuffer">Batch up changes by specifying the buffer. This greatly increases performance when many elements require a refresh</param>
        /// <param name="scheduler">The scheduler</param>
        /// <returns>An observable change set with additional refresh changes</returns>
        public static IObservable<IChangeSet<TObject>> AutoRefreshOnObservable<TObject, TAny>(this IObservable<IChangeSet<TObject>> source,
            Func<TObject, IObservable<TAny>> reevaluator,
            TimeSpan? changeSetBuffer = null,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (reevaluator == null) throw new ArgumentNullException(nameof(reevaluator));
            return new AutoRefresh<TObject, TAny>(source, reevaluator, changeSetBuffer, scheduler).Run();
        }


        /// <summary>
        /// Supress  refresh notifications
        /// </summary>
        /// <param name="source">The source observable change set</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> SupressRefresh<T>(this IObservable<IChangeSet<T>> source)
        {
            return source.WhereReasonsAreNot(ListChangeReason.Refresh);
        }


        #endregion

        #region Conversion

        /// <summary>
        /// Removes the index from all changes.
        /// 
        /// NB: This operator has been introduced as a temporary fix for creating an Or operator using merge many.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<T>> RemoveIndex<T>([NotNull] this IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Select(changes => new ChangeSet<T>(changes.YieldWithoutIndex()));
        }

        /// <summary>
        /// Adds a key to the change set result which enables all observable cache features of dynamic data
        /// </summary>
        /// <remarks>
        /// All indexed changes are dropped i.e. sorting is not supported by this function
        /// </remarks>
        /// <typeparam name="TObject">The type of  object.</typeparam>
        /// <typeparam name="TKey">The type of  key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> AddKey<TObject, TKey>(
            [NotNull] this IObservable<IChangeSet<TObject>> source, [NotNull] Func<TObject, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            return source.Select(changes => new ChangeSet<TObject, TKey>(new AddKeyEnumerator<TObject, TKey>(changes, keySelector)));
        }

        /// <summary>
        /// Convert the object using the sepcified conversion function.
        /// 
        /// This is a lighter equivalent of Transform and is designed to be used with non-disposable objects
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="conversionFactory">The conversion factory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        [Obsolete("Prefer Cast as it is does the same thing but is semantically correct")]
        public static IObservable<IChangeSet<TDestination>> Convert<TObject, TDestination>(
            [NotNull] this IObservable<IChangeSet<TObject>> source,
            [NotNull] Func<TObject, TDestination> conversionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (conversionFactory == null) throw new ArgumentNullException(nameof(conversionFactory));
            return source.Select(changes => changes.Transform(conversionFactory));
        }



        /// <summary>
        /// Cast the underlying type of an object. Use before a Cast function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<object>> CastToObject<T>(this IObservable<IChangeSet<T>> source)
        {
            return source.Select(changes =>
            {
                var items = changes.Transform(t => (object)t);
                return new ChangeSet<object>(items);
            });
        }

        /// <summary>
        /// Cast the changes to another form
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Cast<TDestination>([NotNull] this IObservable<IChangeSet<object>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Select(changes => changes.Transform(t=>(TDestination)t));
        }

        /// <summary>
        /// Cast the changes to another form
        /// 
        /// Alas, I had to add the converter due to type inference issues. The converter can be avoided by CastToObject() first
        /// </summary>
        /// <typeparam name="TSource">The type of the object.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="conversionFactory">The conversion factory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Cast<TSource, TDestination>([NotNull] this IObservable<IChangeSet<TSource>> source,
            [NotNull] Func<TSource, TDestination> conversionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (conversionFactory == null) throw new ArgumentNullException(nameof(conversionFactory));
            return source.Select(changes => changes.Transform(conversionFactory));
        }

        #endregion

        #region Binding

        /// <summary>
        /// Binds a clone of the observable changeset to the target observable collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="targetCollection">The target collection.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// targetCollection
        /// </exception>
        public static IObservable<IChangeSet<T>> Bind<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] IObservableCollection<T> targetCollection, int resetThreshold = 25)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (targetCollection == null) throw new ArgumentNullException(nameof(targetCollection));

            var adaptor = new ObservableCollectionAdaptor<T>(targetCollection, resetThreshold);
            return source.Adapt(adaptor);
        }

        /// <summary>
        /// Creates a binding to a readonly observable collection which is specified as an 'out' parameter
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns>A continuation of the source stream</returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<T>> Bind<T>([NotNull] this IObservable<IChangeSet<T>> source,
            out ReadOnlyObservableCollection<T> readOnlyObservableCollection, int resetThreshold = 25)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var target = new ObservableCollectionExtended<T>();
            var result = new ReadOnlyObservableCollection<T>(target);
            var adaptor = new ObservableCollectionAdaptor<T>(target, resetThreshold);
            readOnlyObservableCollection = result;
            return source.Adapt(adaptor);
        }

#if SUPPORTS_BINDINGLIST

        /// <summary>
        /// Binds a clone of the observable changeset to the target observable collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="bindingList">The target binding list</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// targetCollection
        /// </exception>
        public static IObservable<IChangeSet<T>> Bind<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] BindingList<T> bindingList, int resetThreshold = 25)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (bindingList == null) throw new ArgumentNullException(nameof(bindingList));

            return source.Adapt(new BindingListAdaptor<T>(bindingList, resetThreshold));
        }

#endif


        /// <summary>
        /// Injects a side effect into a changeset observable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="adaptor">The adaptor.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// adaptor
        /// </exception>
        public static IObservable<IChangeSet<T>> Adapt<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] IChangeSetAdaptor<T> adaptor)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (adaptor == null) throw new ArgumentNullException(nameof(adaptor));


            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var locker = new object();
                return source
                    .Synchronize(locker)
                    .Select(changes =>
                    {
                        adaptor.Adapt(changes);
                        return changes;
                    }).SubscribeSafe(observer);
            });
        }

        #endregion

        #region Populate into an observable list

        /// <summary>
        /// list.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        /// source
        /// or
        /// destination
        /// </exception>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// destination</exception>
        public static IDisposable PopulateInto<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] ISourceList<T> destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
        }

        /// <summary>
        /// Converts the source list to an read only observable list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservableList<T> AsObservableList<T>([NotNull] this ISourceList<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new AnonymousObservableList<T>(source);
        }

        /// <summary>
        /// Converts the source observable to an read only observable list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservableList<T> AsObservableList<T>([NotNull] this IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new AnonymousObservableList<T>(source);
        }

        /// <summary>
        /// List equivalent to Publish().RefCount().  The source is cached so long as there is at least 1 subscriber.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<T>> RefCount<T>([NotNull] this IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new RefCount<T>(source).Run();
        }

        #endregion

        #region Core List Operators

        /// <summary>
        /// Filters the source using the specified valueSelector
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicate">The valueSelector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> Filter<T>(this IObservable<IChangeSet<T>> source, Func<T, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return new Filter<T>(source, predicate).Run();
        }

        /// <summary>
        /// Filters source using the specified filter observable predicate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicate"></param>
        /// <param name="filterPolicy">Should the filter clear and replace, or calculate a diff-set</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// filterController</exception>
        public static IObservable<IChangeSet<T>> Filter<T>([NotNull] this IObservable<IChangeSet<T>> source, [NotNull] IObservable<Func<T, bool>> predicate, ListFilterPolicy filterPolicy = ListFilterPolicy.CalculateDiff)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return new Filter<T>(source, predicate, filterPolicy).Run();
        }


        /// <summary>
        /// Filters source on the specified property using the specified predicate.
        /// 
        /// The filter will automatically reapply when a property changes 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector. When the property changes the filter specified will be re-evaluated</param>
        /// <param name="predicate">A predicate based on the object which contains the changed property</param>
        /// <param name="propertyChangedThrottle">The property changed throttle.</param>
        /// <param name="scheduler">The scheduler used when throttling</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        [Obsolete("Use AutoRefresh(), followed by Filter() instead")]
        public static IObservable<IChangeSet<TObject>> FilterOnProperty<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source,
            Expression<Func<TObject, TProperty>> propertySelector,
            Func<TObject, bool> predicate,
            TimeSpan? propertyChangedThrottle = null,
            IScheduler scheduler = null) where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return new FilterOnProperty<TObject, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
        }

        /// <summary>
        /// Filters source on the specified observable property using the specified predicate.
        /// 
        /// The filter will automatically reapply when a property changes 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="objectFilterObservable">The filter property selector. When the observable changes the filter will be re-evaluated</param>
        /// <param name="propertyChangedThrottle">The property changed throttle.</param>
        /// <param name="scheduler">The scheduler used when throttling</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject>> FilterOnObservable<TObject>(this IObservable<IChangeSet<TObject>> source,
            Func<TObject, IObservable<bool>> objectFilterObservable,
            TimeSpan? propertyChangedThrottle = null,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new FilterOnObservable<TObject>(source, objectFilterObservable, propertyChangedThrottle, scheduler).Run();
        }

        /// <summary>
        /// Reverse sort of the changset
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// comparer
        /// </exception>
        public static IObservable<IChangeSet<T>> Reverse<T>(this IObservable<IChangeSet<T>> source)
        {
            var reverser = new Reverser<T>();
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Select(changes => new ChangeSet<T>(reverser.Reverse(changes)));
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// valueSelector
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, 
            Func<TSource, TDestination> transformFactory,
            bool transformOnRefresh = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            return source.Transform<TSource, TDestination>((t, previous, idx) => transformFactory(t), transformOnRefresh);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform fuunction</param>
        /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received</param>
        /// <returns>A an observable changeset of the transformed object</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// valueSelector
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, 
            Func<TSource, int, TDestination> transformFactory,
            bool transformOnRefresh = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return source.Transform<TSource, TDestination>((t, previous, idx) => transformFactory(t,idx),transformOnRefresh);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function.
        /// 
        /// *** Annoyingly when using this overload you will have to explicitly specify the generic type arguments as type inference fails
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform function</param>
        /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received</param>
        /// <returns>A an observable changeset of the transformed object</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// valueSelector
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source,
            Func<TSource, Optional<TDestination>, TDestination> transformFactory,
            bool transformOnRefresh = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return source.Transform<TSource, TDestination>((t, previous, idx) => transformFactory(t, previous), transformOnRefresh);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// 
        /// *** Annoyingly when using this overload you will have to explicy specify the generic type arguments as type inference fails
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="transformOnRefresh">Should a new transform be applied when a refresh event is received</param>
        /// <returns>A an observable changeset of the transformed object</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// valueSelector
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> Transform<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source,
            Func<TSource, Optional<TDestination>, int, TDestination> transformFactory, bool transformOnRefresh = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return new Transformer<TSource, TDestination>(source, transformFactory, transformOnRefresh).Run();
        }


        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <returns>A an observable changeset of the transformed object</returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// valueSelector
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> TransformAsync<TSource, TDestination>(
            this IObservable<IChangeSet<TSource>> source, Func<TSource, Task<TDestination>> transformFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return new TransformAsync<TSource, TDestination>(source, transformFactory).Run();
        }

        /// <summary>
        /// Equivalent to a select many transform. To work, the key must individually identify each child.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// manyselector
        /// </exception>
        public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>( [NotNull] this IObservable<IChangeSet<TSource>> source,
            [NotNull] Func<TSource, IEnumerable<TDestination>> manyselector,
            IEqualityComparer<TDestination> equalityComparer = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (manyselector == null) throw new ArgumentNullException(nameof(manyselector));
            return new TransformMany<TSource, TDestination>(source, manyselector, equalityComparer).Run();
        }

         /// <summary>
        /// Flatten the nested observable collection, and  observe subsequentl observable collection changes
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children</param>
        public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>( this IObservable<IChangeSet<TSource>> source,
            Func<TSource, ObservableCollection<TDestination>> manyselector,
            IEqualityComparer<TDestination> equalityComparer = null)
        {
            return new TransformMany<TSource, TDestination>(source,manyselector, equalityComparer).Run();
        }

        /// <summary>
        /// Flatten the nested observable collection, and  observe subsequentl observable collection changes
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children</param>
        public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source,
            Func<TSource, ReadOnlyObservableCollection<TDestination>> manyselector,
            IEqualityComparer<TDestination> equalityComparer = null)
        {
            return new TransformMany<TSource, TDestination>(source, manyselector, equalityComparer).Run();
        }

        /// <summary>
        /// Flatten the nested observable list, and observe subsequent observable collection changes
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children</param>
        public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source,
            Func<TSource, IObservableList<TDestination>> manyselector,
            IEqualityComparer<TDestination> equalityComparer = null)
        {
            return new TransformMany<TSource, TDestination>(source, manyselector, equalityComparer).Run();
        }

        /// <summary>
        /// Selects distinct values from the source, using the specified value selector
        /// </summary>
        /// <typeparam name="TObject">The type of the source.</typeparam>
        /// <typeparam name="TValue">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="valueSelector">The transform factory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// valueSelector
        /// </exception>
        public static IObservable<IChangeSet<TValue>> DistinctValues<TObject, TValue>(
            this IObservable<IChangeSet<TObject>> source,
            Func<TObject, TValue> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            return new Distinct<TObject, TValue>(source, valueSelector).Run();
        }

        /// <summary>
        ///  Groups the source on the value returned by group selector factory.  The groupings contains an inner observable list.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TGroup">The type of the group.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelector">The group selector.</param>
        /// <param name="regrouper">Force the grouping function to recalculate the group value.
        /// For example if you have a time based grouping with values like `Last Minute', 'Last Hour', 'Today' etc regrouper is used to refresh these groupings</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// groupSelector
        /// </exception>
        public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOn<TObject, TGroup>(
            this IObservable<IChangeSet<TObject>> source, Func<TObject, TGroup> groupSelector,
            IObservable<Unit> regrouper = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelector == null) throw new ArgumentNullException(nameof(groupSelector));
            return new GroupOn<TObject, TGroup>(source, groupSelector, regrouper).Run();
        }

        /// <summary>
        ///  Groups the source on the value returned by group selector factory. Each update produces immuatable grouping.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <param name="regrouper">Force the grouping function to recalculate the group value.
        /// For example if you have a time based grouping with values like `Last Minute', 'Last Hour', 'Today' etc regrouper is used to refresh these groupings</param>
        /// <returns></returns>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// groupSelectorKey
        /// </exception>
        public static IObservable<IChangeSet<List.IGrouping<TObject, TGroupKey>>> GroupWithImmutableState
            <TObject, TGroupKey>(this IObservable<IChangeSet<TObject>> source,
                Func<TObject, TGroupKey> groupSelectorKey,
                IObservable<Unit> regrouper = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelectorKey == null) throw new ArgumentNullException(nameof(groupSelectorKey));

            return new GroupOnImmutable<TObject, TGroupKey>(source, groupSelectorKey, regrouper).Run();
        }


        /// <summary>
        /// Groups the source using the property specified by the property selector.  The resulting groupings contains an inner observable list.
        /// Groups are re-applied when the property value changed.
        /// When there are likely to be a large number of group property changes specify a throttle to improve performance
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TGroup">The type of the group.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector used to group the items</param>
        /// <param name="propertyChangedThrottle">The property changed throttle.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOnProperty<TObject, TGroup>(
            this IObservable<IChangeSet<TObject>> source,
            Expression<Func<TObject, TGroup>> propertySelector,
            TimeSpan? propertyChangedThrottle = null,
            IScheduler scheduler = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));
            return
                new GroupOnProperty<TObject, TGroup>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
        }

        /// <summary>
        /// Groups the source using the property specified by the property selector.  The resulting groupings are immutable.
        /// Groups are re-applied when the property value changed.
        /// When there are likely to be a large number of group property changes specify a throttle to improve performance
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TGroup">The type of the group.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector used to group the items</param>
        /// <param name="propertyChangedThrottle">The property changed throttle.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<List.IGrouping<TObject, TGroup>>> GroupOnPropertyWithImmutableState<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source,
                Expression<Func<TObject, TGroup>> propertySelector,
                TimeSpan? propertyChangedThrottle = null,
                IScheduler scheduler = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));
            return     new GroupOnPropertyWithImmutableState<TObject, TGroup>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
        }

        /// <summary>
        /// Prevents an empty notification
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> NotEmpty<T>(this IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Where(s => s.Count != 0);
        }

        /// <summary>
        /// Clones the target list as a side effect of the stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="target"></param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> Clone<T>(this IObservable<IChangeSet<T>> source, IList<T> target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Do(target.Clone);
        }

        #endregion

        #region Sort

        /// <summary>
        /// Sorts the sequence using the specified comparer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer used for sorting</param>
        /// <param name="options">For improved performance, specify SortOptions.UseBinarySearch. This can only be used when the values which are sorted on are immutable</param>
        /// <param name="resetThreshold">Since sorting can be slow for large record sets, the reset threshold is used to force the list re-ordered </param>
        /// <param name="resort">OnNext of this observable causes data to resort. This is required when the value which is sorted on mutable</param>
        /// <param name="comparerChanged">An observable comparer used to change the comparer on which the sorted list i</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// comparer</exception>
        public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source,
            IComparer<T> comparer,
            SortOptions options = SortOptions.None,
            IObservable<Unit> resort = null,
            IObservable<IComparer<T>> comparerChanged = null,
            int resetThreshold = 50)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));

            return new Sort<T>(source, comparer, options, resort, comparerChanged, resetThreshold).Run();
        }

        /// <summary>
        /// Sorts the sequence using the specified observable comparer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="options">For improved performance, specify SortOptions.UseBinarySearch. This can only be used when the values which are sorted on are immutable</param>
        /// <param name="resetThreshold">Since sorting can be slow for large record sets, the reset threshold is used to force the list re-ordered </param>
        /// <param name="resort">OnNext of this observable causes data to resort. This is required when the value which is sorted on mutable</param>
        /// <param name="comparerChanged">An observable comparer used to change the comparer on which the sorted list i</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// comparer</exception>
        public static IObservable<IChangeSet<T>> Sort<T>(this IObservable<IChangeSet<T>> source,
            IObservable<IComparer<T>> comparerChanged,
            SortOptions options = SortOptions.None,
            IObservable<Unit> resort = null,
            int resetThreshold = 50)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparerChanged == null) throw new ArgumentNullException(nameof(comparerChanged));

            return new Sort<T>(source, null, options, resort, comparerChanged, resetThreshold).Run();
        }

        #endregion

        #region Item operators

        /// <summary>
        /// Provides a call back for each item change.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject>> ForEachChange<TObject>(
            [NotNull] this IObservable<IChangeSet<TObject>> source,
            [NotNull] Action<Change<TObject>> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            return source.Do(changes => changes.ForEach(action));
        }

        /// <summary>
        /// Provides a call back for each item change.
        /// 
        /// Range changes are flattened, so there is only need to check for Add, Replace, Remove and Clear
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject>> ForEachItemChange<TObject>(
            [NotNull] this IObservable<IChangeSet<TObject>> source,
            [NotNull] Action<ItemChange<TObject>> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            return source.Do(changes => changes.Flatten().ForEach(action));
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and unmerges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector</exception>
        public static IObservable<TDestination> MergeMany<T, TDestination>(
            [NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] Func<T, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return new MergeMany<T, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<TValue> WhenValueChanged<TObject, TValue>(
            [NotNull] this IObservable<IChangeSet<TObject>> source,
            [NotNull] Expression<Func<TObject, TValue>> propertyAccessor,
            bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            var factory = propertyAccessor.GetFactory();
            return source.MergeMany(t => factory(t, notifyOnInitialValue).Select(pv=>pv.Value));
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed
        /// </summary>
        /// <typeparam name="TObject"></typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TValue>(
            [NotNull] this IObservable<IChangeSet<TObject>> source,
            [NotNull] Expression<Func<TObject, TValue>> propertyAccessor,
            bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            var factory = propertyAccessor.GetFactory();
            return source.MergeMany(t => factory(t, notifyOnInitialValue));
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertiesToMonitor">specify properties to Monitor, or omit to monitor all property changes</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<TObject> WhenAnyPropertyChanged<TObject>([NotNull] this IObservable<IChangeSet<TObject>> source, params string[] propertiesToMonitor)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed
        /// </remarks>
        public static IObservable<IChangeSet<T>> SubscribeMany<T>(this IObservable<IChangeSet<T>> source,
            Func<T, IDisposable> subscriptionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));
            return new SubscribeMany<T>(source, subscriptionFactory).Run();
        }

        /// <summary>
        /// Disposes each item when no longer required.
        /// 
        /// Individual items are disposed when removed or replaced. All items
        /// are disposed when the stream is disposed
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>A continuation of the original stream</returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> DisposeMany<T>(this IObservable<IChangeSet<T>> source)
        {
            return source.OnItemRemoved(t =>
            {
                var d = t as IDisposable;
                d?.Dispose();
            });
        }

        /// <summary>
        /// Callback for each item as and when it is being removed from the stream
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="removeAction">The remove action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// removeAction
        /// </exception>
        public static IObservable<IChangeSet<T>> OnItemRemoved<T>(this IObservable<IChangeSet<T>> source,
            Action<T> removeAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (removeAction == null) throw new ArgumentNullException(nameof(removeAction));

            return new OnBeingRemoved<T>(source, removeAction).Run();
        }

        /// <summary>
        /// Callback for each item as and when it is being added to the stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="addAction">The add action.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> OnItemAdded<T>(this IObservable<IChangeSet<T>> source,
            Action<T> addAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (addAction == null) throw new ArgumentNullException(nameof(addAction));
            return new OnBeingAdded<T>(source, addAction).Run();
        }

        #endregion

        #region Reason filtering

        /// <summary>
        /// Includes changes for the specified reasons only
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reasons">The reasons.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">Must enter at least 1 reason</exception>
        public static IObservable<IChangeSet<T>> WhereReasonsAre<T>(this IObservable<IChangeSet<T>> source,
            params ListChangeReason[] reasons)
        {
            if (reasons.Length == 0)
                throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));

            var matches = new HashSet<ListChangeReason>(reasons); 
            return source.Select(changes =>
            {
                var filtered = changes.Where(change => matches.Contains(change.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
        }

        /// <summary>
        /// Excludes updates for the specified reasons
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reasons">The reasons.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">Must enter at least 1 reason</exception>
        public static IObservable<IChangeSet<T>> WhereReasonsAreNot<T>(this IObservable<IChangeSet<T>> source,
            params ListChangeReason[] reasons)
        {
            if (reasons.Length == 0)
                throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));

            var matches =  new HashSet<ListChangeReason>(reasons);
            return source.Select(updates =>
            {
                var filtered = updates.Where(u => !matches.Contains(u.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
        }

        #endregion

        #region Buffering

        /// <summary>
        /// Buffers changes for an intial period only. After the period has elapsed, not further buffering occurs. 
        /// </summary>
        /// <param name="source">The source changeset</param>
        /// <param name="initalBuffer">The period to buffer, measure from the time that the first item arrives</param>
        /// <param name="scheduler">The scheduler to buffer on</param>
        public static IObservable<IChangeSet<TObject>> BufferInitial<TObject>(this IObservable<IChangeSet<TObject>> source, TimeSpan initalBuffer, IScheduler scheduler = null)
        {
            return source.DeferUntilLoaded().Publish(shared =>
            {
                var initial = shared.Buffer(initalBuffer, scheduler ?? Scheduler.Default)
                    .FlattenBufferResult()
                    .Take(1);

                return initial.Concat(shared);
            });
        }

        /// <summary>
        /// Convert the result of a buffer operation to a change set
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> FlattenBufferResult<T>(this IObservable<IList<IChangeSet<T>>> source)
        {
            return source
                .Where(x => x.Count != 0)
                .Select(updates => new ChangeSet<T>(updates.SelectMany(u => u)));
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> BufferIf<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] IObservable<bool> pauseIfTrueSelector,
            IScheduler scheduler = null)
        {
            return BufferIf(source, pauseIfTrueSelector, false, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> BufferIf<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] IObservable<bool> pauseIfTrueSelector,
            bool intialPauseState = false,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pauseIfTrueSelector == null) throw new ArgumentNullException(nameof(pauseIfTrueSelector));
            return BufferIf(source, pauseIfTrueSelector, intialPauseState, null, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source,
            IObservable<bool> pauseIfTrueSelector,
            TimeSpan? timeOut = null,
            IScheduler scheduler = null)
        {
            return BufferIf(source, pauseIfTrueSelector, false, timeOut, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
        /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source,
            IObservable<bool> pauseIfTrueSelector,
            bool intialPauseState = false,
            TimeSpan? timeOut = null,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pauseIfTrueSelector == null) throw new ArgumentNullException(nameof(pauseIfTrueSelector));
            return new BufferIf<T>(source, pauseIfTrueSelector, intialPauseState, timeOut, scheduler).Run();
        }

        /// <summary>
        ///  The latest copy of the cache is exposed for querying after each modification to the underlying data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="resultSelector">The result selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// resultSelector
        /// </exception>
        public static IObservable<TDestination> QueryWhenChanged<TObject, TDestination>(
            this IObservable<IChangeSet<TObject>> source,
            Func<IReadOnlyCollection<TObject>, TDestination> resultSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return source.QueryWhenChanged().Select(resultSelector);
        }

        /// <summary>
        /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IReadOnlyCollection<T>> QueryWhenChanged<T>([NotNull] this IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new QueryWhenChanged<T>(source).Run();
        }

        /// <summary>
        /// Converts the changeset into a fully formed collection. Each change in the source results in a new collection
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject>(this IObservable<IChangeSet<TObject>> source)
        {
            return source.QueryWhenChanged(items => items);
        }

        /// <summary>
        /// Converts the changeset into a fully formed sorted collection. Each change in the source results in a new sorted collection
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TSortKey">The sort key</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sort">The sort function</param>
        /// <param name="sortOrder">The sort order. Defaults to ascending</param>
        /// <returns></returns>
        public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject, TSortKey>(this IObservable<IChangeSet<TObject>> source,
            Func<TObject, TSortKey> sort, SortDirection sortOrder = SortDirection.Ascending)
        {
            return source.QueryWhenChanged(query => sortOrder == SortDirection.Ascending
                ? new ReadOnlyCollectionLight<TObject>(query.OrderBy(sort))
                : new ReadOnlyCollectionLight<TObject>(query.OrderByDescending(sort)));
        }

        /// <summary>
        /// Converts the changeset into a fully formed sorted collection. Each change in the source results in a new sorted collection
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The sort comparer</param>
        /// <returns></returns>
        public static IObservable<IReadOnlyCollection<TObject>> ToSortedCollection<TObject>(this IObservable<IChangeSet<TObject>> source,
            IComparer<TObject> comparer)
        {
            return source.QueryWhenChanged(query =>
            {
                var items = query.AsList();
                items.Sort(comparer);
                return new ReadOnlyCollectionLight<TObject>(items);
            });
        }


        /// <summary>
        /// Defer the subscribtion until loaded and skip initial changeset
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> SkipInitial<T>(this IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.DeferUntilLoaded().Skip(1);
        }

        /// <summary>
        /// Defer the subscription until the stream has been inflated with data
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>([NotNull] this IObservable<IChangeSet<T>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new DeferUntilLoaded<T>(source).Run();
        }

        /// <summary>
        /// Defer the subscription until the cache has been inflated with data
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservableList<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Connect().DeferUntilLoaded();
        }

        #endregion

        #region Virtualisation / Paging

        /// <summary>
        /// Virtualises the source using parameters provided via the requests observable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="requests">The requests.</param>
        /// <returns></returns>
        public static IObservable<IVirtualChangeSet<T>> Virtualise<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] IObservable<IVirtualRequest> requests)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            return new Virtualiser<T>(source, requests).Run();
        }

        /// <summary>
        /// Limits the size of the result set to the specified number of items
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="numberOfItems">The number of items.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Top<T>([NotNull] this IObservable<IChangeSet<T>> source,
            int numberOfItems)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (numberOfItems <= 0)
                throw new ArgumentOutOfRangeException(nameof(numberOfItems),
                    "Number of items should be greater than zero");

            return source.Virtualise(Observable.Return(new VirtualRequest(0, numberOfItems)));
        }


        /// <summary>
        /// Applies paging to the the data source
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="requests">Observable to control page requests</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Page<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] IObservable<IPageRequest> requests)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            return new Pager<T>(source, requests).Run();
        }

        #endregion

        #region Expiry / size limiter

        /// <summary>
        /// Limits the size of the source cache to the specified limit. 
        /// Notifies which items have been removed from the source list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sizeLimit">The size limit.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException">sizeLimit cannot be zero</exception>
        /// <exception cref="ArgumentNullException">source</exception>
        /// <exception cref="ArgumentException">sizeLimit cannot be zero</exception>
        public static IObservable<IEnumerable<T>> LimitSizeTo<T>([NotNull] this ISourceList<T> source, int sizeLimit,
            IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sizeLimit <= 0) throw new ArgumentException("sizeLimit cannot be zero", nameof(sizeLimit));

            var locker = new object();
            var limiter = new LimitSizeTo<T>(source, sizeLimit, scheduler ?? Scheduler.Default, locker);

            return limiter.Run().Synchronize(locker).Do(source.RemoveMany);
        }

        /// <summary>
        /// Removes items from the cache according to the value specified by the time selector function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">Selector returning when to expire the item. Return null for non-expiring item</param>
        /// <param name="scheduler">The scheduler</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IEnumerable<T>> ExpireAfter<T>([NotNull] this ISourceList<T> source,
            [NotNull] Func<T, TimeSpan?> timeSelector, IScheduler scheduler = null)
        {
            return source.ExpireAfter(timeSelector, null, scheduler);
        }

        /// <summary>
        /// Removes items from the cache according to the value specified by the time selector function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">Selector returning when to expire the item. Return null for non-expiring item</param>
        /// <param name="pollingInterval">Enter the polling interval to optimise expiry timers, if ommited 1 timer is created for each unique expiry time</param>
        /// <param name="scheduler">The scheduler</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IEnumerable<T>> ExpireAfter<T>([NotNull] this ISourceList<T> source,
            [NotNull] Func<T, TimeSpan?> timeSelector, TimeSpan? pollingInterval = null, IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));

            var locker = new object();
            var limiter = new ExpireAfter<T>(source, timeSelector, pollingInterval, scheduler ?? Scheduler.Default,
                locker);

            return limiter.Run().Synchronize(locker).Do(source.RemoveMany);
        }

        #endregion

        #region Logical collection operators

        /// <summary>
        /// Apply a logical Or operator between the collections.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Or<T>([NotNull] this ICollection<IObservable<IChangeSet<T>>> sources)
        {
            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Apply a logical Or operator between the collections.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Or<T>([NotNull] this IObservable<IChangeSet<T>> source,
            params IObservable<IChangeSet<T>>[] others)
        {
            return source.Combine(CombineOperator.Or, others);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Or<T>(
            [NotNull] this IObservableList<IObservable<IChangeSet<T>>> sources)
        {
            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Or<T>([NotNull] this IObservableList<IObservableList<T>> sources)
        {
            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Or<T>([NotNull] this IObservableList<ISourceList<T>> sources)
        {
            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Apply a logical Xor operator between the collections.
        /// Items which are only in one of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Xor<T>([NotNull] this IObservable<IChangeSet<T>> source,
            params IObservable<IChangeSet<T>>[] others)
        {
            return source.Combine(CombineOperator.Xor, others);
        }

        /// <summary>
        /// Apply a logical Xor operator between the collections.
        /// Items which are only in one of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The sources.</param>
        /// <returns></returns>>
        public static IObservable<IChangeSet<T>> Xor<T>([NotNull] this ICollection<IObservable<IChangeSet<T>>> sources)
        {
            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Xor<T>(
            [NotNull] this IObservableList<IObservable<IChangeSet<T>>> sources)
        {
            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Xor<T>([NotNull] this IObservableList<IObservableList<T>> sources)
        {
            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Xor<T>([NotNull] this IObservableList<ISourceList<T>> sources)
        {
            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Apply a logical And operator between the collections.
        /// Items which are in all of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> And<T>([NotNull] this IObservable<IChangeSet<T>> source,
            params IObservable<IChangeSet<T>>[] others)
        {
            return source.Combine(CombineOperator.And, others);
        }

        /// <summary>
        /// Apply a logical And operator between the collections.
        /// Items which are in all of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The sources.</param>
        /// <returns></returns>>
        public static IObservable<IChangeSet<T>> And<T>([NotNull] this ICollection<IObservable<IChangeSet<T>>> sources)
        {
            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> And<T>(
            [NotNull] this IObservableList<IObservable<IChangeSet<T>>> sources)
        {
            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> And<T>([NotNull] this IObservableList<IObservableList<T>> sources)
        {
            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> And<T>([NotNull] this IObservableList<ISourceList<T>> sources)
        {
            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Apply a logical Except operator between the collections.
        /// Items which are in the source and not in the others are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Except<T>([NotNull] this IObservable<IChangeSet<T>> source,
            params IObservable<IChangeSet<T>>[] others)
        {
            return source.Combine(CombineOperator.Except, others);
        }

        /// <summary>
        /// Apply a logical Except operator between the collections.
        /// Items which are in the source and not in the others are included in the result
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The sources.</param>
        /// <returns></returns>>
        public static IObservable<IChangeSet<T>> Except<T>(
            [NotNull] this ICollection<IObservable<IChangeSet<T>>> sources)
        {
            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator. Items from the first observable list are included when an equivalent item does not exist in the other sources.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Except<T>(
            [NotNull] this IObservableList<IObservable<IChangeSet<T>>> sources)
        {
            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator. Items from the first observable list are included when an equivalent item does not exist in the other sources.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Except<T>([NotNull] this IObservableList<IObservableList<T>> sources)
        {
            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator. Items from the first observable list are included when an equivalent item does not exist in the other sources.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<T>> Except<T>([NotNull] this IObservableList<ISourceList<T>> sources)
        {
            return sources.Combine(CombineOperator.Except);
        }

        private static IObservable<IChangeSet<T>> Combine<T>(
            [NotNull] this ICollection<IObservable<IChangeSet<T>>> sources,
            CombineOperator type)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return new Combiner<T>(sources, type).Run();
        }

        private static IObservable<IChangeSet<T>> Combine<T>([NotNull] this IObservable<IChangeSet<T>> source,
            CombineOperator type,
            params IObservable<IChangeSet<T>>[] others)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (others.Length == 0)
                throw new ArgumentException("Must be at least one item to combine with", nameof(others));

            var items = source.EnumerateOne().Union(others).ToList();
            return new Combiner<T>(items, type).Run();
        }

        private static IObservable<IChangeSet<T>> Combine<T>([NotNull] this IObservableList<ISourceList<T>> sources,
            CombineOperator type)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
        }

        private static IObservable<IChangeSet<T>> Combine<T>([NotNull] this IObservableList<IObservableList<T>> sources,
            CombineOperator type)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
        }

        private static IObservable<IChangeSet<T>> Combine<T>(
            [NotNull] this IObservableList<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            return new DynamicCombiner<T>(sources, type).Run();
        }

        #endregion

        #region Switch

        /// <summary>
        /// Transforms an observable sequence of observable lists into a single sequence
        /// producing values only from the most recent observable sequence.
        /// Each time a new inner observable sequence is received, unsubscribe from the
        /// previous inner observable sequence and clear the existing result set
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>
        /// The observable sequence that at any point in time produces the elements of the most recent inner observable sequence that has been received.
        /// </returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="sources" /> is null.</exception>
        public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservableList<T>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            return sources.Select(cache => cache.Connect()).Switch();
        }

        /// <summary>
        /// Transforms an observable sequence of observable changes sets into an observable sequence
        /// producing values only from the most recent observable sequence.
        /// Each time a new inner observable sequence is received, unsubscribe from the
        /// previous inner observable sequence and clear the existing resukt set
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>
        /// The observable sequence that at any point in time produces the elements of the most recent inner observable sequence that has been received.
        /// </returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="sources" /> is null.</exception>
        public static IObservable<IChangeSet<T>> Switch<T>(this IObservable<IObservable<IChangeSet<T>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            return new Switch<T>(sources).Run();
        }

        #endregion


        #region Start with

        /// <summary>
        /// Prepends an empty changeset to the source
        /// </summary>
        public static IObservable<IChangeSet<T>> StartWithEmpty<T>(this IObservable<IChangeSet<T>> source)
        {
            return source.StartWith(ChangeSet<T>.Empty);
        }



        #endregion
    }
}
