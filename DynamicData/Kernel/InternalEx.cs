using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

namespace DynamicData.Kernel
{
    public static class InternalEx
    {
        internal static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> source)
        {
            return source ?? Enumerable.Empty<T>();
        }

        internal static HashSet<T> ToHashSet<T>(this IEnumerable<T> source )
        {
            return new HashSet<T>(source);
        }

        internal static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
        }

        internal static void ForEach<TObject>(this IEnumerable<TObject> source, Action<TObject, int> action)
        {
            int i = 0;
            foreach (var item in source)
            {
                action(item,i);
                i++;
            }
        }
        
        //internal static void ToConsoleWithThreadId(this string message, TimeSpan timeSpan)
        //{
        //    Console.WriteLine("{0}. Thread={1} @ {2}ms".FormatWith(message,
        //        Thread.CurrentThread.ManagedThreadId, timeSpan.TotalMilliseconds));
        //}

        //internal static void ToConsoleWithThreadId(this string message)
        //{
        //    var time = DateTime.Now;
        //    Console.WriteLine("{0}. Thread={1} @ {2}.{3}ms".FormatWith(message, 
        //        Thread.CurrentThread.ManagedThreadId,time.Second,time.Millisecond));
        //}

        internal static String FormatWith(this String @this, params object[] parameters)
        {
            return string.Format(@this, parameters);
        }
        

        public static IObservable<string> ObserveChanges<T>(this T source)
            where T: INotifyPropertyChanged
        {

            return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>
                (
                    handler => source.PropertyChanged += handler,
                    handler => source.PropertyChanged -= handler
                )
                .Select(x => x.EventArgs.PropertyName);
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
        public static IObservable<TSource> RetryWithBackOff<TSource, TException>(this IObservable<TSource> source, Func<TException , int , TimeSpan?> backOffStrategy)
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
