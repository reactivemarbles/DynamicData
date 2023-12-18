// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class SizeExpirer<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly int _size;

    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public SizeExpirer(IObservable<IChangeSet<TObject, TKey>> source, int size)
    {
        if (size <= 0)
        {
            throw new ArgumentException("Size limit must be greater than zero");
        }

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _size = size;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var sizeLimiter = new SizeLimiter<TObject, TKey>(_size);
                var root = new IntermediateCache<TObject, TKey>(_source);

                var subscriber = root.Connect().Transform((t, v) => new ExpirableItem<TObject, TKey>(t, v, DateTime.Now)).Select(
                    changes =>
                    {
                        var result = sizeLimiter.Change(changes);

                        var removes = result.Where(c => c.Reason == ChangeReason.Remove);
                        root.Edit(updater => removes.ForEach(c => updater.Remove(c.Key)));
                        return result;
                    }).Finally(observer.OnCompleted).SubscribeSafe(observer);

                return Disposable.Create(
                    () =>
                    {
                        subscriber.Dispose();
                        root.Dispose();
                    });
            });
}
