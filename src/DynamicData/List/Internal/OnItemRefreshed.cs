// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal static class OnItemRefreshed<T>
    where T : notnull
{
    public static IObservable<IChangeSet<T>> Create(
        IObservable<IChangeSet<T>> source,
        Action<T> refreshAction)
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        refreshAction.ThrowArgumentNullExceptionIfNull(nameof(refreshAction));

        return source.Do(changeSet =>
        {
            foreach (var change in changeSet)
            {
                if (change.Reason is ListChangeReason.Refresh)
                    refreshAction.Invoke(change.Item.Current);
            }
        });
    }
}
