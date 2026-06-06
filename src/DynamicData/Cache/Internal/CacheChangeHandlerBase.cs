// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Optional base for <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/> implementations
/// that prefer per-<see cref="ChangeReason"/> virtual hooks over decoding the changeset themselves.
/// The base implementation of <see cref="OnSourceChangeSet"/> walks the changeset and dispatches each
/// change to the corresponding protected virtual method, then invokes <see cref="OnChangeSetProcessed"/>
/// for end-of-changeset bookkeeping. Captures the <see cref="ICacheOrchestratorContext{TKey, TInner}"/>
/// in a <see cref="Context"/> property accessible to derived classes.
/// </summary>
/// <typeparam name="TSource">Type of items in the source changeset.</typeparam>
/// <typeparam name="TKey">Type of the source changeset key.</typeparam>
/// <typeparam name="TInner">Type of values emitted by the per-key inner observables.</typeparam>
/// <typeparam name="TResult">Type delivered downstream by <see cref="Emit"/>.</typeparam>
internal abstract class CacheChangeHandlerBase<TSource, TKey, TInner, TResult>
    : ICacheOrchestrator<TSource, TKey, TInner, TResult>
    where TSource : notnull
    where TKey : notnull
    where TInner : notnull
{
    private ICacheOrchestratorContext<TKey, TInner> _context = null!;

    protected ICacheOrchestratorContext<TKey, TInner> Context => _context;

    public void Initialize(ICacheOrchestratorContext<TKey, TInner> context) => _context = context;

    public void OnSourceChangeSet(IChangeSet<TSource, TKey> changes)
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

        OnChangeSetProcessed(changes);
    }

    public abstract void OnInner(TInner value, TKey key);

    public abstract void Emit(IObserver<TResult> observer);

    protected virtual void OnItemAdded(TSource item, TKey key)
    {
    }

    protected virtual void OnItemUpdated(TSource current, TSource previous, TKey key) => OnItemAdded(current, key);

    protected virtual void OnItemRemoved(TSource item, TKey key)
    {
    }

    protected virtual void OnItemRefreshed(TSource item, TKey key)
    {
    }

    protected virtual void OnChangeSetProcessed(IChangeSet<TSource, TKey> changes)
    {
    }
}
