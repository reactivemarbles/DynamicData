using System;
using System.Reactive;

namespace DynamicData.Tests.Utilities;

public class UnsynchronizedNotificationException<T>
    : Exception
{
    public UnsynchronizedNotificationException()
        : base("Unsynchronized notification received: Another notification is already being processed")
    { }

    public required Notification<T> IncomingNotification { get; init; }

    public required Notification<T> PriorNotification { get; init; }
}
