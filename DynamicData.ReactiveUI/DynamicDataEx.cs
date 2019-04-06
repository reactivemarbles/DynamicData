using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using ReactiveUI.Legacy;

#pragma warning disable CS0618 // Using legacy code.

namespace DynamicData.ReactiveUI
{

    /// <summary>
    /// Integration methods between dynamic data and reactive list
    /// </summary>
    public static class DynamicDataEx
    {
        /// <summary>
        /// Flattens a nested reactive list
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children</param>
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source,
            Func<TSource, IReadOnlyReactiveList<TDestination>> manyselector,
            IEqualityComparer<TDestination> equalityComparer = null)
        {
            return new TransformMany<TSource, TDestination>(source, manyselector, equalityComparer, t => Observable.Defer(() =>
            {
                var subsequentChanges = manyselector(t).ToObservableChangeSet();

                if (manyselector(t).Count > 0)
                    return subsequentChanges;

                return Observable.Return(ChangeSet<TDestination>.Empty)
                    .Concat(subsequentChanges);
            })).Run();
        }


        /// <summary>
        /// Flattens a nested reactive list
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="equalityComparer">Used when an item has been replaced to determine whether child items are the same as previous children</param>
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source,
            Func<TSource, ReactiveList<TDestination>> manyselector,
            IEqualityComparer<TDestination> equalityComparer = null)
        {
            return new TransformMany<TSource, TDestination>(source, manyselector, equalityComparer,  t => Observable.Defer(() =>
            {
                var subsequentChanges = manyselector(t).ToObservableChangeSet();

                if (manyselector(t).Count > 0)
                    return subsequentChanges;

                return Observable.Return(ChangeSet<TDestination>.Empty)
                    .Concat(subsequentChanges);
            })).Run();
        }

        /// <summary>
        /// Flattens a nested reactive list, using the key selector to ensure only unique items are added
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="keySelector">The key selector which must be unique across all</param>
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(
            this IObservable<IChangeSet<TSource, TSourceKey>> source,
            Func<TSource, ReactiveList<TDestination>> manyselector,
            Func<TDestination, TDestinationKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (manyselector == null) throw new ArgumentNullException(nameof(manyselector));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source,
                manyselector,
                keySelector,
                t => Observable.Defer(() =>
                {
                    var subsequentChanges = manyselector(t).ToObservableChangeSet(keySelector);

                    if (manyselector(t).Count > 0)
                        return subsequentChanges;

                    return Observable.Return(ChangeSet<TDestination, TDestinationKey>.Empty)
                        .Concat(subsequentChanges);
                })).Run();
        }

        /// <summary>
        /// Flattens a nested reactive list, using the key selector to ensure only unique items are added
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="manyselector">The manyselector.</param>
        /// <param name="keySelector">The key selector which must be unique across all</param>
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(
            this IObservable<IChangeSet<TSource, TSourceKey>> source,
            Func<TSource, IReadOnlyReactiveList<TDestination>> manyselector,
            Func<TDestination, TDestinationKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (manyselector == null) throw new ArgumentNullException(nameof(manyselector));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source,
                manyselector,
                keySelector,
                t => Observable.Defer(() =>
                {
                    var subsequentChanges = manyselector(t).ToObservableChangeSet(keySelector);

                    if (manyselector(t).Count > 0)
                        return subsequentChanges;

                    return Observable.Return(ChangeSet<TDestination, TDestinationKey>.Empty)
                        .Concat(subsequentChanges);
                })).Run();
        }

        /// <summary>
        /// Binds the observable changeset to the target ReactiveList
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
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<IChangeSet<T>> Bind<T>([NotNull] this IObservable<IChangeSet<T>> source,
            [NotNull] ReactiveList<T> targetCollection, int resetThreshold = 25)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (targetCollection == null) throw new ArgumentNullException(nameof(targetCollection));

            var adaptor = new ObservableListToReactiveListAdaptor<T>(targetCollection, resetThreshold);
            return source.Adapt(adaptor);
        }


        /// <summary>
        /// Populate and maintain the specified reactive list from the source observable changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="resetThreshold">The reset threshold before a reset event  on the target list is invoked</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        /// destination
        /// or
        /// target
        /// </exception>
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ReactiveList<TObject> target,
            int resetThreshold = 25)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var adaptor = new ObservableCacheToReactiveListAdaptor<TObject, TKey>(target, resetThreshold);
            return source.Bind(adaptor);

        }

        /// <summary>
        /// Binds the results using the specified changeset adaptor
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="updater">The updater.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<IChangeSet<TObject, TKey>> Bind<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
            IChangeSetAdaptor<TObject, TKey> updater)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (updater == null) throw new ArgumentNullException(nameof(updater));

            return Observable.Create<IChangeSet<TObject, TKey>>
                (observer =>
                {
                    var locker = new object();
                    var published = source.Synchronize(locker).Publish();

                    var adaptor = published.Subscribe(updates =>
                    {
                        try
                        {
                            updater.Adapt(updates);
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    },
                        observer.OnError, observer.OnCompleted);

                    var connected = published.Connect();
                    var subscriber = published.SubscribeSafe(observer);

                    return Disposable.Create(() =>
                    {
                        adaptor.Dispose();
                        subscriber.Dispose();
                        connected.Dispose();
                    });
                }
                );
        }

        /// <summary>
        /// Populate and maintain the specified reactive list from the source observable changeset
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="resetThreshold">The reset threshold before a reset event  on the target list is invoked</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        /// destination
        /// or
        /// target
        /// </exception>
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(
            this IObservable<ISortedChangeSet<TObject, TKey>> source, ReactiveList<TObject> target,
            int resetThreshold = 25)
        {
            if (target == null) throw new ArgumentNullException("target");

            var adaptor = new SortedReactiveListAdaptor<TObject, TKey>(target, resetThreshold);
            return source.Bind(adaptor);

        }

        /// <summary>
        /// Binds the results using the specified sorted changeset adaptor
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="updater">The updater.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        [Obsolete("ReactiveList has been deprecated by the ReactiveUI team.")]
        public static IObservable<ISortedChangeSet<TObject, TKey>> Bind<TObject, TKey>(
            this IObservable<ISortedChangeSet<TObject, TKey>> source,
            ISortedChangeSetAdaptor<TObject, TKey> updater)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (updater == null) throw new ArgumentNullException(nameof(updater));

            return Observable.Create<ISortedChangeSet<TObject, TKey>>
                (observer =>
                {
                    var locker = new object();
                    var published = source.Synchronize(locker).Publish();

                    var adaptor = published.Subscribe(updates =>
                    {
                        try
                        {
                            updater.Adapt(updates);
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    },observer.OnError, observer.OnCompleted);

                    var connected = published.Connect();

                    var subscriber = published.SubscribeSafe(observer);

                    return Disposable.Create(() =>
                    {
                        adaptor.Dispose();
                        subscriber.Dispose();
                        connected.Dispose();
                    });
                }
                );
        }
    }
}
