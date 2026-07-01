// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.List.Internal;
#else

using DynamicData.List.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to virtualize.</param>
    /// <param name="requests">An observable of <see cref="IVirtualRequest"/> specifying the start index and size of the window.</param>
    /// <returns>An <c>IVirtualChangeSet&lt;T&gt;</c> stream containing only items within the current virtual window.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="requests"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Like <c>Page&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IPageRequest&gt;)</c> but uses absolute start index and size instead of page number and page size.
    /// Internally maintains the full source list and recalculates the window on each change or request.
    /// </para>
    /// </remarks>
    /// <seealso><c>Page&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IPageRequest&gt;)</c></seealso>
    /// <seealso><c>Top&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, int)</c></seealso>
    public static IObservable<IVirtualChangeSet<T>> Virtualise<T>(this IObservable<IChangeSet<T>> source, IObservable<IVirtualRequest> requests)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(requests);

        return new Virtualiser<T>(source, requests).Run();
    }
}
