using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData.Kernel
{
    internal static class ParallelEx
    {
        public static async Task<IEnumerable<TDestination>> SelectParallel<TSource, TDestination>(this IEnumerable<TSource> source, Func<TSource, Task<TDestination>> selector, int maximumThreads = 5)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var semaphore = new SemaphoreSlim(maximumThreads);
            var tasks = new List<Task<TDestination>>();

            foreach (var item in source)
            {
                await semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        return await selector(item);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
            return await Task.WhenAll(tasks);
        }

        public static async Task<IEnumerable<T>> SelectParallel<T>(this IEnumerable<Task<T>> source, int maximumThreads = 5)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var semaphore = new SemaphoreSlim(maximumThreads);
            var tasks = new List<Task<T>>();

            foreach (var task in source)
            {
                await semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        return await task;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
            return await Task.WhenAll(tasks);
        }
    }
}
