// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Kernel;

/// <summary>
/// Extensions associated with times and intervals.
/// </summary>
public static class InternalEx
{
    /// <summary>
    /// Retries the with back off.
    /// </summary>
    /// <remarks>
    /// With big thanks.  I took this from
    /// https://social.msdn.microsoft.com/Forums/en-US/af43b14e-fb00-42d4-8fb1-5c45862f7796/recursive-async-web-requestresponse-what-is-best-practice-3rd-try.
    /// </remarks>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TException">The type of the exception.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="backOffStrategy">The back off strategy.</param>
    /// <returns>An observable which will emit the value.</returns>
    public static IObservable<TSource> RetryWithBackOff<TSource, TException>(this IObservable<TSource> source, Func<TException, int, TimeSpan?> backOffStrategy)
        where TException : Exception
    {
        IObservable<TSource> Retry(int failureCount) =>
            source.Catch<TSource, TException>(
                error =>
                {
                    var delay = backOffStrategy(error, failureCount);
                    if (!delay.HasValue)
                    {
                        return Observable.Throw<TSource>(error);
                    }

                    return Observable.Timer(delay.Value).SelectMany(Retry(failureCount + 1));
                });

        return Retry(0);
    }

    /// <summary>
    /// Schedules a recurring action.
    /// </summary>
    /// <remarks>
    ///  I took this from
    /// https://www.zerobugbuild.com/?p=259.
    /// </remarks>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="interval">The interval.</param>
    /// <param name="action">The action.</param>
    /// <returns>A disposable that will stop the schedule.</returns>
    public static IDisposable ScheduleRecurringAction(this IScheduler scheduler, TimeSpan interval, Action action) => scheduler.Schedule(
            interval,
            scheduleNext =>
            {
                action();
                scheduleNext(interval);
            });

    /// <summary>
    /// Schedules a recurring action.
    /// </summary>
    /// <remarks>
    /// <para> I took this from.</para>
    /// <para>https://www.zerobugbuild.com/?p=259.</para>
    /// <para>and adapted it to receive.</para>
    /// </remarks>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="interval">The interval.</param>
    /// <param name="action">The action.</param>
    /// <returns>A disposable that will stop the schedule.</returns>
    public static IDisposable ScheduleRecurringAction(this IScheduler scheduler, Func<TimeSpan> interval, Action action)
    {
        interval.ThrowArgumentNullExceptionIfNull(nameof(interval));

        return scheduler.Schedule(
            interval(),
            scheduleNext =>
            {
                action();
                var next = interval();
                scheduleNext(next);
            });
    }

    internal static void OnNext(this ISubject<Unit> source) => source.OnNext(Unit.Default);

    internal static void Swap<TSwap>(ref TSwap t1, ref TSwap t2) => (t2, t1) = (t1, t2);

    internal static IObservable<Unit> ToUnit<T>(this IObservable<T> source) => source.Select(_ => Unit.Default);

    /// <summary>
    /// Observable.Return without the memory leak.
    /// </summary>
    internal static IObservable<T> Return<T>(Func<T> source) =>
        Observable.Create<T>(o =>
        {
            o.OnNext(source());
            o.OnCompleted();
            return () => { };
        });
}
