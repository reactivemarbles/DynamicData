// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal class MergeMany<TObject, TKey, TDestination>
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
        if (observableSelector is null)
        {
            throw new ArgumentNullException(nameof(observableSelector));
        }

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = (t, _) => observableSelector(t);
    }

    public IObservable<TDestination> Run()
    {
        return Observable.Create<TDestination>(
            observer =>
            {
                var locker = new object();
                return _source.SubscribeMany((t, key) => _observableSelector(t, key).Synchronize(locker).Subscribe(observer.OnNext, _ => { }, () => { })).Subscribe(_ => { }, observer.OnError);
            });
    }
}
