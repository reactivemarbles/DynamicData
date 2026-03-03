// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal static class OnItemRemoved<T>
    where T : notnull
{
    public static IObservable<IChangeSet<T>> Create(
        IObservable<IChangeSet<T>> source,
        Action<T> removeAction,
        bool invokeOnUnsubscribe)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        removeAction.ThrowArgumentNullExceptionIfNull(nameof(removeAction));

        var removalProcessor = source.Do(changeSet =>
        {
            foreach (var change in changeSet)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Clear:
                    case ListChangeReason.RemoveRange:
                        foreach (var item in change.Range)
                            removeAction.Invoke(item);
                        break;

                    case ListChangeReason.Remove:
                        removeAction.Invoke(change.Item.Current);
                        break;

                    case ListChangeReason.Replace:
                        removeAction.Invoke(change.Item.Previous.Value);
                        break;
                }
            }
        });

        return invokeOnUnsubscribe
            ? Observable.Create<IChangeSet<T>>(observer =>
            {
                var items = new List<T>();

                return removalProcessor
                    .Do(changeSet => items.Clone(changeSet))
                    .Finally(() =>
                    {
                        foreach (var item in items)
                            removeAction.Invoke(item);
                    })
                    .SubscribeSafe(observer);
            })
            : removalProcessor;
    }
}
