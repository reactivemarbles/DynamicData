// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
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

    public IObservable<TDestination> Run() => Observable.Create<TDestination>(
            observer =>
            {
                var counter = new[] { 1 };
                var queue = new DeliveryQueue<TDestination>(observer);

                return new CompositeDisposable(_source
                    .Do(static _ => { }, static _ => { }, () => CheckCompleted(counter, queue))
                    .Concat(Observable.Never<IChangeSet<TObject, TKey>>())
                    .SubscribeMany((t, key) =>
                    {
                        Interlocked.Increment(ref counter[0]);
                        return _observableSelector(t, key)
                            .Finally(() => CheckCompleted(counter, queue))
                            .Subscribe(queue.OnNext, static _ => { });
                    })
                    .Subscribe(static _ => { }, observer.OnError), queue);
            });

    private static void CheckCompleted(int[] counter, DeliveryQueue<TDestination> queue)
    {
        if (Interlocked.Decrement(ref counter[0]) == 0)
        {
            queue.OnCompleted();
        }
    }
}
