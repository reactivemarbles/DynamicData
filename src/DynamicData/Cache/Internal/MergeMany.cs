// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Internal;

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
                var counter = new SubscriptionCounter();
                var locker = new object();
                var disposable = _source.Concat(counter.DeferCleanup)
                                                .SubscribeMany((t, key) =>
                                                {
                                                    counter.Added();
                                                    return _observableSelector(t, key).Synchronize(locker).Finally(() => counter.Finally()).Subscribe(observer.OnNext, static _ => { });
                                                })
                                                .SubscribeSafe(observer.OnError, observer.OnCompleted);

                return new CompositeDisposable(disposable, counter);
            });

    private sealed class SubscriptionCounter : IDisposable
    {
        private readonly Subject<IChangeSet<TObject, TKey>> _subject = new();
        private int _subscriptionCount = 1;

        public IObservable<IChangeSet<TObject, TKey>> DeferCleanup => Observable.Defer(() =>
        {
            CheckCompleted();
            return _subject.AsObservable();
        });

        public void Added() => _ = Interlocked.Increment(ref _subscriptionCount);

        public void Finally() => CheckCompleted();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _subscriptionCount, 0) != 0)
            {
                _subject.OnCompleted();
            }

            _subject.Dispose();
        }

        private void CheckCompleted()
        {
            if (Interlocked.Decrement(ref _subscriptionCount) == 0)
            {
                _subject.OnCompleted();
            }
        }
    }
}
