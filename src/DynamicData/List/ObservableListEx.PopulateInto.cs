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
    /// Subscribes to the source changeset stream and pipes all changes into the <paramref name="destination"/> <see cref="ISourceList{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to pipe into a target list.</param>
    /// <param name="destination">The destination <see cref="ISourceList{T}"/> to receive all changes.</param>
    /// <returns>An <see cref="IDisposable"/> representing the subscription. Dispose to stop piping changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Each changeset is applied to the destination using <c>Clone()</c> inside an <c>Edit()</c> call, producing a single batch update per changeset.</para>
    /// </remarks>
    /// <seealso cref="Clone{T}(IObservable{IChangeSet{T}}, IList{T})"/>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    /// <seealso cref="ObservableCacheEx.PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    public static IDisposable PopulateInto<T>(this IObservable<IChangeSet<T>> source, ISourceList<T> destination)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }
}
