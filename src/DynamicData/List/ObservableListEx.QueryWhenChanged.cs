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
    /// The <paramref name="resultSelector"/> receives an <see cref="IReadOnlyCollection{T}"/> representing the current state.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TDestination">The type of the projected result.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to project on each change.</param>
    /// <param name="resultSelector">A <see cref="Func{T, TResult}"/> function projecting the current list snapshot to a result value.</param>
    /// <returns>An observable emitting the projected value after each changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="resultSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Delegates to <see cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/> and applies <paramref name="resultSelector"/> via <c>Select</c>.</para>
    /// </remarks>
    /// <seealso cref="QueryWhenChanged{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ObservableCacheEx.QueryWhenChanged{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{IQuery{TObject, TKey}, TDestination})"/>
    public static IObservable<TDestination> QueryWhenChanged<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<IReadOnlyCollection<TObject>, TDestination> resultSelector)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(resultSelector);

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// Emits an <see cref="IReadOnlyCollection{T}"/> snapshot of the current list state after every changeset.
    /// Maintains an internal list updated by cloning each changeset.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to project on each change.</param>
    /// <returns>An observable emitting the full list snapshot as <see cref="IReadOnlyCollection{T}"/> after each change.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a non-changeset operator. It emits the entire collection state on each change, not incremental diffs.</para>
    /// <para><b>Worth noting:</b> A new snapshot is emitted on every changeset, which can be chatty. The collection is rebuilt by cloning each changeset into an internal list. For sorted output, use <see cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>.</para>
    /// </remarks>
    /// <seealso cref="QueryWhenChanged{TObject, TDestination}(IObservable{IChangeSet{TObject}}, Func{IReadOnlyCollection{TObject}, TDestination})"/>
    /// <seealso cref="ToCollection{TObject}(IObservable{IChangeSet{TObject}})"/>
    /// <seealso cref="ToSortedCollection{TObject, TSortKey}(IObservable{IChangeSet{TObject}}, Func{TObject, TSortKey}, SortDirection)"/>
    public static IObservable<IReadOnlyCollection<T>> QueryWhenChanged<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new QueryWhenChanged<T>(source).Run();
    }
}
