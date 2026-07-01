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
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Takes the first <paramref name="numberOfItems"/> items from the source list. Implemented as <c>Virtualise</c> with a fixed window starting at index 0.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to take the top items.</param>
    /// <param name="numberOfItems">The maximum number of items to include. Must be greater than zero.</param>
    /// <returns>A virtual changeset stream containing at most <paramref name="numberOfItems"/> items from the beginning of the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="numberOfItems"/> is zero or negative.</exception>
    /// <remarks>
    /// <para>The source should ideally be sorted before applying Top, since list order determines which items appear.</para>
    /// </remarks>
    /// <seealso><c>Virtualise&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IVirtualRequest&gt;)</c></seealso>
    /// <seealso><c>Page&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IPageRequest&gt;)</c></seealso>
    /// <seealso><c>Sort&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IComparer&lt;T&gt;, SortOptions, IObservable&lt;Unit&gt;?, IObservable&lt;IComparer&lt;T&gt;&gt;?, int)</c></seealso>
    public static IObservable<IChangeSet<T>> Top<T>(this IObservable<IChangeSet<T>> source, int numberOfItems)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        if (numberOfItems <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfItems), "Number of items should be greater than zero");
        }

        return source.Virtualise(Observable.Return(new VirtualRequest(0, numberOfItems)));
    }
}
