// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the OnItemRefreshed class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal static class OnItemRefreshed<T>
    where T : notnull
{
    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="refreshAction">The refreshAction value.</param>
    /// <returns>The result of the operation.</returns>
    public static IObservable<IChangeSet<T>> Create(
        IObservable<IChangeSet<T>> source,
        Action<T> refreshAction)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(refreshAction);

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
