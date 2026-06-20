// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Prepends an empty changeset to the source stream. Useful for initializing downstream consumers that expect an initial emission.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to prepend an empty changeset to.</param>
    /// <returns>A list changeset stream that begins with an empty changeset.</returns>
    /// <seealso cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="SkipInitial{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ObservableCacheEx.StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<T>> StartWithEmpty<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.StartWith(ChangeSet<T>.Empty);
    }
}
