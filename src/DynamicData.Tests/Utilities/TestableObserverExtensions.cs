using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;

using FluentAssertions;

using Microsoft.Reactive.Testing;

namespace DynamicData.Tests.Utilities;

internal static class TestableObserverExtensions
{
    public static IEnumerable<Recorded<Notification<T>>> EnumerateInvalidNotifications<T>(this ITestableObserver<T> observer)
        => observer.Messages
            .SkipWhile(message => message.Value.Kind is NotificationKind.OnNext)
            .Skip(1);

    public static IEnumerable<T> EnumerateRecordedValues<T>(this ITestableObserver<T> observer)
        => observer.Messages
            .TakeWhile(message => message.Value.Kind is NotificationKind.OnNext)
            .Select(message => message.Value.Value);

    public static Exception? TryGetRecordedError<T>(this ITestableObserver<T> observer)
        => observer.Messages
            .Where(message => message.Value.Kind is NotificationKind.OnError)
            .Select(message => message.Value.Exception)
            .FirstOrDefault();

    public static bool TryGetRecordedCompletion<T>(this ITestableObserver<T> observer)
        => observer.Messages
            .Where(message => message.Value.Kind is NotificationKind.OnCompleted)
            .Any();
}
