// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Kernel;
#else

namespace DynamicData.Kernel;
#endif

/// <summary>
/// Provides members for the ParallelEx class.
/// </summary>
internal static class ParallelEx
{
    /// <summary>
    /// Executes the SelectParallel operation.
    /// </summary>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="selector">The selector value.</param>
    /// <param name="maximumThreads">The maximumThreads value.</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<IEnumerable<TDestination>> SelectParallel<TSource, TDestination>(this IEnumerable<TSource> source, Func<TSource, Task<TDestination>> selector, int maximumThreads = 5)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(selector);

        var semaphore = new SemaphoreSlim(maximumThreads);
        var tasks = new List<Task<TDestination>>();

        foreach (var item in source)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            tasks.Add(
                Task.Run(
                    async () =>
                    {
                        try
                        {
                            return await selector(item).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
        }

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
