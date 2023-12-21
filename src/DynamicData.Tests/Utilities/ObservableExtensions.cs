using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;

namespace DynamicData.Tests.Utilities;

internal static class ObservableExtensions
{
    /// <summary>
    /// Forces the given observable to fail after the specified number events if an exception is provided.
    /// </summary>
    /// <typeparam name="T">Observable type.</typeparam>
    /// <param name="source">Source Observable.</param>
    /// <param name="count">Number of events before failing.</param>
    /// <param name="e">Exception to fail with.</param>
    /// <returns>The new Observable.</returns>
    public static IObservable<T> ForceFail<T>(this IObservable<T> source, int count, Exception? e) =>
        e is not null
            ? source.Take(count).Concat(Observable.Throw<T>(e))
            : source;

    /// <summary>
    /// Creates an observable that parallelizes some given work by taking the source observable, creates multiple subscriptions, limiting each to a certain number of values, and 
    /// attaching some work to be done in parallel to each before merging them back together.
    /// </summary>
    /// <typeparam name="T">Input Observable type.</typeparam>
    /// <typeparam name="U">Output Observable type.</typeparam>
    /// <param name="source">Source Observable.</param>
    /// <param name="count">Total number of values to process.</param>
    /// <param name="parallel">Total number of subscriptions to create.</param>
    /// <param name="fnAttachParallelWork">Function to append work to be done before the merging.</param>
    /// <returns>An Observable that contains the values resulting from the work performed.</returns>
    public static IObservable<U> Parallelize<T, U>(this IObservable<T> source, int count, int parallel, Func<IObservable<T>, IObservable<U>> fnAttachParallelWork) =>
        Observable.Merge(Distribute(count, parallel).Select(n => fnAttachParallelWork(source.Take(n))));

    /// <summary>
    /// Creates an observable that parallelizes some given work by taking the source observable, creates multiple subscriptions, limiting each to a certain number of values, and 
    /// merging them back together.
    /// </summary>
    /// <typeparam name="T">Observable type.</typeparam>
    /// <param name="source">Source Observable.</param>
    /// <param name="count">Total number of values to process.</param>
    /// <param name="parallel">Total number of subscriptions to create.</param>
    /// <returns>An Observable that contains the values resulting from the merged sequences.</returns>
    public static IObservable<T> Parallelize<T>(this IObservable<T> source, int count, int parallel) =>
        Observable.Merge(Distribute(count, parallel).Select(n => source.Take(n)));

    public static IObservable<T> ValidateSynchronization<T>(this IObservable<T> source)
        // Using Raw observable and observer classes to bypass normal RX safeguards, which prevent out-of-sequence notifications.
        // This allows the operator to be combined with TestableObserver, for correctness-testing of operators.
        => RawAnonymousObservable.Create<T>(observer =>
        {
            var inFlightNotification = null as Notification<T>;
            var synchronizationGate = new object();

            // Not using .Do() so we can track the *entire* in-flight period of a notification, including all synchronous downstream processing.
            return source.SubscribeSafe(RawAnonymousObserver.Create<T>(
                onNext: value => ProcessIncomingNotification(Notification.CreateOnNext(value)),
                onError: error => ProcessIncomingNotification(Notification.CreateOnError<T>(error)),
                onCompleted: () => ProcessIncomingNotification(Notification.CreateOnCompleted<T>())));

            void ProcessIncomingNotification(Notification<T> incomingNotification)
            {
                try
                {
                    var priorNotification = Interlocked.Exchange(ref inFlightNotification, incomingNotification);
                    if (priorNotification is not null)
                        throw new UnsynchronizedNotificationException<T>()
                        {
                            IncomingNotification = incomingNotification,
                            PriorNotification = priorNotification
                        };

                    lock (synchronizationGate)
                    {
                        switch(incomingNotification.Kind)
                        {
                            case NotificationKind.OnNext:
                                observer.OnNext(incomingNotification.Value);
                                break;

                            case NotificationKind.OnError:
                                observer.OnError(incomingNotification.Exception!);
                                break;

                            case NotificationKind.OnCompleted:
                                observer.OnCompleted();
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (synchronizationGate)
                    {
                        observer.OnError(ex);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref inFlightNotification, null);
                }
            }
        });

    // Emits "parallel" number of values that add up to "count"
    private static IEnumerable<int> Distribute(int count, int parallel) =>
        (count, parallel, count / parallel) switch
        {
            // Not enough count for each parallel, so just return as many as needed
            (int c, int p, _) when c <= p => Enumerable.Repeat(1, c),

            // Divides equally, so return the ratio for the parallel quantity
            (int c, int p, int ratio) when (c % p) == 0 => Enumerable.Repeat(ratio, p),

            // Doesn't divide equally, so return the ratio for the parallel quantity, and the remainder for the last one
            (int c, int p, int ratio) => Enumerable.Repeat(ratio, p - 1).Append(c - (ratio * (p - 1))),
        };
}
