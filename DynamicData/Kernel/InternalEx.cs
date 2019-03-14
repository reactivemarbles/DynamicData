using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace DynamicData.Kernel
{
    /// <summary>
    /// 
    /// </summary>
    public static class InternalEx
    {
        /// <summary>
        /// Retries the with back off.
        /// </summary>
        /// <remarks>
        /// With big thanks.  I took this from 
        /// http://social.msdn.microsoft.com/Forums/en-US/af43b14e-fb00-42d4-8fb1-5c45862f7796/recursive-async-web-requestresponse-what-is-best-practice-3rd-try
        /// </remarks>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <typeparam name="TException">The type of the exception.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="backOffStrategy">The back off strategy.</param>
        /// <returns></returns>
        public static IObservable<TSource> RetryWithBackOff<TSource, TException>(this IObservable<TSource> source, Func<TException, int, TimeSpan?> backOffStrategy)
            where TException : Exception
        {
            IObservable<TSource> Retry(int failureCount) => source.Catch<TSource, TException>(error =>
            {
                TimeSpan? delay = backOffStrategy(error, failureCount);
                if (!delay.HasValue)
                    return Observable.Throw<TSource>(error);

                return Observable.Timer(delay.Value).SelectMany(Retry(failureCount + 1));
            });

            return Retry(0);
        }

        internal static IObservable<Unit> ToUnit<T>(this IObservable<T> source)
        {
            return source.Select(_ => Unit.Default);
        }

        internal static void OnNext(this ISubject<Unit> source)
        {
            source.OnNext(Unit.Default);
        }

        internal static IObservable<TResult> SelectTask<T, TResult>(this IObservable<T> source, Func<T, Task<TResult>> factory )
        {
            return source.Select(t =>
            {
                return Observable.FromAsync(() => factory(t)).Wait();
            });
        }




        /// <summary>
        /// Schedules a recurring action.
        /// </summary>
        /// <remarks>
        ///  I took this from 
        /// http://www.zerobugbuild.com/?p=259
        /// </remarks>
        /// <param name="scheduler">The scheduler.</param>
        /// <param name="interval">The interval.</param>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        public static IDisposable ScheduleRecurringAction(this IScheduler scheduler, TimeSpan interval, Action action)
        {
            return scheduler.Schedule(interval, scheduleNext =>
            {
                action();
                scheduleNext(interval);
            });
        }

        /// <summary>
        /// Schedules a recurring action.
        /// </summary>
        /// <remarks>
        ///  I took this from 
        /// 
        /// http://www.zerobugbuild.com/?p=259
        /// 
        /// and adapted it to receive 
        /// </remarks>
        /// <param name="scheduler">The scheduler.</param>
        /// <param name="interval">The interval.</param>
        /// <param name="action">The action.</param>
        /// <returns></returns>
        public static IDisposable ScheduleRecurringAction(this IScheduler scheduler, Func<TimeSpan> interval, Action action)
        {
            return scheduler.Schedule(interval(), scheduleNext =>
            {
                action();
                var next = interval();
                scheduleNext(next);
            });
        }

        internal static void Swap<TSwap>(ref TSwap t1, ref TSwap t2)
        {
            TSwap temp = t1;
            t1 = t2;
            t2 = temp;
        }
    }
}
