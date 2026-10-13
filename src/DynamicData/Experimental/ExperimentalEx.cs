// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Experimental;

/// <summary>
/// Experimental operator extensions.
/// </summary>
public static class ExperimentalEx
{
    /// <summary>
    /// Wraps the source cache, optimising it for watching individual updates.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <returns>The watcher.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>Delivered to every watch through <paramref name="scheduler"/>, the same way as changes. A watch started afterwards receives the current value, if there is one, and then fails.</description></item>
    /// <item><term>OnCompleted</term><description>Delivered to every watch through <paramref name="scheduler"/>, the same way as changes. A watch started afterwards receives the current value, if there is one, and then completes.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Disposing the watcher completes every watch.</para>
    /// </remarks>
    public static IWatcher<TObject, TKey> AsWatcher<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(source);

        return new Watcher<TObject, TKey>(source, scheduler ?? GlobalConfig.DefaultScheduler);
    }
}
