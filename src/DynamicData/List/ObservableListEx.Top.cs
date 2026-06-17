// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Takes the first <paramref name="numberOfItems"/> items from the source list. Implemented as <c>Virtualise</c> with a fixed window starting at index 0.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to take the top items.</param>
    /// <param name="numberOfItems">The maximum number of items to include. Must be greater than zero.</param>
    /// <returns>A virtual changeset stream containing at most <paramref name="numberOfItems"/> items from the beginning of the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfItems"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>The source should ideally be sorted before applying Top, since list order determines which items appear.</para>
    /// </remarks>
    /// <seealso cref="Virtualise{T}(IObservable{IChangeSet{T}}, IObservable{IVirtualRequest})"/>
    /// <seealso cref="Page{T}(IObservable{IChangeSet{T}}, IObservable{IPageRequest})"/>
    /// <seealso cref="Sort{T}(IObservable{IChangeSet{T}}, IComparer{T}, SortOptions, IObservable{Unit}?, IObservable{IComparer{T}}?, int)"/>
    public static IObservable<IChangeSet<T>> Top<T>(this IObservable<IChangeSet<T>> source, int numberOfItems)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (numberOfItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfItems), "Number of items should be greater than zero");
        }

        return source.Virtualise(Observable.Return(new VirtualRequest(0, numberOfItems)));
    }
}
