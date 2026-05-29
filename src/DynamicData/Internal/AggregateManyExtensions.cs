// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Internal;

/// <summary>
/// Provides the <see cref="AggregateMany{TParent, TKey, TChild, TResult}"/> operator, a delegate-driven entry point to the
/// <see cref="CacheParentSubscription{TParent, TKey, TChild, TObserver}"/> batching machinery that manages per-key
/// child subscriptions and coalesces parent and child notifications into a single downstream emission per drain cycle.
/// </summary>
internal static class AggregateManyExtensions
{
    /// <summary>
    /// Subscribes to a parent changeset and manages per-key child subscriptions, aggregating parent
    /// and child notifications into a single downstream emission per drain cycle via <paramref name="tryEmit"/>.
    /// </summary>
    /// <typeparam name="TParent">Type of the parent changeset items.</typeparam>
    /// <typeparam name="TKey">Type of the parent changeset key.</typeparam>
    /// <typeparam name="TChild">Type of the per-key child observable values.</typeparam>
    /// <typeparam name="TResult">Type of the downstream observer notifications.</typeparam>
    /// <param name="source">The parent changeset source.</param>
    /// <param name="onParent">
    /// Receives each parent changeset and a <c>setChild</c> callback that adds, replaces,
    /// or removes the child subscription for a key. Pass a non-<see langword="null"/> observable
    /// to add or replace; pass <see langword="null"/> to remove. The callback automatically
    /// routes the child observable through the shared delivery queue so callers do not
    /// need to synchronize it themselves.
    /// </param>
    /// <param name="onChild">Receives each value emitted by a child observable, paired with its parent key.</param>
    /// <param name="tryEmit">Invoked once per drain cycle to flush accumulated state to the observer.</param>
    /// <returns>The aggregated observable stream.</returns>
    public static IObservable<TResult> AggregateMany<TParent, TKey, TChild, TResult>(
            this IObservable<IChangeSet<TParent, TKey>> source,
            Action<IChangeSet<TParent, TKey>, Action<TKey, IObservable<TChild>?>> onParent,
            Action<TChild, TKey> onChild,
            Action<IObserver<TResult>> tryEmit)
        where TParent : notnull
        where TKey : notnull
        where TChild : notnull =>
        Observable.Create<TResult>(observer => new DelegatedAggregator<TParent, TKey, TChild, TResult>(source, observer, onParent, onChild, tryEmit));

    private sealed class DelegatedAggregator<TParent, TKey, TChild, TResult>
        : CacheParentSubscription<TParent, TKey, TChild, TResult>
        where TParent : notnull
        where TKey : notnull
        where TChild : notnull
    {
        private readonly Action<IChangeSet<TParent, TKey>, Action<TKey, IObservable<TChild>?>> _onParent;
        private readonly Action<TChild, TKey> _onChild;
        private readonly Action<IObserver<TResult>> _tryEmit;

        public DelegatedAggregator(
                IObservable<IChangeSet<TParent, TKey>> source,
                IObserver<TResult> observer,
                Action<IChangeSet<TParent, TKey>, Action<TKey, IObservable<TChild>?>> onParent,
                Action<TChild, TKey> onChild,
                Action<IObserver<TResult>> tryEmit)
            : base(observer)
        {
            _onParent = onParent;
            _onChild = onChild;
            _tryEmit = tryEmit;
            CreateParentSubscription(source);
        }

        protected override void ParentOnNext(IChangeSet<TParent, TKey> changes) => _onParent(changes, SetChild);

        protected override void ChildOnNext(TChild child, TKey parentKey) => _onChild(child, parentKey);

        protected override void EmitChanges(IObserver<TResult> observer) => _tryEmit(observer);

        private void SetChild(TKey key, IObservable<TChild>? observable)
        {
            if (observable is null)
            {
                RemoveChildSubscription(key);
            }
            else
            {
                AddChildSubscription(MakeChildObservable(observable), key);
            }
        }
    }
}
