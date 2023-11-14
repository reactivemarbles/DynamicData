// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class OnBeingRemoved<T>(IObservable<IChangeSet<T>> source, Action<T> callback, bool invokeOnUnsubscribe)
    where T : notnull
{
    private readonly Action<T> _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = new object();
                var items = new List<T>();
                var subscriber = _source.Synchronize(locker).Do(changes => RegisterForRemoval(items, changes), observer.OnError).SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        subscriber.Dispose();

                        if (invokeOnUnsubscribe)
                        {
                            items.ForEach(t => _callback(t));
                        }
                    });
            });

    private void RegisterForRemoval(IList<T> items, IChangeSet<T> changes)
    {
        foreach (var change in changes)
        {
            switch (change.Reason)
            {
                case ListChangeReason.Replace:
                    change.Item.Previous.IfHasValue(t => _callback(t));
                    break;

                case ListChangeReason.Remove:
                    _callback(change.Item.Current);
                    break;

                case ListChangeReason.RemoveRange:
                    change.Range.ForEach(_callback);
                    break;

                case ListChangeReason.Clear:
                    items.ForEach(_callback);
                    break;
            }
        }

        items.Clone(changes);
    }
}
