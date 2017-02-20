using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DynamicData.Annotations;

namespace DynamicData.Kernel
{
    /// <summary>
    /// 
    /// </summary>
    public static class InternalEx
    {
        [StringFormatMethod("parameters")]
        internal static string FormatWith(this string source, params object[] parameters)
        {
            return string.Format(source, parameters);
        }

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
            Func<int, IObservable<TSource>> retry = null;

            retry = (failureCount) => source.Catch<TSource, TException>(error =>
            {
                TimeSpan? delay = backOffStrategy(error, failureCount);
                if (!delay.HasValue)
                    return Observable.Throw<TSource>(error);

                return Observable.Timer(delay.Value).SelectMany(retry(failureCount + 1));
            });

            return retry(0);
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
    }
}
