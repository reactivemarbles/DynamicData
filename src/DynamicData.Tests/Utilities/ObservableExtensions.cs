using System;
using System.Linq;
using System.Reactive.Linq;

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
}
