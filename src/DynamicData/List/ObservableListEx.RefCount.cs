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
    /// Reference-counted materialization of the source changeset stream into an <see cref="IObservableList{T}"/>.
    /// The shared list is created on the first subscriber and disposed when the last subscriber unsubscribes.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to share via reference counting.</param>
    /// <returns>A list changeset stream backed by a shared, reference-counted <see cref="IObservableList{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Equivalent to <c>Publish().RefCount()</c> for changeset streams. The underlying list is created lazily on first subscription.</para>
    /// </remarks>
    /// <seealso cref="AsObservableList{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> RefCount<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new RefCount<T>(source).Run();
    }
}
