// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace DynamicData.Kernel;

internal static class ParallelEx
{
    public static async Task<IEnumerable<TDestination>> SelectParallel<TSource, TDestination>(this IEnumerable<TSource> source, Func<TSource, Task<TDestination>> selector, int maximumThreads = 5)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        selector.ThrowArgumentNullExceptionIfNull(nameof(selector));

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
