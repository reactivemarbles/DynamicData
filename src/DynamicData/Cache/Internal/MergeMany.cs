// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class MergeMany<TObject, TKey, TDestination>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<TDestination>> _observableSelector;
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public MergeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));
    }

    public MergeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
    {
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = (t, _) => observableSelector(t);
    }

    public IObservable<TDestination> Run() =>
        _source.OrchestrateMany<TObject, TKey, TDestination, TDestination>(
            (context, emitter) => new Orchestrator(context, emitter, _observableSelector));

    private sealed class Orchestrator(
            ICacheOrchestratorContext<TKey, TDestination> context,
            IObserver<TDestination> emitter,
            Func<TObject, TKey, IObservable<TDestination>> selector)
        : OrchestratorCacheChangeBase<TObject, TKey, TDestination, TDestination>(context, emitter)
    {
        public override void OnInner(TDestination value, TKey key) => Emitter.OnNext(value);

        protected override void OnItemAdded(TObject item, TKey key) => Context.Track(key, selector(item, key));

        protected override void OnItemRemoved(TObject item, TKey key) => Context.Track(key, null);
    }
}
