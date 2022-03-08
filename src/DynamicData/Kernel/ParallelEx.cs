// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DynamicData.Kernel;

internal static class ParallelEx
{
    [SuppressMessage("Design", "CA2000: Dispose SemaphoreSlim", Justification = "Captured in lambda, can cause problems.")]
    public static async Task<IEnumerable<TDestination>> SelectParallel<TSource, TDestination>(this IEnumerable<TSource> source, Func<TSource, Task<TDestination>> selector, int maximumThreads = 5)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (selector is null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

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
