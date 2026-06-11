// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class MergeManyItems<TObject, TKey, TDestination>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<TDestination>> _observableSelector;
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));
    }

    public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
    {
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = (t, _) => observableSelector(t);
    }

    public IObservable<ItemWithValue<TObject, TDestination>> Run() =>
        _source.OrchestrateMany<TObject, TKey, (TObject Item, TDestination Value), ItemWithValue<TObject, TDestination>, Orchestrator>(
            (context, emitter) => new Orchestrator(context, emitter, _observableSelector));

    private sealed class Orchestrator(
            ICacheOrchestratorContext<TKey, (TObject Item, TDestination Value)> context,
            IObserver<ItemWithValue<TObject, TDestination>> emitter,
            Func<TObject, TKey, IObservable<TDestination>> selector)
        : OrchestratorCacheChangeBase<TObject, TKey, (TObject Item, TDestination Value), ItemWithValue<TObject, TDestination>>(context, emitter)
    {
        public override void OnInner((TObject Item, TDestination Value) value, TKey key) =>
            Emitter.OnNext(new ItemWithValue<TObject, TDestination>(value.Item, value.Value));

        protected override void OnItemAdded(TObject item, TKey key) =>
            Context.Track(key, selector(item, key).Select(value => (Item: item, Value: value)));

        protected override void OnItemRemoved(TObject item, TKey key) => Context.Untrack(key);
    }
}
