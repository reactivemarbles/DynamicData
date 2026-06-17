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
    /// Applies a sliding window to the source list using start index and size from <paramref name="requests"/>.
    /// Only items within the window are included downstream.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to virtualize.</param>
    /// <param name="requests">An observable of <see cref="IVirtualRequest"/> specifying the start index and size of the window.</param>
    /// <returns>An <see cref="IVirtualChangeSet{T}"/> stream containing only items within the current virtual window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="requests"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Like <see cref="Page{T}(IObservable{IChangeSet{T}}, IObservable{IPageRequest})"/> but uses absolute start index and size instead of page number and page size.
    /// Internally maintains the full source list and recalculates the window on each change or request.
    /// </para>
    /// </remarks>
    /// <seealso cref="Page{T}(IObservable{IChangeSet{T}}, IObservable{IPageRequest})"/>
    /// <seealso cref="Top{T}(IObservable{IChangeSet{T}}, int)"/>
    public static IObservable<IVirtualChangeSet<T>> Virtualise<T>(this IObservable<IChangeSet<T>> source, IObservable<IVirtualRequest> requests)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        requests.ThrowArgumentNullExceptionIfNull(nameof(requests));

        return new Virtualiser<T>(source, requests).Run();
    }
}
