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
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Annotations;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;
using DynamicData.Controllers;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Extensions for dynamic data
    /// </summary>
    [PublicAPI]
    public static class ObservableCacheEx
    {
        #region General

        /// <summary>
        /// Ensure that finally is always called. Thanks to Lee Campbell for this
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="finallyAction">The finally action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<T> FinallySafe<T>(this IObservable<T> source, Action finallyAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (finallyAction == null) throw new ArgumentNullException(nameof(finallyAction));

            return new FinallySafe<T>(source, finallyAction).Run();
        }

        /// <summary>
        /// Cache equivalent to Publish().RefCount().  The source is cached so long as there is at least 1 subscriber.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> RefCount<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new RefCount<TObject, TKey>(source).Run();
        }

        /// <summary>
        /// Monitors the status of a stream
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ConnectionStatus> MonitorStatus<T>(this IObservable<T> source)
        {
            return new StatusMonitor<T>(source).Run();
        }

        /// <summary>
        /// Supresses updates which are empty
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> NotEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Where(changes => changes.Count != 0);
        }

        /// <summary>
        /// Flattens an update collection to it's individual items
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<Change<TObject, TKey>> Flatten<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.SelectMany(changes => changes);
        }

        /// <summary>
        /// Provides a call back for each change
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> ForEachChange<TObject, TKey>(
            [NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] Action<Change<TObject, TKey>> action)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));
            return source.Do(changes => changes.ForEach(action));
        }

        /// <summary>
        /// Ignores the update when the condition is met.
        /// The first parameter in the ignore function is the current value and the second parameter is the previous value
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="ignoreFunction">The ignore function (current,previous)=>{ return true to ignore }.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> IgnoreUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                             Func<TObject, TObject, bool> ignoreFunction)
        {
            return source.Select(updates =>
            {
                var result = updates.Where(u =>
                {
                    if (u.Reason != ChangeReason.Update)
                        return true;

                    return !ignoreFunction(u.Current, u.Previous.Value);
                });
                return new ChangeSet<TObject, TKey>(result);
            }).NotEmpty();
        }

        /// <summary>
        /// Only includes the update when the condition is met.
        /// The first parameter in the ignore function is the current value and the second parameter is the previous value
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="includeFunction">The include function (current,previous)=>{ return true to include }.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> IncludeUpdateWhen<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                              Func<TObject, TObject, bool> includeFunction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (includeFunction == null) throw new ArgumentNullException(nameof(includeFunction));

            return source.Select(changes =>
            {
                var result = changes.Where(change => change.Reason != ChangeReason.Update || includeFunction(change.Current, change.Previous.Value));
                return new ChangeSet<TObject, TKey>(result);
            }).NotEmpty();
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and unmerges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector</exception>
        public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and unmerges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector</exception>
        public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and unmerges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector</exception>
        public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Dynamically merges the observable which is selected from each item in the stream, and unmerges the item
        /// when it is no longer part of the stream.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// observableSelector</exception>
        public static IObservable<TDestination> MergeMany<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));

            return new MergeMany<TObject, TKey, TDestination>(source, observableSelector).Run();
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<TValue> WhenValueChanged<TObject, TKey, TValue>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                  [NotNull] Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            return source.MergeMany(t => t.WhenValueChanged(propertyAccessor, notifyOnInitialValue));
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertyAccessor">The property accessor.</param>
        /// <param name="notifyOnInitialValue">if set to <c>true</c> [notify on initial value].</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<PropertyValue<TObject, TValue>> WhenPropertyChanged<TObject, TKey, TValue>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                             [NotNull] Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertyAccessor == null) throw new ArgumentNullException(nameof(propertyAccessor));

            var member = propertyAccessor.GetProperty();
            var accessor = propertyAccessor.Compile();
            return source.MergeMany(t => t.WhenPropertyChanged(accessor, member.Name, notifyOnInitialValue));
        }

        /// <summary>
        /// Watches each item in the collection and notifies when any of them has changed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="propertiesToMonitor">specify properties to Monitor, or omit to monitor all property changes</param>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<TObject> WhenAnyPropertyChanged<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source, params string[] propertiesToMonitor)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                          Func<TObject, IDisposable> subscriptionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));

            return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                          Func<TObject, TKey, IDisposable> subscriptionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));

            return new SubscribeMany<TObject, TKey>(source, subscriptionFactory).Run();
        }

        /// <summary>
        /// Callback for each item as and when it is being removed from the stream
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="removeAction">The remove action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// removeAction
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> OnItemRemoved<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (removeAction == null) throw new ArgumentNullException(nameof(removeAction));

            return new OnItemRemoved<TObject, TKey>(source, removeAction).Run();
        }

        /// <summary>
        /// Callback when an item has been updated eg. (current, previous)=>{} 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="updateAction">The update action.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> OnItemUpdated<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TObject> updateAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));

            return source.Do(changes => changes.Where(c => c.Reason == ChangeReason.Update)
                .ForEach(c =>
                {
                    updateAction(c.Current, c.Previous.Value);
                }));
        }

        /// <summary>
        /// Disposes each item when no longer required.
        /// 
        /// Individual items are disposed when removed or replaced. All items
        /// are disposed when the stream is disposed
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>A continuation of the original stream</returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> DisposeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.OnItemRemoved(t =>
            {
                var d = t as IDisposable;
                d?.Dispose();
            });
        }

        /// <summary>
        /// Includes changes for the specified reasons only
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reasons">The reasons.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">reasons</exception>
        /// <exception cref="System.ArgumentException">Must select at least on reason</exception>
        public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAre<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
        {
            if (reasons == null) throw new ArgumentNullException(nameof(reasons));
            if (!reasons.Any()) throw new ArgumentException("Must select at least one reason");
            var hashed = new HashSet<ChangeReason>(reasons);

            return source.Select(updates =>
            {
                return new ChangeSet<TObject, TKey>(updates.Where(u => hashed.Contains(u.Reason)));
            }).NotEmpty();
        }

        /// <summary>
        /// Excludes updates for the specified reasons
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reasons">The reasons.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">reasons</exception>
        /// <exception cref="System.ArgumentException">Must select at least on reason</exception>
        public static IObservable<IChangeSet<TObject, TKey>> WhereReasonsAreNot<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params ChangeReason[] reasons)
        {
            if (reasons == null) throw new ArgumentNullException(nameof(reasons));
            if (!reasons.Any()) throw new ArgumentException("Must select at least one reason");

            var hashed = new HashSet<ChangeReason>(reasons);

            return source.Select(updates =>
            {
                return new ChangeSet<TObject, TKey>(updates.Where(u => !hashed.Contains(u.Reason)));
            }).NotEmpty();
        }
        
        #endregion

        #region Start with
        
        /// <summary>
        /// Prepends an empty changeset to the source
        /// </summary>
        public static IObservable<IChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.StartWith(ChangeSet<TObject, TKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty changeset to the source
        /// </summary>
        /// <returns></returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        {
            return source.StartWith(SortedChangeSet<TObject, TKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty changeset to the source
        /// </summary>
        /// <returns></returns>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
        {
            return source.StartWith(VirtualChangeSet<TObject, TKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty changeset to the source
        /// </summary>
        /// <returns></returns>
        public static IObservable<IPagedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
        {
            return source.StartWith(PagedChangeSet<TObject, TKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty changeset to the source
        /// </summary>
        /// <returns></returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> source)
        {
            return source.StartWith(GroupChangeSet<TObject, TKey, TGroupKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty changeset to the source
        /// </summary>
        /// <returns></returns>
        public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> source)
        {
            return source.StartWith(ImmutableGroupChangeSet<TObject, TKey, TGroupKey>.Empty);
        }

        /// <summary>
        /// Prepends an empty changeset to the source
        /// </summary>
        public static IObservable<IReadOnlyCollection<T>> StartWithEmpty<T>(this IObservable<IReadOnlyCollection<T>> source)
        {
            return source.StartWith(ReadOnlyCollectionLight<T>.Empty);
        }

        #endregion

        #region Conversion

        /// <summary>
        /// Removes the key which enables all observable list features of dynamic data
        /// </summary>
        /// <remarks>
        /// All indexed changes are dropped i.e. sorting is not supported by this function
        /// </remarks>
        /// <typeparam name="TObject">The type of  object.</typeparam>
        /// <typeparam name="TKey">The type of  key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TObject>> RemoveKey<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Select(changes =>
            {
                var enumerator = new RemoveKeyEnumerator<TObject, TKey>(changes);
                return new ChangeSet<TObject>(enumerator);
            });
        }

        /// <summary>
        /// Changes the primary key.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector eg. (item) => newKey;</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TObject, TDestinationKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return source.Select(updates =>
            {
                var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Current), u.Current, u.Previous));
                return new ChangeSet<TObject, TDestinationKey>(changed);
            });
        }

        /// <summary>
        /// Changes the primary key.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector eg. (key, item) => newKey;</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TSourceKey, TObject, TDestinationKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return source.Select(updates =>
            {
                var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Key, u.Current), u.Current, u.Previous));
                return new ChangeSet<TObject, TDestinationKey>(changed);
            });
        }

        /// <summary>
        /// Convert the object using the sepcified conversion function.
        /// This is a lighter equivalent of Transform and is designed to be used with non-disposable objects
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="conversionFactory">The conversion factory.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        [Obsolete("This was an experiment that did not work. Use Transform instead")]
        public static IObservable<IChangeSet<TDestination, TKey>> Convert<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                       Func<TObject, TDestination> conversionFactory)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (conversionFactory == null) throw new ArgumentNullException(nameof(conversionFactory));
            return source.Select(changes =>
            {
                var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason,
                                                                                          change.Key,
                                                                                          conversionFactory(change.Current),
                                                                                          change.Previous.Convert(conversionFactory),
                                                                                          change.CurrentIndex,
                                                                                          change.PreviousIndex));
                return new ChangeSet<TDestination, TKey>(transformed);
            });
        }


        /// <summary>
        /// Cast the object to the specified type. 
        /// Alas, I had to add the converter due to type inference issues 
        /// </summary>
        /// <typeparam name="TSource">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="converter">The conversion factory.</param>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Cast<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)

        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new Cast<TSource, TKey, TDestination>(source, converter).Run();
        }


        #endregion

        #region Delayed Stream

        /// <summary>
        /// Batches the updates for the specified time period
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSpan">The time span.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// scheduler</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Batch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                  TimeSpan timeSpan,
                                                                                  IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Buffer(timeSpan, scheduler ?? Scheduler.Default).FlattenBufferResult();
        }

        /// <summary>
        /// Convert the result of a buffer operation to a single change set
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> FlattenBufferResult<TObject, TKey>([NotNull] this IObservable<IList<IChangeSet<TObject, TKey>>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Where(x => x.Count != 0)
                         .Select(updates => new ChangeSet<TObject, TKey>(updates.SelectMany(u => u)));
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                    IObservable<bool> pauseIfTrueSelector,
                                                                                    IScheduler scheduler = null)
        {
            return BatchIf(source, pauseIfTrueSelector, false, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                    IObservable<bool> pauseIfTrueSelector,
                                                                                    bool intialPauseState = false,
                                                                                    IScheduler scheduler = null)
        {
            return BatchIf(source, pauseIfTrueSelector, intialPauseState, null, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                    IObservable<bool> pauseIfTrueSelector,
                                                                                    TimeSpan? timeOut = null,
                                                                                    IScheduler scheduler = null)
        {
            return BatchIf(source, pauseIfTrueSelector, false, timeOut, scheduler);
        }

        /// <summary>
        /// Batches the underlying updates if a pause signal (i.e when the buffer selector return true) has been received.
        /// When a resume signal has been received the batched updates will  be fired.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pauseIfTrueSelector">When true, observable begins to buffer and when false, window closes and buffered result if notified</param>
        /// <param name="intialPauseState">if set to <c>true</c> [intial pause state].</param>
        /// <param name="timeOut">Specify a time to ensure the buffer window does not stay open for too long</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                    IObservable<bool> pauseIfTrueSelector,
                                                                                    bool intialPauseState = false,
                                                                                    TimeSpan? timeOut = null,
                                                                                    IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pauseIfTrueSelector == null) throw new ArgumentNullException(nameof(pauseIfTrueSelector));

            return new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, intialPauseState, timeOut, scheduler).Run();
        }

        /// <summary>
        /// Defer the subscribtion until loaded and skip initial changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> SkipInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.DeferUntilLoaded().Skip(1);
        }

        /// <summary>
        /// Defer the subscription until the stream has been inflated with data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new DeferUntilLoaded<TObject, TKey>(source).Run();
        }

        /// <summary>
        /// Defer the subscription until the stream has been inflated with data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new DeferUntilLoaded<TObject, TKey>(source).Run();
        }

        #endregion

        #region True for all values

        /// <summary>
        /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// 
        /// i) The cache changes
        /// or ii) The inner observable changes
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">Selector which returns the target observable</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                          Func<TObject, IObservable<TValue>> observableSelector,
                                                                          Func<TValue, bool> equalityCondition)
        {
            return source.TrueFor(observableSelector,
                                  items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));
        }

        /// <summary>
        /// Produces a boolean observable indicating whether the latest resulting value from all of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// 
        /// i) The cache changes
        /// or ii) The inner observable changes
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">Selector which returns the target observable</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<bool> TrueForAll<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                          Func<TObject, IObservable<TValue>> observableSelector,
                                                                          Func<TObject, TValue, bool> equalityCondition)
        {
            return source.TrueFor(observableSelector,
                                  items => items.All(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));
        }

        /// <summary>
        /// Produces a boolean observable indicating whether the resulting value of whether any of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// i) The cache changes.
        /// or ii) The inner observable changes.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// observableSelector
        /// or
        /// equalityCondition
        /// </exception>
        public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                          Func<TObject, IObservable<TValue>> observableSelector,
                                                                          Func<TObject, TValue, bool> equalityCondition)
        {
            return source.TrueFor(observableSelector,
                                  items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));
        }

        /// <summary>
        /// Produces a boolean observable indicating whether the resulting value of whether any of the specified observables matches
        /// the equality condition. The observable is re-evaluated whenever
        /// i) The cache changes.
        /// or ii) The inner observable changes.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="observableSelector">The observable selector.</param>
        /// <param name="equalityCondition">The equality condition.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// observableSelector
        /// or
        /// equalityCondition
        /// </exception>
        public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                          Func<TObject, IObservable<TValue>> observableSelector,
                                                                          Func<TValue, bool> equalityCondition)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (observableSelector == null) throw new ArgumentNullException(nameof(observableSelector));
            if (equalityCondition == null) throw new ArgumentNullException(nameof(equalityCondition));

            return source.TrueFor(observableSelector,
                                  items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));
        }

        private static IObservable<bool> TrueFor<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                        Func<TObject, IObservable<TValue>> observableSelector,
                                                                        Func<IEnumerable<ObservableWithValue<TObject, TValue>>, bool> collectionMatcher)
        {
            return new TrueFor<TObject, TKey, TValue>(source, observableSelector, collectionMatcher).Run();
        }

        #endregion

        #region Entire Collection Operators

        /// <summary>
        ///  The latest copy of the cache is exposed for querying after each modification to the underlying data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="resultSelector">The result selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// resultSelector
        /// </exception>
        public static IObservable<TDestination> QueryWhenChanged<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                              Func<IQuery<TObject, TKey>, TDestination> resultSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            return source.QueryWhenChanged().Select(resultSelector);
        }

        /// <summary>
        /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new QueryWhenChanged<TObject, TKey, Unit>(source).Run();
        }

        /// <summary>
        /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) on subscription
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="itemChangedTrigger">Should the query be triggered for observables on individual items</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey, TValue>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] Func<TObject, IObservable<TValue>> itemChangedTrigger)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (itemChangedTrigger == null) throw new ArgumentNullException(nameof(itemChangedTrigger));

            return new QueryWhenChanged<TObject, TKey, TValue>(source, itemChangedTrigger).Run();
        }

        /// <summary>
        /// Converts the changeset into a fully formed collection. Each change in the source results in a new collection
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        {
            return source.QueryWhenChanged(query => new ReadOnlyCollectionLight<TObject>(query.Items));
        }

        #endregion

        #region Watch

        /// <summary>
        /// Watches updates for a single value matching the specified key
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservableCache<TObject, TKey> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Watch(key).Select(u => u.Current);
        }

        /// <summary>
        /// Watches updates for a single value matching the specified key
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Watch(key).Select(u => u.Current);
        }

        /// <summary>
        /// Returns an observable of any updates which match the specified key,  preceeded with the initital cache state
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public static IObservable<Change<TObject, TKey>> Watch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.SelectMany(updates => updates).Where(update => update.Key.Equals(key));
        }

        #endregion

        #region Clone


        /// <summary>
        /// Clones the changes  into the specified collection
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, [NotNull] ICollection<TObject> target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            return source.Do(changes =>
            {
                foreach (var item in changes)
                {
                    switch (item.Reason)
                    {
                        case ChangeReason.Add:
                            {
                                target.Add(item.Current);
                            }
                            break;
                        case ChangeReason.Update:
                            {
                                target.Remove(item.Previous.Value);
                                target.Add(item.Current);
                            }
                            break;
                        case ChangeReason.Remove:
                            target.Remove(item.Current);
                            break;
                    }
                }
            });
        }

        #endregion

        #region Auto removal

        /// <summary>
        /// Automatically removes items from the stream after the time specified by
        /// the timeSelector elapses.  Return null if the item should never be removed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">The time selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// timeSelector
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                        Func<TObject, TimeSpan?> timeSelector)
        {
            return ExpireAfter(source, timeSelector, Scheduler.Default);
        }

        /// <summary>
        /// Automatically removes items from the stream after the time specified by
        /// the timeSelector elapses.  Return null if the item should never be removed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="timeSelector">The time selector.</param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// timeSelector
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                        Func<TObject, TimeSpan?> timeSelector, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));

            return source.ExpireAfter(timeSelector, null, scheduler);
        }

        /// <summary>
        /// Automatically removes items from the stream on the next poll after the time specified by
        /// the time selector elapses 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="pollingInterval">The polling interval.  if this value is specified,  items are expired on an interval.
        /// This will result in a loss of accuracy of the time which the item is expired but is less computationally expensive.
        /// </param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                        Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval)
        {
            return ExpireAfter(source, timeSelector, pollingInterval, Scheduler.Default);
        }

        /// <summary>
        /// Automatically removes items from the stream on the next poll after the time specified by
        /// the time selector elapses 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The cache.</param>
        /// <param name="timeSelector">The time selector.  Return null if the item should never be removed</param>
        /// <param name="pollingInterval">The polling interval.  if this value is specified,  items are expired on an interval.
        /// This will result in a loss of accuracy of the time which the item is expired but is less computationally expensive.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ExpireAfter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                        Func<TObject, TimeSpan?> timeSelector, TimeSpan? pollingInterval, IScheduler scheduler)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (timeSelector == null) throw new ArgumentNullException(nameof(timeSelector));
            return new TimeExpirer<TObject, TKey>(source, timeSelector, pollingInterval, scheduler).ExpireAfter();
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
        /// it may be worth setting the interval.
        /// </param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns>An observable of anumerable of the kev values which has been removed</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// timeSelector</exception>
        internal static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> ForExpiry<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                       Func<TObject, TimeSpan?> timeSelector,
                                                                                                       TimeSpan? interval,
                                                                                                       IScheduler scheduler)
        {
            return new TimeExpirer<TObject, TKey>(source, timeSelector, interval, scheduler).ForExpiry();
        }

        /// <summary>
        /// Applies a size limiter to the number of records which can be included in the 
        /// underlying cache.  When the size limit is reached the oldest items are removed.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        /// <exception cref="System.ArgumentException">size cannot be zero</exception>
        public static IObservable<IChangeSet<TObject, TKey>> LimitSizeTo<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source, int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size <= 0) throw new ArgumentException("Size limit must be greater than zero");

            return new SizeExpirer<TObject, TKey>(source, size).Run();
        }

        #endregion

        #region Paged

        /// <summary>
        /// Returns the page as specified by the page controller
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="controller">The controller.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IPagedChangeSet<TObject, TKey>> Page<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source,
                                                                                      PageController controller)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            return source.Page(controller.Changed);
        }

        /// <summary>
        /// Returns the page as specified by the pageRequests observable
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pageRequests">The page requests.</param>
        /// <returns></returns>
        public static IObservable<IPagedChangeSet<TObject, TKey>> Page<TObject, TKey>([NotNull] this IObservable<ISortedChangeSet<TObject, TKey>> source,
            [NotNull] IObservable<IPageRequest> pageRequests)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pageRequests == null) throw new ArgumentNullException(nameof(pageRequests));
            return new Page<TObject, TKey>(source, pageRequests).Run();
        }

        #endregion

        #region  Filter

        /// <summary>
        /// Filters the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filter">The filter.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return new StaticFilter<TObject, TKey>(source, filter).Run();
        }

        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] IObservable<Func<TObject, bool>> predicateChanged)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicateChanged == null) throw new ArgumentNullException(nameof(predicateChanged));
            return new DynamicFilter<TObject, TKey>(source, predicateChanged).Run();
        }

        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] IObservable<Unit> reapplyFilter)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (reapplyFilter == null) throw new ArgumentNullException(nameof(reapplyFilter));
            return new DynamicFilter<TObject, TKey>(source, null, reapplyFilter).Run();
        }


        /// <summary>
        /// Creates a filtered stream which can be dynamically filtered
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="reapplyFilter">Observable to re-evaluate whether the filter still matches items. Use when filtering on mutable values</param>
        /// <param name="predicateChanged">Observable to change the underlying predicate.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
            [NotNull] IObservable<Func<TObject, bool>> predicateChanged,
            [NotNull] IObservable<Unit> reapplyFilter)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicateChanged == null) throw new ArgumentNullException(nameof(predicateChanged));
            if (reapplyFilter == null) throw new ArgumentNullException(nameof(reapplyFilter));
            return new DynamicFilter<TObject, TKey>(source, predicateChanged, reapplyFilter).Run();
        }

        /// <summary>
        /// A filtered observerable where the filter is changed using the filter controller
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filterController">The filter.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                   FilterController<TObject> filterController)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (filterController == null) throw new ArgumentNullException(nameof(filterController));

            return new DynamicFilter<TObject, TKey>(source, filterController.FilterChanged, filterController.EvaluateChanged.ToUnit()).Run();
        }

        /// <summary>
        /// Filters source on the specified property using the specified predicate.
        /// The filter will automatically reapply when a property changes.
        /// When there are likely to be a large number of property changes specify a throttle to improve performance
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector. When the property changes a the filter specified will be re-evaluated</param>
        /// <param name="predicate">A predicate based on the object which contains the changed property</param>
        /// <param name="propertyChangedThrottle">The property changed throttle.</param>
        /// <param name="scheduler">The scheduler used when throttling</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TObject, TKey>> FilterOnProperty<TObject, TKey, TProperty>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Expression<Func<TObject, TProperty>> propertySelector,
            Func<TObject, bool> predicate,
            TimeSpan? propertyChangedThrottle = null,
            IScheduler scheduler = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            return new FilterOnProperty<TObject, TKey, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
        }

        #endregion

        #region Interface aware

        /// <summary>
        /// Updates the index for an object which implements IIndexAware
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> UpdateIndex<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
            where TObject : IIndexAware
        {
            return source.Do(changes => changes.SortedItems.Select((update, index) => new { update, index }).ForEach(u => u.update.Value.Index = u.index));
        }

        /// <summary>
        /// Invokes Evaluate method for an object which implements IEvaluateAware
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> InvokeEvaluate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
            where TObject : IEvaluateAware
        {
            return source.Do(changes => changes.Where(u => u.Reason == ChangeReason.Evaluate).ForEach(u => u.Current.Evaluate()));
        }

        #endregion

        #region Sort

        /// <summary>
        /// Sorts using the specified comparer.
        /// Returns the underlying ChangeSet as as per the system conventions.
        /// The resulting changeset also exposes a sorted key value collection of of the underlying cached data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="sortOptimisations">Sort optimisation flags. Specify one or more sort optimisations</param>
        /// <param name="resetThreshold">The number of updates before the entire list is resorted (rather than inline sore)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// comparer
        /// </exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                       IComparer<TObject> comparer,
                                                                                       SortOptimisations sortOptimisations = SortOptimisations.None,
                                                                                       int resetThreshold = -1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            return new Sort<TObject, TKey>(source, comparer, sortOptimisations, resetThreshold: resetThreshold).Run();
        }

        /// <summary>
        /// Sorts a sequence as dictated by the sort controller.
        /// Sequence returns a changeset as as per the system conventions.
        /// Additionally returns a fully sort collection of cached data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="sortController">The controlled sort.</param>
        /// <param name="sortOptimisations">Sort optimisation flags. Specify one or more sort optimisations</param>
        /// <param name="resetThreshold">The number of updates before the entire list is resorted (rather than inline sore)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">scheduler</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                            [NotNull] SortController<TObject> sortController,
                                            SortOptimisations sortOptimisations = SortOptimisations.None,
                                            int resetThreshold = -1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sortController == null) throw new ArgumentNullException(nameof(sortController));
            return new Sort<TObject, TKey>(source, null, sortOptimisations, sortController.ComparerChanged, sortController.SortAgain, resetThreshold).Run();
        }

        /// <summary>
        /// Sorts a sequence as, using the comparer observable to determine order
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparerObservable">The comparer observable.</param>
        /// <param name="sortOptimisations">The sort optimisations.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns></returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<IComparer<TObject>> comparerObservable,
            SortOptimisations sortOptimisations = SortOptimisations.None,
            int resetThreshold = -1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparerObservable == null) throw new ArgumentNullException(nameof(comparerObservable));

            return new Sort<TObject, TKey>(source, null, sortOptimisations, comparerObservable, resetThreshold: resetThreshold).Run();
        }

        /// <summary>
        /// Sorts a sequence as, using the comparer observable to determine order
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparerObservable">The comparer observable.</param>
        /// <param name="resorter">Signal to instruct the algroirthm to re-sort the entire data set</param>
        /// <param name="sortOptimisations">The sort optimisations.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns></returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            IObservable<IComparer<TObject>> comparerObservable,
            IObservable<Unit> resorter,
            SortOptimisations sortOptimisations = SortOptimisations.None,
            int resetThreshold = -1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparerObservable == null) throw new ArgumentNullException(nameof(comparerObservable));

            return new Sort<TObject, TKey>(source, null, sortOptimisations, comparerObservable, resorter, resetThreshold).Run();
        }

        /// <summary>
        /// Sorts a sequence as, using the comparer observable to determine order
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer to sort on</param>
        /// <param name="resorter">Signal to instruct the algroirthm to re-sort the entire data set</param>
        /// <param name="sortOptimisations">The sort optimisations.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <returns></returns>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Sort<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            IComparer<TObject> comparer,
            IObservable<Unit> resorter,
            SortOptimisations sortOptimisations = SortOptimisations.None,
            int resetThreshold = -1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (resorter == null) throw new ArgumentNullException(nameof(resorter));

            return new Sort<TObject, TKey>(source, comparer, sortOptimisations, null, resorter, resetThreshold).Run();
        }


        #endregion

        #region   And, or, except

        /// <summary>
        /// Applied a logical And operator between the collections i.e items which are in all of the sources are included
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (others == null || others.Length == 0) throw new ArgumentNullException(nameof(others));

            return source.Combine(CombineOperator.And, others);
        }

        /// <summary>
        /// Applied a logical And operator between the collections i.e items which are in all of the sources are included
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in all of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in all of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Dynamically apply a logical And operator between the items in the outer observable list.
        /// Items which are in all of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.And);
        }

        /// <summary>
        /// Apply a logical Or operator between the collections i.e items which are in any of the sources are included
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                               params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (others == null || others.Length == 0) throw new ArgumentNullException(nameof(others));

            return source.Combine(CombineOperator.Or, others);
        }

        /// <summary>
        /// Apply a logical Or operator between the collections i.e items which are in any of the sources are included
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Dynamically apply a logical Or operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Or);
        }

        /// <summary>
        /// Apply a logical Xor operator between the collections. 
        /// Items which are only in one of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (others == null || others.Length == 0) throw new ArgumentNullException(nameof(others));

            return source.Combine(CombineOperator.Xor, others);
        }

        /// <summary>
        /// Apply a logical Xor operator between the collections. 
        /// Items which are only in one of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are only in one of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Xor operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Xor);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the collections 
        /// Items from the first collection in the outer list are included unless contained in any of the other lists
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="others">The others.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                   params IObservable<IChangeSet<TObject, TKey>>[] others)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (others == null || others.Length == 0) throw new ArgumentNullException(nameof(others));

            return source.Combine(CombineOperator.Except, others);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the collections 
        /// Items from the first collection in the outer list are included unless contained in any of the other lists
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The sources.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// others
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the collections 
        /// Items from the first collection in the outer list are included unless contained in any of the other lists
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Except);
        }

        /// <summary>
        /// Dynamically apply a logical Except operator between the items in the outer observable list.
        /// Items which are in any of the sources are included in the result
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return sources.Combine(CombineOperator.Except);
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>([NotNull] this IObservableList<IObservableCache<TObject, TKey>> source, CombineOperator type)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                var subscriber = connections.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(connections, subscriber);
            });
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>([NotNull] this IObservableList<ISourceCache<TObject, TKey>> source, CombineOperator type)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                var subscriber = connections.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(connections, subscriber);
            });
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>([NotNull] this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, CombineOperator type)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new DynamicCombiner<TObject, TKey>(source, type).Run();
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources, CombineOperator type)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        Action<IChangeSet<TObject, TKey>> updateAction = updates =>
                        {
                            try
                            {
                                observer.OnNext(updates);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                            }
                        };
                        IDisposable subscriber = Disposable.Empty;
                        try
                        {
                            var combiner = new Combiner<TObject, TKey>(type, updateAction);
                            subscriber = combiner.Subscribe(sources.ToArray());
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                            observer.OnCompleted();
                        }

                        return subscriber;
                    });
        }

        private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            CombineOperator type,
            params IObservable<IChangeSet<TObject, TKey>>[] combinetarget)
        {
            if (combinetarget == null) throw new ArgumentNullException(nameof(combinetarget));

            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        Action<IChangeSet<TObject, TKey>> updateAction = updates =>
                        {
                            try
                            {
                                observer.OnNext(updates);
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                                observer.OnCompleted();
                            }
                        };
                        IDisposable subscriber = Disposable.Empty;
                        try
                        {
                            var list = combinetarget.ToList();
                            list.Insert(0, source);

                            var combiner = new Combiner<TObject, TKey>(type, updateAction);
                            subscriber = combiner.Subscribe(list.ToArray());
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                            observer.OnCompleted();
                        }

                        return subscriber;
                    });
        }

        /// <summary>
        /// The equivalent of rx startwith operator, but wraps the item in a change where reason is ChangeReason.Add
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                          TObject item) where TObject : IKey<TKey>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.StartWithItem(item, item.Key);
        }

        /// <summary>
        /// The equivalent of rx startwith operator, but wraps the item in a change where reason is ChangeReason.Add
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                          TObject item, TKey key)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.StartWith(new ChangeSet<TObject, TKey>(ChangeReason.Add, key, item));
        }

        #endregion

        #region  Transform

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Func<TSource, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            return source.Transform((current, previous, key) => transformFactory(current), forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            return source.Transform((current, previous, key) => transformFactory(current, key), forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return new Transform<TDestination, TSource, TKey>(source, transformFactory, null, forceTransform).Run();
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, IObservable<Unit> forceTransform)
        {
            return source.Transform((cur, prev, key) => transformFactory(cur), forceTransform.ForForced<TSource, TKey>());
        }


        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>#
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (forceTransform == null) throw new ArgumentNullException(nameof(forceTransform));

            return source.Transform((cur, prev, key) => transformFactory(cur, key), forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>#
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory, IObservable<Unit> forceTransform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (forceTransform == null) throw new ArgumentNullException(nameof(forceTransform));

            return source.Transform(transformFactory, forceTransform.ForForced<TSource, TKey>());
        }

        private static IObservable<Func<TSource, TKey, bool>> ForForced<TSource, TKey>(this IObservable<Unit> source)
        {
            return source?.Select(_ =>
            {
                Func<TSource, TKey, bool> transformer = (item, key) => true;
                return transformer;
            });
        }

        private static IObservable<Func<TSource, TKey, bool>> ForForced<TSource, TKey>(this IObservable<Func<TSource, bool>> source)
        {
            return source?.Select(condition =>
            {
                Func<TSource, TKey, bool> transformer = (item, key) => condition(item);
                return transformer;
            });
        }

        #endregion

        #region Transform Async

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="maximumConcurrency">The maximum concurrent tasks used to perform transforms.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, Task<TDestination>> transformFactory,
                                                                                                         IObservable<Func<TSource, TKey, bool>> forceTransform = null,
                                                                                                         int maximumConcurrency = 1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return source.TransformAsync((current, previous, key) => transformFactory(current), maximumConcurrency, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="maximumConcurrency">The maximum concurrent tasks used to perform transforms.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, TKey, Task<TDestination>> transformFactory,
                                                                                                         int maximumConcurrency = 1,
                                                                                                         IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return source.TransformAsync((current, previous, key) => transformFactory(current, key), maximumConcurrency, forceTransform);
        }


        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="maximumConcurrency">The maximum concurrent tasks used to perform transforms.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory,
                                                                                                         int maximumConcurrency = 1,
                                                                                                         IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));

            return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, null, maximumConcurrency, forceTransform).Run();
        }

        #endregion

        #region Transform many

        /// <summary>
        /// Equivalent to a select many transform. To work, the key must individually identify each child. 
        /// 
        /// **** Assumes each child can only have one  parent - support for children with multiple parents is a work in progresss
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>s
        /// <returns></returns>
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source,
                                                                 Func<TSource, IEnumerable<TDestination>> manyselector)
            where TDestination : IKey<TDestinationKey>
        {
            return source.FlattenWithSingleParent(manyselector, t => t.Key);
        }

        /// <summary>
        /// Equivalent to a select many transform. To work, the key must individually identify each child. 
        /// 
        /// **** Assumes each child can only have one  parent - support for children with multiple parents is a work in progresss
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="keySelector">The key selector which must be unique across all</param>
        /// <param name="childHasOneParent">if set to <c>true</c> the child only ever belongs to one parent</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(
            this IObservable<IChangeSet<TSource, TSourceKey>> source,
            Func<TSource, IEnumerable<TDestination>> manyselector, Func<TDestination, TDestinationKey> keySelector,
            bool childHasOneParent = true)
        {
            return source.FlattenWithSingleParent(manyselector, keySelector);
        }

        /// <summary>
        /// Flattens the with single parent.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        internal static IObservable<IChangeSet<TDestination, TDestinationKey>> FlattenWithSingleParent<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source,
                                                                 Func<TSource, IEnumerable<TDestination>> manyselector, Func<TDestination, TDestinationKey> keySelector)
        {
            return new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manyselector, keySelector).Run();
        }

        #endregion

        #region Transform safe

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));
            return source.TransformSafe((current, previous, key) => transformFactory(current), errorHandler, forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));
            return source.TransformSafe((current, previous, key) => transformFactory(current, key), errorHandler, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory,
            Action<Error<TSource, TKey>> errorHandler,
            IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

            return new Transform<TDestination, TSource, TKey>(source, transformFactory, errorHandler, forceTransform).Run();
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        {
            return source.TransformSafe((cur, prev, key) => transformFactory(cur), errorHandler, forceTransform.ForForced<TSource, TKey>());
        }


        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>#
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TKey, TDestination> transformFactory, Action<Error<TSource, TKey>> errorHandler, IObservable<Unit> forceTransform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (forceTransform == null) throw new ArgumentNullException(nameof(forceTransform));

            return source.TransformSafe((cur, prev, key) => transformFactory(cur, key), errorHandler, forceTransform.ForForced<TSource, TKey>());
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function,
        /// providing an error handling action to safely handle transform errors without killing the stream.
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="forceTransform">Invoke to force a new transform for all items</param>#
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory,
            Action<Error<TSource, TKey>> errorHandler,
            IObservable<Unit> forceTransform)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (forceTransform == null) throw new ArgumentNullException(nameof(forceTransform));

            return source.TransformSafe(transformFactory, errorHandler, forceTransform.ForForced<TSource, TKey>());
        }


        #endregion

        #region Transform safe async



        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">The error handler.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="maximumConcurrency">The maximum concurrent tasks used to perform transforms.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, Task<TDestination>> transformFactory,
                                                                                                         Action<Error<TSource, TKey>> errorHandler,
                                                                                                         IObservable<Func<TSource, TKey, bool>> forceTransform = null,
                                                                                                         int maximumConcurrency = 1)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

            return source.TransformSafeAsync((current, previous, key) => transformFactory(current), errorHandler, maximumConcurrency, forceTransform);
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">The error handler.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="maximumConcurrency">The maximum concurrent tasks used to perform transforms.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, TKey, Task<TDestination>> transformFactory,
                                                                                                         Action<Error<TSource, TKey>> errorHandler,
                                                                                                         int maximumConcurrency = 1,
                                                                                                         IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

            return source.TransformSafeAsync((current, previous, key) => transformFactory(current, key), errorHandler, maximumConcurrency, forceTransform);
        }


        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="errorHandler">The error handler.</param>
        /// <param name="forceTransform">Invoke to force a new transform for items matching the selected objects</param>
        /// <param name="maximumConcurrency">The maximum concurrent tasks used to perform transforms.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafeAsync<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, Optional<TSource>, TKey, Task<TDestination>> transformFactory,
                                                                                                         Action<Error<TSource, TKey>> errorHandler,
                                                                                                         int maximumConcurrency = 1,
                                                                                                         IObservable<Func<TSource, TKey, bool>> forceTransform = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

            return new TransformAsync<TDestination, TSource, TKey>(source, transformFactory, errorHandler, maximumConcurrency, forceTransform).Run();
        }



        /// <summary>
        /// Transforms the object to a fully recursive tree, create a hiearchy based on the pivot function
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="pivotOn">The pivot on.</param>
        /// <returns></returns>
        public static IObservable<IChangeSet<Node<TObject, TKey>, TKey>> TransformToTree<TObject, TKey>([NotNull] this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                        [NotNull] Func<TObject, TKey> pivotOn)
            where TObject : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pivotOn == null) throw new ArgumentNullException(nameof(pivotOn));
            return new TreeBuilder<TObject, TKey>(source, pivotOn).Run();
        }



        #endregion

        #region Distinct values

        /// <summary>
        ///     Selects distinct values from the source.
        /// </summary>
        /// <typeparam name="TObject">The tyoe object from which the distinct values are selected</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="source">The soure.</param>
        /// <param name="valueSelector">The value selector.</param>
        /// <returns></returns>
        /// <remarks>
        /// Due to it's nature only adds or removes can be returned
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IDistinctChangeSet<TValue>> DistinctValues<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TValue> valueSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));

            return Observable.Create<IDistinctChangeSet<TValue>>(observer =>
            {
                return new DistinctCalculator<TObject, TKey, TValue>(source, valueSelector).Run().SubscribeSafe(observer);
            });
        }

        #endregion

        #region   Grouping

        /// <summary>
        ///  Groups the source on the value returned by group selector factory. 
        ///  A group is included for each item in the resulting group source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelector">The group selector factory.</param> 
        /// <param name="resultGroupSource">
        ///   A distinct stream used to determine the result
        /// </param>
        /// <remarks>
        /// Useful for parent-child collection when the parent and child are soured from different streams
        /// </remarks>
        /// <returns></returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TGroupKey> groupSelector,
            IObservable<IDistinctChangeSet<TGroupKey>> resultGroupSource)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelector == null) throw new ArgumentNullException(nameof(groupSelector));
            if (resultGroupSource == null) throw new ArgumentNullException(nameof(resultGroupSource));

            return new SpecifiedGrouper<TObject, TKey, TGroupKey>(source, groupSelector, resultGroupSource).Run();
        }

        /// <summary>
        ///  Groups the source on the value returned by group selector factory. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <returns></returns>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TGroupKey> groupSelectorKey)

        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelectorKey == null) throw new ArgumentNullException(nameof(groupSelectorKey));

            return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, null).Run();
        }

        /// <summary>
        ///  Groups the source on the value returned by group selector factory. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <param name="groupController">The group controller which enables reapplying the group</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// groupSelectorKey
        /// or
        /// groupController
        /// </exception>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                             Func<TObject, TGroupKey> groupSelectorKey,
                                                                                                             GroupController groupController)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelectorKey == null) throw new ArgumentNullException(nameof(groupSelectorKey));
            if (groupController == null) throw new ArgumentNullException(nameof(groupController));

            return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, groupController.Regrouped).Run();
        }

        /// <summary>
        ///  Groups the source on the value returned by group selector factory. 
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <param name="regrouper">Invoke to  the for the grouping to be re-evaluated</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// groupSelectorKey
        /// or
        /// groupController
        /// </exception>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Group<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                             Func<TObject, TGroupKey> groupSelectorKey,
                                                                                                             IObservable<Unit> regrouper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelectorKey == null) throw new ArgumentNullException(nameof(groupSelectorKey));
            if (regrouper == null) throw new ArgumentNullException(nameof(regrouper));

            return new GroupOn<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
        }



        /// <summary>
        ///  Groups the source on the value returned by group selector factory. Each update produces immuatable grouping.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="groupSelectorKey">The group selector key.</param>
        /// <param name="regrouper">Invoke to  the for the grouping to be re-evaluated</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// groupSelectorKey
        /// or
        /// groupController
        /// </exception>
        public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> GroupWithImmutableState<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                             Func<TObject, TGroupKey> groupSelectorKey,
                                                                                                             IObservable<Unit> regrouper = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelectorKey == null) throw new ArgumentNullException(nameof(groupSelectorKey));

            return new GroupOnImmutable<TObject, TKey, TGroupKey>(source, groupSelectorKey, regrouper).Run();
        }

        /// <summary>
        /// Groups the source using the property specified by the property selector. Groups are re-applied when the property value changed.
        /// 
        /// When there are likely to be a large number of group property changes specify a throttle to improve performance
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector used to group the items</param>
        /// <param name="propertyChangedThrottle"></param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnProperty<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                     Expression<Func<TObject, TGroupKey>> propertySelector,
                                                                                                     TimeSpan? propertyChangedThrottle = null,
                                                                                                     IScheduler scheduler = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));

            return new GroupOnProperty<TObject, TKey, TGroupKey>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
        }

        /// <summary>
        /// Groups the source using the property specified by the property selector. Each update produces immuatable grouping. Groups are re-applied when the property value changed.
        /// 
        /// When there are likely to be a large number of group property changes specify a throttle to improve performance
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <typeparam name="TGroupKey">The type of the group key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="propertySelector">The property selector used to group the items</param>
        /// <param name="propertyChangedThrottle"></param>
        /// <param name="scheduler">The scheduler.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> GroupOnPropertyWithImmutableState<TObject, TKey, TGroupKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                                     Expression<Func<TObject, TGroupKey>> propertySelector,
                                                                                                     TimeSpan? propertyChangedThrottle = null,
                                                                                                     IScheduler scheduler = null)
            where TObject : INotifyPropertyChanged
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (propertySelector == null) throw new ArgumentNullException(nameof(propertySelector));

            return new GroupOnPropertyWithImmutableState<TObject, TKey, TGroupKey>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
        }

        #endregion

        #region Virtualisation

        /// <summary>
        /// Limits the size of the result set to the specified number
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">size;Size should be greater than zero</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Top<TObject, TKey>(
            this IObservable<ISortedChangeSet<TObject, TKey>> source, int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size should be greater than zero");
            return new Virtualise<TObject, TKey>(source, Observable.Return(new VirtualRequest(0, size))).Run();
        }

        /// <summary>
        /// Limits the size of the result set to the specified number, ordering by the comparer
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="comparer">The comparer.</param>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">size;Size should be greater than zero</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Top<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            IComparer<TObject> comparer,
            int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size should be greater than zero");

            return source.Sort(comparer).Top(size);
        }

        /// <summary>
        /// Virtualises the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="virtualisingController">The virtualising controller.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Virtualise<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source,
                                                                                              VirtualisingController virtualisingController)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (virtualisingController == null) throw new ArgumentNullException(nameof(virtualisingController));

            return source.Virtualise(virtualisingController.Changed);
        }

        /// <summary>
        /// Virtualises the underlying data from the specified source.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="virtualRequests">The virirtualising requests</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IVirtualChangeSet<TObject, TKey>> Virtualise<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source,
                                                                                              IObservable<IVirtualRequest> virtualRequests)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (virtualRequests == null) throw new ArgumentNullException(nameof(virtualRequests));
            return new Virtualise<TObject, TKey>(source, virtualRequests).Run();
        }

        #endregion

        #region Binding

        /// <summary>
        ///  Binds the results to the specified observable collection collection using the default update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                 IObservableCollection<TObject> destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            var updater = new ObservableCollectionAdaptor<TObject, TKey>();
            return source.Bind(destination, updater);
        }

        /// <summary>
        /// Binds the results to the specified binding collection using the specified update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="updater">The updater.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                 IObservableCollection<TObject> destination,
                                                                                 IObservableCollectionAdaptor<TObject, TKey> updater)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (updater == null) throw new ArgumentNullException(nameof(updater));

            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var locker = new object();
                return source
                    .Synchronize(locker)
                    .Select(changes =>
                    {
                        updater.Adapt(changes, destination);
                        return changes;
                    }).SubscribeSafe(observer);
            });
        }

        /// <summary>
        ///  Binds the results to the specified observable collection collection using the default update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source,
                                                                                       IObservableCollection<TObject> destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            var updater = new SortedObservableCollectionAdaptor<TObject, TKey>();
            return source.Bind(destination, updater);
        }

        /// <summary>
        /// Binds the results to the specified binding collection using the specified update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="updater">The updater.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(
            this IObservable<ISortedChangeSet<TObject, TKey>> source,
            IObservableCollection<TObject> destination,
            ISortedObservableCollectionAdaptor<TObject, TKey> updater)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (updater == null) throw new ArgumentNullException(nameof(updater));

            return Observable.Create<ISortedChangeSet<TObject, TKey>>(observer =>
            {
                var locker = new object();
                return source
                    .Synchronize(locker)
                    .Select(changes =>
                    {
                        updater.Adapt(changes, destination);
                        return changes;
                    }).SubscribeSafe(observer);
            });
        }

        /// <summary>
        /// Binds the results to the specified readonly observable collection collection using the default update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
        /// <param name="resetThreshold">The number of changes before a reset event is called on the observable collection</param>
        /// <param name="adaptor">Specify an adaptor to change the algorithm to update the target collection</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source,
                                                                                 out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
                                                                                 int resetThreshold = 25,
                                                                                 ISortedObservableCollectionAdaptor<TObject, TKey> adaptor = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var target = new ObservableCollectionExtended<TObject>();
            var result = new ReadOnlyObservableCollection<TObject>(target);
            var updater = adaptor ?? new SortedObservableCollectionAdaptor<TObject, TKey>(resetThreshold);
            readOnlyObservableCollection = result;
            return source.Bind(target, updater);
        }

        /// <summary>
        /// Binds the results to the specified readonly observable collection collection using the default update algorithm
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
        /// <param name="resetThreshold">The number of changes before a reset event is called on the observable collection</param>
        /// <param name="adaptor">Specify an adaptor to change the algorithm to update the target collection</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                 out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
                                                                                 int resetThreshold = 25,
                                                                                 IObservableCollectionAdaptor<TObject, TKey> adaptor = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var target = new ObservableCollectionExtended<TObject>();
            var result = new ReadOnlyObservableCollection<TObject>(target);
            var updater = adaptor ?? new ObservableCollectionAdaptor<TObject, TKey>(resetThreshold);
            readOnlyObservableCollection = result;
            return source.Bind(target, updater);
        }

        #endregion

        #region Adaptor

        /// <summary>
        /// Inject side effects into the stream using the specified adaptor
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="adaptor">The adaptor.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// destination
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IChangeSetAdaptor<TObject, TKey> adaptor)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (adaptor == null) throw new ArgumentNullException(nameof(adaptor));

            return source.Do(adaptor.Adapt);
        }

        /// <summary>
        /// Inject side effects into the stream using the specified sorted adaptor
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="adaptor">The adaptor.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// destination
        /// </exception>
        public static IObservable<IChangeSet<TObject, TKey>> Adapt<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, ISortedChangeSetAdaptor<TObject, TKey> adaptor)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (adaptor == null) throw new ArgumentNullException(nameof(adaptor));

            return source.Do(adaptor.Adapt);
        }

        #endregion

        #region Joins
        /// <summary>
        /// Joins the left and right observable data sources, taking values when both left and right values are present
        /// This is the equivalent of SQL inner join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<TLeft, TRight, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return left.InnerJoin(right, rightKeySelector, (leftKey, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        ///  Groups the right data source and joins the to the left and the right sources, taking values when both left and right values are present
        /// This is the equivalent of SQL inner join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<TLeftKey, TLeft, TRight, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return new InnerJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Groups the right data source and joins the resulting group to the left data source, matching these using the specified key selector. Results are included when the left and right have matching values.
        /// This is the equivalent of SQL inner join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return left.InnerJoinMany(right, rightKeySelector, (leftKey, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Groups the right data source and joins the resulting group to the left data source, matching these using the specified key selector. Results are included when the left and right have matching values.
        /// This is the equivalent of SQL inner join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return new InnerJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }


        /// <summary>
        /// Joins the left and right observable data sources, taking any left or right values and matching them, provided that the left or the right has a value.
        /// This is the equivalent of SQL full join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<Optional<TLeft>, Optional<TRight>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return left.FullJoin(right, rightKeySelector, (leftKey, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking any left or right values and matching them, provided that the left or the right has a value.
        /// This is the equivalent of SQL full join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<TLeftKey, Optional<TLeft>, Optional<TRight>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return new FullJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }


        /// <summary>
        /// Groups the right data source and joins the resulting group to the left data source, matching these using the specified key selector. Results are included when the left or the right has a value.
        /// This is the equivalent of SQL full join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull] Func<TRight, TLeftKey> rightKeySelector,
               [NotNull] Func<Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return left.FullJoinMany(right, rightKeySelector, (leftKey, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Groups the right data source and joins the resulting group to the left data source, matching these using the specified key selector. Results are included when the left or the right has a value.
        /// This is the equivalent of SQL full join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull] Func<TRight, TLeftKey> rightKeySelector,
               [NotNull] Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return new FullJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking all left values and combining any matching right values.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull] Func<TRight, TLeftKey> rightKeySelector,
               [NotNull] Func<TLeft, Optional<TRight>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return left.LeftJoin(right, rightKeySelector, (leftKey, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking all left values and combining any matching right values.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<TLeftKey, TLeft, Optional<TRight>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return new LeftJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Groups the right data source and joins the two sources matching them using the specified key selector, taking all left values and combining any matching right values.
        /// This is the equivalent of SQL left join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull] Func<TRight, TLeftKey> rightKeySelector,
               [NotNull] Func<TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return left.LeftJoinMany(right, rightKeySelector, (leftKey, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Groups the right data source and joins the two sources matching them using the specified key selector, taking all left values and combining any matching right values.
        /// This is the equivalent of SQL left join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull] Func<TRight, TLeftKey> rightKeySelector,
               [NotNull] Func<TLeftKey, TLeft, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return new LeftJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking all right values and combining any matching left values.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<Optional<TLeft>, TRight, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return left.RightJoin(right, rightKeySelector, (leftKey, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Joins the left and right observable data sources, taking all right values and combining any matching left values.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull]  Func<TRight, TLeftKey> rightKeySelector,
               [NotNull]  Func<TLeftKey, Optional<TLeft>, TRight, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return new RightJoin<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
        }

        /// <summary>
        /// Groups the right data source and joins the two sources matching them using the specified key selector, , taking all right values and combining any matching left values.
        /// This is the equivalent of SQL left join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which</typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (left, right) =&gt; new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull] Func<TRight, TLeftKey> rightKeySelector,
               [NotNull] Func<Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return left.RightJoinMany(right, rightKeySelector, (leftKey, leftValue, rightValue) => resultSelector(leftValue, rightValue));
        }

        /// <summary>
        /// Groups the right data source and joins the two sources matching them using the specified key selector,, taking all right values and combining any matching left values.
        /// This is the equivalent of SQL left join.
        /// </summary>
        /// <typeparam name="TLeft">The object type of the left datasource</typeparam>
        /// <typeparam name="TLeftKey">The key type of the left datasource</typeparam>
        /// <typeparam name="TRight">The object type of the right datasource</typeparam>
        /// <typeparam name="TRightKey">The key type of the right datasource</typeparam>
        /// <typeparam name="TDestination">The resulting object which </typeparam>
        /// <param name="left">The left data source</param>
        /// <param name="right">The right data source.</param>
        /// <param name="rightKeySelector">Specify the foreign key on the right datasource</param>
        /// <param name="resultSelector">The result selector.used to transform the combined data into. Example (key, left, right) => new CustomObject(key, left, right)</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        public static IObservable<IChangeSet<TDestination, TLeftKey>> RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(this IObservable<IChangeSet<TLeft, TLeftKey>> left,
               [NotNull] IObservable<IChangeSet<TRight, TRightKey>> right,
               [NotNull] Func<TRight, TLeftKey> rightKeySelector,
               [NotNull] Func<TLeftKey, Optional<TLeft>, IGrouping<TRight, TRightKey, TLeftKey>, TDestination> resultSelector)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (rightKeySelector == null) throw new ArgumentNullException(nameof(rightKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            return new RightJoinMany<TLeft, TLeftKey, TRight, TRightKey, TDestination>(left, right, rightKeySelector, resultSelector).Run();
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

        #region AsObservableCache / Connect

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
            return new AnonymousObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Converts the source to a readonly observable cache
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="applyLocking">if set to <c>true</c> all methods are synchronised. There is no need to apply locking when the consumer can be sure the the read / write operations are already synchronised</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, bool applyLocking = true)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (applyLocking)
                return new AnonymousObservableCache<TObject, TKey>(source);

            return new LockFreeObservableCache<TObject, TKey>(source);
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

            return new ToObservableChangeSet<TObject, TKey>(source, keySelector, expireAfter, limitSizeTo, scheduler).Run();
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

            return new ToObservableChangeSet<TObject, TKey>(source, keySelector, expireAfter, limitSizeTo, scheduler).Run();
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
        public static IObservable<IEnumerable<KeyValuePair<TKey, TObject>>> LimitSizeTo<TObject, TKey>(this ISourceCache<TObject, TKey> source, int sizeLimit, IScheduler scheduler = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sizeLimit <= 0) throw new ArgumentException("Size limit must be greater than zero");

            return Observable.Create<IEnumerable<KeyValuePair<TKey, TObject>>>(observer =>
            {
                long orderItemWasAdded = -1;
                var sizeLimiter = new SizeLimiter<TObject, TKey>(sizeLimit);

                return source.Connect()
                             .Finally(observer.OnCompleted)
                             .ObserveOn(scheduler ?? Scheduler.Default)
                             .Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now, Interlocked.Increment(ref orderItemWasAdded)))
                             .Select(sizeLimiter.CloneAndReturnExpiredOnly)
                             .Where(expired => expired.Length != 0)
                             .Subscribe(source.Remove);
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
        /// Loads the cache with the specified items in an optimised manner i.e. calculates the differences between the old and new items
        ///  in the list and amends only the differences
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="alltems"></param>
        /// <param name="equalityComparer">The equality comparer used to determine whether a new item is the same as an existing cached item</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void EditDiff<TObject, TKey>([NotNull] this ISourceCache<TObject, TKey> source,
            [NotNull] IEnumerable<TObject> alltems,
            [NotNull] IEqualityComparer<TObject> equalityComparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (alltems == null) throw new ArgumentNullException(nameof(alltems));
            if (equalityComparer == null) throw new ArgumentNullException(nameof(equalityComparer));
            source.EditDiff(alltems, equalityComparer.Equals);
        }

        /// <summary>
        /// Loads the cache with the specified items in an optimised manner i.e. calculates the differences between the old and new items
        ///  in the list and amends only the differences
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="alltems"></param>
        /// <param name="areItemsEqual">Expression to determine whether an item's value is equal to the old value (current, previous) => current.Version == previous.Version</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void EditDiff<TObject, TKey>([NotNull] this ISourceCache<TObject, TKey> source,
            [NotNull] IEnumerable<TObject> alltems,
            [NotNull] Func<TObject, TObject, bool> areItemsEqual)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (alltems == null) throw new ArgumentNullException(nameof(alltems));
            if (areItemsEqual == null) throw new ArgumentNullException(nameof(areItemsEqual));
            var editDiff = new EditDiff<TObject, TKey>(source, areItemsEqual);
            editDiff.Edit(alltems);
        }



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
        /// Signal observers to re-evaluate the all items.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Evaluate<TObject, TKey>(this ISourceCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Evaluate());
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


        /// <summary>
        /// Clears all data
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static void Clear<TObject, TKey>(this LockFreeObservableCache<TObject, TKey> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            source.Edit(updater => updater.Clear());
        }


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
        public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, LockFreeObservableCache<TObject, TKey> detination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (detination == null) throw new ArgumentNullException(nameof(detination));

            return source.Subscribe(changes => detination.Edit(updater => updater.Update(changes)));
        }

        #endregion

        #region Switch

        /// <summary>
        /// Transforms an observable sequence of observable caches into a single sequence
        /// producing values only from the most recent observable sequence.
        /// Each time a new inner observable sequence is received, unsubscribe from the
        /// previous inner observable sequence and clear the existing result set
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>
        /// The observable sequence that at any point in time produces the elements of the most recent inner observable sequence that has been received.
        /// </returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="sources" /> is null.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservableCache<TObject, TKey>> sources)
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
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="sources">The source.</param>
        /// <returns>
        /// The observable sequence that at any point in time produces the elements of the most recent inner observable sequence that has been received.
        /// </returns>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="sources" /> is null.</exception>

        public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            return new Switch<TObject, TKey>(sources).Run();

        }

        #endregion
    }
}
