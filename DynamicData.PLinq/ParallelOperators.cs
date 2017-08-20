using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Cache.Internal;
using DynamicData.Kernel;

// ReSharper disable once CheckNamespace

namespace DynamicData.PLinq
{
    /// <summary>
    /// PLinq operators or Net4 and Net45 only
    /// </summary>
    public static class ParallelOperators
    {
        #region Subscribe Many

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function</param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                          Func<TObject, IDisposable> subscriptionFactory, 
                                                                                          ParallelisationOptions parallelisationOptions)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));
            if (parallelisationOptions == null) throw new ArgumentNullException(nameof(parallelisationOptions));

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                {
                    var published = source.Publish();
                    var subscriptions = published
                        .Transform((t, k) => new SubscriptionContainer<TObject, TKey>(t, k,(a,b)=> subscriptionFactory(a)), parallelisationOptions)
                        .DisposeMany()
                        .Subscribe();

                    var result = published.SubscribeSafe(observer);
                    var connected = published.Connect();

                    return new CompositeDisposable(subscriptions, connected, result);
                });
        }

        /// <summary>
        /// Subscribes to each item when it is added to the stream and unsubcribes when it is removed.  All items will be unsubscribed when the stream is disposed
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="subscriptionFactory">The subsription function</param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// subscriptionFactory</exception>
        /// <remarks>
        /// Subscribes to each item when it is added or updates and unsubcribes when it is removed
        /// </remarks>
        public static IObservable<IChangeSet<TObject, TKey>> SubscribeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source,
                                                                                          Func<TObject, TKey, IDisposable> subscriptionFactory, ParallelisationOptions parallelisationOptions)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subscriptionFactory == null) throw new ArgumentNullException(nameof(subscriptionFactory));
            if (parallelisationOptions == null) throw new ArgumentNullException(nameof(parallelisationOptions));

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                {
                    var published = source.Publish();
                    var subscriptions = published
                        .Transform((t, k) => new SubscriptionContainer<TObject, TKey>(t, k, subscriptionFactory), parallelisationOptions)
                        .DisposeMany()
                        .Subscribe();

                    var result = published.SubscribeSafe(observer);
                    var connected = published.Connect();

                    return Disposable.Create(() =>
                    {
                        connected.Dispose();
                        subscriptions.Dispose();
                        result.Dispose();
                    });
                });
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
        /// <param name="parallelisationOptions">The parallelisation options to be used on the transforms</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, TKey, TDestination> transformFactory,
                                                                                                         ParallelisationOptions parallelisationOptions)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (parallelisationOptions == null) throw new ArgumentNullException(nameof(parallelisationOptions));

            return Observable.Create<IChangeSet<TDestination, TKey>>(
                observer =>
                {
                    var transformer = new PlinqTransformer<TDestination, TSource, TKey>(parallelisationOptions, null);
                    return source
                        .Select(updates => transformer.Transform(updates, transformFactory))
                        .NotEmpty()
                        .Finally(observer.OnCompleted)
                        .SubscribeSafe(observer);
                });
        }

        /// <summary>
        /// Projects each update item to a new form using the specified transform function
        /// </summary>
        /// <typeparam name="TDestination">The type of the destination.</typeparam>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="transformFactory">The transform factory.</param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> Transform<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                         Func<TSource, TDestination> transformFactory,
                                                                                                         ParallelisationOptions parallelisationOptions)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (parallelisationOptions == null) throw new ArgumentNullException(nameof(parallelisationOptions));

            return Observable.Create<IChangeSet<TDestination, TKey>>(
                observer =>
                {
                    var transformer = new PlinqTransformer<TDestination, TSource, TKey>(parallelisationOptions, null);
                    return source
                        .Select(updates => transformer.Transform(updates, transformFactory))
                        .NotEmpty()
                        .SubscribeSafe(observer);
                });
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
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.
        ///  If not specified the stream will terminate as per rx convention.
        /// </param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                             Func<TSource, TDestination> transformFactory,
                                                                                                             Action<Error<TSource, TKey>> errorHandler,
                                                                                                             ParallelisationOptions parallelisationOptions)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));
            if (parallelisationOptions == null) throw new ArgumentNullException(nameof(parallelisationOptions));

            return Observable.Create<IChangeSet<TDestination, TKey>>(
                observer =>
                {
                    var transformer = new PlinqTransformer<TDestination, TSource, TKey>(parallelisationOptions, errorHandler);
                    return source
                        .Select(updates => transformer.Transform(updates, transformFactory))
                        .NotEmpty()
                        .SubscribeSafe(observer);
                });
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
        /// <param name="errorHandler">Provides the option to safely handle errors without killing the stream.
        ///  If not specified the stream will terminate as per rx convention.
        /// </param>
        /// <param name="parallelisationOptions">The parallelisation options to be used on the transforms</param>
        /// <returns>
        /// A transformed update collection
        /// </returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// transformFactory</exception>
        public static IObservable<IChangeSet<TDestination, TKey>> TransformSafe<TDestination, TSource, TKey>(this IObservable<IChangeSet<TSource, TKey>> source,
                                                                                                             Func<TSource, TKey, TDestination> transformFactory,
                                                                                                             Action<Error<TSource, TKey>> errorHandler,
                                                                                                             ParallelisationOptions parallelisationOptions)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (transformFactory == null) throw new ArgumentNullException(nameof(transformFactory));
            if (errorHandler == null) throw new ArgumentNullException(nameof(errorHandler));

            return Observable.Create<IChangeSet<TDestination, TKey>>(
                observer =>
                {
                    var transformer = new PlinqTransformer<TDestination, TSource, TKey>(parallelisationOptions, errorHandler);
                    return source
                        .Select(updates => transformer.Transform(updates, transformFactory))
                        .NotEmpty()
                        .SubscribeSafe(observer);
                });
        }

        #endregion

        #region Filter

        /// <summary>
        /// Filters the stream using the specified predicate
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="filter">The filter.</param>
        /// <param name="parallelisationOptions">The parallelisation options.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> Filter<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, bool> filter, ParallelisationOptions parallelisationOptions)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (filter == null)
                return source;

            return Observable.Create<IChangeSet<TObject, TKey>>(
                observer =>
                {
                    var filterer = new PLinqFilteredUpdater<TObject, TKey>(filter, parallelisationOptions);
                    return source
                        .Select(filterer.Update)
                        .NotEmpty()
                        .SubscribeSafe(observer);
                });
        }

        #endregion
    }
}
