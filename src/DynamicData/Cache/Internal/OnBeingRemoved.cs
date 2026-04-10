// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class OnBeingRemoved<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> removeAction)
    where TObject : notnull
    where TKey : notnull
{
    private readonly Action<TObject, TKey> _removeAction = removeAction ?? throw new ArgumentNullException(nameof(removeAction));
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var locker = InternalEx.NewLock();
                var cache = new Cache<TObject, TKey>();

                var queue = new DeliveryQueue<DynamicData.Internal.Notification<IChangeSet<TObject, TKey>>>(locker, notification =>
                {
                    if (notification.HasValue)
                    {
                        var changes = notification.Value!;
                        RegisterForRemoval(changes, cache);
                        observer.OnNext(changes);
                    }
                    else if (notification.Error is not null)
                    {
                        observer.OnError(notification.Error);
                    }
                    else
                    {
                        observer.OnCompleted();
                    }

                    return !notification.IsTerminal;
                });

                var subscriber = _source.SynchronizeSafe(queue);

                return Disposable.Create(
                    () =>
                    {
                        subscriber.Dispose();

                        using (var readLock = queue.AcquireReadLock())
                        {
                            cache.KeyValues.ForEach(kvp => _removeAction(kvp.Value, kvp.Key));
                            cache.Clear();
                        }
                    });
            });

    private void RegisterForRemoval(IChangeSet<TObject, TKey> changes, Cache<TObject, TKey> cache)
    {
        changes.ForEach(
            change =>
            {
                switch (change.Reason)
                {
                    case ChangeReason.Remove:
                        // ReSharper disable once InconsistentlySynchronizedField
                        _removeAction(change.Current, change.Key);
                        break;
                }
            });
        cache.Clone(changes);
    }
}
