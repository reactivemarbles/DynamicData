namespace DynamicData.Tests.Utilities;

public class UnsynchronizedNotificationException<T>
    : Exception
{
    public UnsynchronizedNotificationException()
        : base("Unsynchronized notification received: Another notification is already being processed")
    { }

    public required System.Reactive.Notification<T> IncomingNotification { get; init; }

    public required System.Reactive.Notification<T> PriorNotification { get; init; }
}
