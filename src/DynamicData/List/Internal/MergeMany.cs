// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.List.Internal
{
    internal sealed class MergeMany<T, TDestination>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly Func<T, IObservable<TDestination>> _observableSelector;

        public MergeMany([NotNull] IObservable<IChangeSet<T>> source,
                         [NotNull] Func<T, IObservable<TDestination>> observableSelector)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));
        }

        public IObservable<TDestination> Run()
        {
            return Observable.Create<TDestination>
                (
                    observer =>
                    {
                        var locker = new object();
                        return _source
                            .SubscribeMany(t => _observableSelector(t).Synchronize(locker).Subscribe(observer.OnNext))
                            .Subscribe(t => { }, observer.OnError);
                    });
        }
    }
}
