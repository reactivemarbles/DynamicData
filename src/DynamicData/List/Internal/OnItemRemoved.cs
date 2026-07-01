// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the OnItemRemoved class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal static class OnItemRemoved<T>
    where T : notnull
{
    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="removeAction">The removeAction value.</param>
    /// <param name="invokeOnUnsubscribe">The invokeOnUnsubscribe value.</param>
    /// <returns>The result of the operation.</returns>
    public static IObservable<IChangeSet<T>> Create(
        IObservable<IChangeSet<T>> source,
        Action<T> removeAction,
        bool invokeOnUnsubscribe)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(removeAction);

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
