// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Executes the OnChangeAction operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="predicate">The predicate value.</param>
    /// <param name="changeAction">The changeAction value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IChangeSet<TObject, TKey>> OnChangeAction<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Predicate<Change<TObject, TKey>> predicate, Action<Change<TObject, TKey>> changeAction)
        where TObject : notnull
        where TKey : notnull
    {
        return source.Do(changes =>
        {
            foreach (var change in changes.ToConcreteType())
            {
                if (!predicate(change))
                {
                    continue;
                }

                changeAction(change);
            }
        });
    }

    /// <summary>
    /// Executes the OnChangeAction operation.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="reason">The reason value.</param>
    /// <param name="action">The action value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IChangeSet<TObject, TKey>> OnChangeAction<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ChangeReason reason, Action<TObject, TKey> action)
        where TObject : notnull
        where TKey : notnull
        => source.OnChangeAction(change => change.Reason == reason, change => action(change.Current, change.Key));
}
