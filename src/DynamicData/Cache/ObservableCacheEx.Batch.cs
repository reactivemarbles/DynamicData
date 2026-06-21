// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Collects changesets emitted within a time window and merges them into a single changeset.
    /// Uses Rx's <c>Buffer</c> operator followed by <see cref="FlattenBufferResult{TObject, TKey}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to batch.</param>
    /// <param name="timeSpan">The <see cref="TimeSpan"/> time window for batching.</param>
    /// <param name="scheduler">The scheduler for timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits merged changesets, one per time window.</returns>
    /// <remarks>
    /// <para>
    /// All changesets received during the time window are concatenated into a single changeset.
    /// This is useful for reducing UI update frequency when the source emits many rapid changes.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Buffered and included in the merged changeset at the end of the time window.</description></item>
    /// <item><term>Update</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>Remove</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>Refresh</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>OnCompleted</term><description>Any remaining buffered changes are flushed, then completion is forwarded.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> The merged changeset may contain contradictory changes (e.g., Add then Remove for the same key). Downstream operators handle this correctly, but raw inspection of the changeset may be surprising.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="BufferInitial{TObject, TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> Batch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan timeSpan, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Buffer(timeSpan, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult();
    }
}
