// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif
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
    /// Emits a projected value from the current list snapshot after every changeset.
    /// The <paramref name="resultSelector"/> receives an <c>IReadOnlyCollection&lt;T&gt;</c> representing the current state.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TDestination">The type of the projected result.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to project on each change.</param>
    /// <param name="resultSelector">A <c>Func&lt;T, TResult&gt;</c> function projecting the current list snapshot to a result value.</param>
    /// <returns>An observable emitting the projected value after each changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="resultSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Delegates to <c>QueryWhenChanged&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c> and applies <paramref name="resultSelector"/> via <c>Select</c>.</para>
    /// </remarks>
    /// <seealso><c>QueryWhenChanged&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>ToCollection&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;)</c></seealso>
    /// <seealso><c>ObservableCacheEx.QueryWhenChanged&lt;TObject, TKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;IQuery&lt;TObject, TKey&gt;, TDestination&gt;)</c></seealso>
    public static IObservable<TDestination> QueryWhenChanged<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<IReadOnlyCollection<TObject>, TDestination> resultSelector)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(resultSelector);

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// Emits an <c>IReadOnlyCollection&lt;T&gt;</c> snapshot of the current list state after every changeset.
    /// Maintains an internal list updated by cloning each changeset.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to project on each change.</param>
    /// <returns>An observable emitting the full list snapshot as <c>IReadOnlyCollection&lt;T&gt;</c> after each change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a non-changeset operator. It emits the entire collection state on each change, not incremental diffs.</para>
    /// <para><b>Worth noting:</b> A new snapshot is emitted on every changeset, which can be chatty. The collection is rebuilt by cloning each changeset into an internal list. For sorted output, use <c>ToSortedCollection&lt;TObject, TSortKey&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, TSortKey&gt;, SortDirection)</c>.</para>
    /// </remarks>
    /// <seealso><c>QueryWhenChanged&lt;TObject, TDestination&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;IReadOnlyCollection&lt;TObject&gt;, TDestination&gt;)</c></seealso>
    /// <seealso><c>ToCollection&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;)</c></seealso>
    /// <seealso><c>ToSortedCollection&lt;TObject, TSortKey&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, TSortKey&gt;, SortDirection)</c></seealso>
    public static IObservable<IReadOnlyCollection<T>> QueryWhenChanged<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new QueryWhenChanged<T>(source).Run();
    }
}
