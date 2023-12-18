using System;
using System.Collections.Generic;
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

    // Emits "parallel" number of values that add up to "count"
    private static IEnumerable<int> Distribute(int count, int parallel)
    {
        if (count <= parallel)
        {
            return Enumerable.Repeat(1, count);
        }

        if ((count % parallel) == 0)
        {
            return Enumerable.Repeat(count / parallel, parallel);
        }

        var num = count / parallel;
        return Enumerable.Repeat(num, parallel - 1).Append(count - (num * (parallel - 1)));
    }
}
