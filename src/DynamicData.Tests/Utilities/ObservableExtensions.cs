using System;
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
}
