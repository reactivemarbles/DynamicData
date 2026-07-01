// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the OnItemAdded class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal static class OnItemAdded<T>
    where T : notnull
{
    /// <summary>
    /// Executes the Create operation.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="addAction">The addAction value.</param>
    /// <returns>The result of the operation.</returns>
    public static IObservable<IChangeSet<T>> Create(
        IObservable<IChangeSet<T>> source,
        Action<T> addAction)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(addAction);

        return source.Do(changeSet =>
        {
            foreach (var change in changeSet)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        addAction.Invoke(change.Item.Current);
                        break;

                    case ListChangeReason.AddRange:
                        foreach (var item in change.Range)
                            addAction.Invoke(item);
                        break;

                    case ListChangeReason.Replace:
                        addAction.Invoke(change.Item.Current);
                        break;
                }
            }
        });
    }
}
