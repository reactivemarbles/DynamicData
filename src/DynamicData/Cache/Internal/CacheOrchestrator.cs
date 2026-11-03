// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Base class for the operator logic driven by a <see cref="CacheOrchestration{TSource, TKey, TInner, TResult}"/>.
/// The orchestration builds a fresh instance for every subscription, so per-subscription state can live in fields.
/// </summary>
/// <remarks>
/// Every hook is invoked by the orchestration's delivery queue, so no two hooks ever run at the same time and none
/// of them runs under a lock. A hook may call <see cref="Emitter"/> directly.
/// </remarks>
/// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
/// <typeparam name="TKey">Type of the source changeset key.</typeparam>
/// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
/// <typeparam name="TResult">Type delivered downstream.</typeparam>
/// <param name="context">The orchestration driving this instance, which manages the per-key inner subscriptions.</param>
/// <param name="emitter">The downstream observer.</param>
internal abstract class CacheOrchestrator<TSource, TKey, TInner, TResult>(CacheOrchestration<TSource, TKey, TInner, TResult> context, IObserver<TResult> emitter)
    where TSource : notnull
    where TKey : notnull
    where TInner : notnull
{
    /// <summary>Gets the orchestration driving this instance, used to track and untrack per-key inner observables.</summary>
    protected CacheOrchestration<TSource, TKey, TInner, TResult> Context => context;

    /// <summary>Gets the downstream observer.</summary>
    protected IObserver<TResult> Emitter => emitter;

    /// <summary>
    /// Invoked for each source changeset. The default implementation dispatches each change to the matching
    /// <c>OnItem*</c> hook.
    /// </summary>
    /// <param name="changes">The source changeset.</param>
    public virtual void OnSourceNext(IChangeSet<TSource, TKey> changes)
    {
        foreach (var change in changes.ToConcreteType())
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    OnItemAdded(change.Current, change.Key);
                    break;

                case ChangeReason.Update:
                    OnItemUpdated(change.Current, change.Previous.Value, change.Key);
                    break;

                case ChangeReason.Remove:
                    OnItemRemoved(change.Current, change.Key);
                    break;

                case ChangeReason.Refresh:
                    OnItemRefreshed(change.Current, change.Key);
                    break;
            }
        }
    }

    /// <summary>Invoked for each value emitted by a tracked inner observable.</summary>
    /// <param name="value">The value emitted by the inner observable.</param>
    /// <param name="key">The source key the inner observable was tracked against.</param>
    public abstract void OnItemSourceNext(TInner value, TKey key);

    /// <summary>
    /// Invoked when a delivery frame ends. A frame is one upstream notification plus everything delivered
    /// synchronously beneath it on the same thread, so this is where an operator that accumulates changes emits
    /// them as a single changeset. Another thread's notifications get a frame of their own.
    /// </summary>
    /// <param name="isFinal">
    /// <see langword="true"/> when the source and every tracked inner observable have completed, so the downstream
    /// observer completes after this call returns. Anything held back for later, such as a buffered batch waiting on
    /// a timer, has to be emitted now.
    /// </param>
    public virtual void OnFrameEnded(bool isFinal)
    {
    }

    /// <summary>Invoked by <see cref="OnSourceNext"/> for each <see cref="ChangeReason.Add"/>.</summary>
    /// <param name="item">The added item.</param>
    /// <param name="key">The key of the added item.</param>
    protected virtual void OnItemAdded(TSource item, TKey key)
    {
    }

    /// <summary>Invoked by <see cref="OnSourceNext"/> for each <see cref="ChangeReason.Update"/>. Defaults to <see cref="OnItemAdded"/>.</summary>
    /// <param name="current">The updated item.</param>
    /// <param name="previous">The item it replaced.</param>
    /// <param name="key">The key of the updated item.</param>
    protected virtual void OnItemUpdated(TSource current, TSource previous, TKey key) => OnItemAdded(current, key);

    /// <summary>Invoked by <see cref="OnSourceNext"/> for each <see cref="ChangeReason.Remove"/>.</summary>
    /// <param name="item">The removed item.</param>
    /// <param name="key">The key of the removed item.</param>
    protected virtual void OnItemRemoved(TSource item, TKey key)
    {
    }

    /// <summary>Invoked by <see cref="OnSourceNext"/> for each <see cref="ChangeReason.Refresh"/>.</summary>
    /// <param name="item">The refreshed item.</param>
    /// <param name="key">The key of the refreshed item.</param>
    protected virtual void OnItemRefreshed(TSource item, TKey key)
    {
    }
}
