// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

internal sealed class MergeManyItems<TObject, TKey, TDestination>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<TDestination>> _observableSelector;

    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        _source = source;
        _observableSelector = observableSelector;
    }

    public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        _source = source;
        _observableSelector = (t, _) => observableSelector(t);
    }

    public IObservable<ItemWithValue<TObject, TDestination>> Run() => Observable.Create<ItemWithValue<TObject, TDestination>>(observer => _source.SubscribeMany((t, v) => _observableSelector(t, v).Select(z => new ItemWithValue<TObject, TDestination>(t, z)).SubscribeSafe(observer)).Subscribe());
}
