// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.List.Internal;

internal sealed class MergeMany<T, TDestination>(IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
    where T : notnull
{
    private readonly Func<T, IObservable<TDestination>> _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));

    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<TDestination> Run() => Observable.Create<TDestination>(
            observer =>
            {
                var counter = new SubscriptionCounter();
                var locker = new object();
                var disposable = _source.Concat(counter.DeferCleanup)
                                                .SubscribeMany(t =>
                                                {
                                                    counter.Added();
                                                    return _observableSelector(t).Synchronize(locker).Finally(() => counter.Finally()).Subscribe(observer.OnNext, _ => { }, () => { });
                                                })
                                                .Subscribe(_ => { }, observer.OnError, observer.OnCompleted);

                return new CompositeDisposable(disposable, counter);
            });

    private sealed class SubscriptionCounter : IDisposable
    {
        private readonly Subject<IChangeSet<T>> _subject = new();
        private int _subscriptionCount = 1;

        public IObservable<IChangeSet<T>> DeferCleanup => Observable.Defer(() =>
        {
            CheckCompleted();
            return _subject.AsObservable();
        });

        public void Added() => _ = Interlocked.Increment(ref _subscriptionCount);

        public void Finally() => CheckCompleted();

        public void Dispose() => _subject.Dispose();

        private void CheckCompleted()
        {
            if (Interlocked.Decrement(ref _subscriptionCount) == 0)
            {
                _subject.OnCompleted();
            }
        }
    }
}
