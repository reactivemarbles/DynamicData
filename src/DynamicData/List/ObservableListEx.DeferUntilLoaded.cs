// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Defers downstream delivery until the source emits its first changeset, then forwards all subsequent changesets.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to defer until the first changeset arrives.</param>
    /// <returns>A list changeset stream that begins emitting only after the source has produced its first changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Subscribes to the source immediately but buffers internally until the first changeset arrives, at which point it emits
    /// the initial data and all subsequent changesets. This is useful when downstream consumers should not receive an empty initial state.
    /// </para>
    /// </remarks>
    /// <seealso cref="SkipInitial{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<T>(source).Run();
    }

    /// <inheritdoc cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <remarks>
    /// <inheritdoc cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <para>Convenience overload that calls <c>source.Connect().DeferUntilLoaded()</c>.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservableList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().DeferUntilLoaded();
    }
}
