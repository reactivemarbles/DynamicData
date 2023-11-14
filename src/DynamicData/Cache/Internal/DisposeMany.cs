// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class DisposeMany<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction)
    where TObject : notnull
    where TKey : notnull
{
    private readonly Action<TObject> _removeAction = removeAction ?? throw new ArgumentNullException(nameof(removeAction));

    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var locker = new object();
                var cache = new Cache<TObject, TKey>();
                var subscriber = _source.Synchronize(locker).Do(changes => RegisterForRemoval(changes, cache), observer.OnError).SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        subscriber.Dispose();

                        lock (locker)
                        {
                            cache.Items.ForEach(t => _removeAction(t));
                            cache.Clear();
                        }
                    });
            });

    private void RegisterForRemoval(IChangeSet<TObject, TKey> changes, Cache<TObject, TKey> cache)
    {
        changes.ToConcreteType().ForEach(
            change =>
            {
                switch (change.Reason)
                {
                    case ChangeReason.Update:
                        // ReSharper disable once InconsistentlySynchronizedField
                        change.Previous.IfHasValue(t => _removeAction(t));
                        break;

                    case ChangeReason.Remove:
                        // ReSharper disable once InconsistentlySynchronizedField
                        _removeAction(change.Current);
                        break;
                }
            });
        cache.Clone(changes);
    }
}
