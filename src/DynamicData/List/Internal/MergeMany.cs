// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class MergeMany<T, TDestination>
    where T : notnull
{
    private readonly Func<T, IObservable<TDestination>> _observableSelector;

    private readonly IObservable<IChangeSet<T>> _source;

    public MergeMany(IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));
    }

    public IObservable<TDestination> Run()
    {
        return Observable.Create<TDestination>(
            observer =>
            {
                var locker = new object();
                return _source.SubscribeMany(t => _observableSelector(t).Synchronize(locker).Subscribe(observer.OnNext)).Subscribe(_ => { }, observer.OnError);
            });
    }
}
