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
    /// Wraps a <see cref="ISourceList{T}"/> as a read-only <see cref="IObservableList{T}"/>, hiding mutation methods.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The <see cref="ISourceList{T}"/> mutable source list to wrap.</param>
    /// <returns>A read-only observable list that mirrors the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static IObservableList<T> AsObservableList<T>(this ISourceList<T> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new AnonymousObservableList<T>(source);
    }

    /// <summary>
    /// Materializes a changeset stream into a read-only <see cref="IObservableList{T}"/>.
    /// The list is kept in sync with the source stream for the lifetime of the subscription.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to materialize into a read-only list.</param>
    /// <returns>A read-only observable list reflecting the current state of the stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the primary way to <b>multicast</b> a changeset pipeline. Materializing once into an <see cref="IObservableList{T}"/>,
    /// then calling <c>Connect()</c> on the result for each downstream consumer, ensures the upstream operators are evaluated only once
    /// regardless of how many subscribers consume the result.
    /// </para>
    /// </remarks>
    /// <seealso cref="AsObservableList{T}(ISourceList{T})"/>
    public static IObservableList<T> AsObservableList<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new AnonymousObservableList<T>(source);
    }
}
