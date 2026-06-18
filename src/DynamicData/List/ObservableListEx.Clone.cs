// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Binding;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Applies each changeset to the target list as a side effect, keeping it synchronized with the source.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to clone.</param>
    /// <param name="target">The <see cref="IList{T}"/> target list to clone changes into.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Lower-level than <see cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>. Uses <see cref="IList{T}"/>.Clone() to apply all changeset operations directly.</para>
    /// </remarks>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <seealso cref="PopulateInto{T}(IObservable{IChangeSet{T}}, ISourceList{T})"/>
    public static IObservable<IChangeSet<T>> Clone<T>(this IObservable<IChangeSet<T>> source, IList<T> target)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Do(target.Clone);
    }
}
