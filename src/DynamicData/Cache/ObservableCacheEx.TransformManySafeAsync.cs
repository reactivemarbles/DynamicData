// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Specialized;
#if REACTIVE_SHIM
using DynamicData.Reactive.Cache.Internal;
#else
using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Async version of <c>TransformMany&lt;TDestination, TDestinationKey, TSource, TSourceKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TSourceKey&gt;&gt;, Func&lt;TSource, IEnumerable&lt;TDestination&gt;&gt;, Func&lt;TDestination, TDestinationKey&gt;)</c>
    /// with error handling. Factory exceptions are caught and routed to <paramref name="errorHandler"/> instead of
    /// terminating the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource, TSourceKey&gt;&gt;</c> to expand each item into multiple children asynchronously with error handling.</param>
    /// <param name="manySelector">An async function that expands a parent item (and its key) into an <c>IEnumerable&lt;T&gt;</c> of children.</param>
    /// <param name="keySelector">A <c>Func&lt;T, TResult&gt;</c> that extracts a unique key from each child item.</param>
    /// <param name="errorHandler">A <c>Action&lt;T&gt;</c> that called when <paramref name="manySelector"/> throws. The faulting item is skipped and the stream continues.</param>
    /// <param name="equalityComparer">An <c>IEqualityComparer&lt;TDestination&gt;</c> that optional comparer to determine if two child items with the same key are equal.</param>
    /// <param name="comparer">An <c>IComparer&lt;TDestination&gt;</c> that optional comparer to resolve key collisions when the same destination key is produced by multiple parents.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>Because the transformations are asynchronous, each sub-collection may be emitted via a separate changeset.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="manySelector"/>, or <paramref name="errorHandler"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(manySelector);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <summary>
    /// Provides an overload of <c>manySelector</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <summary>
    /// Provides an overload of <c>TransformManySafeAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <typeparam name="TCollection">The type of the TCollection value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <c>IEnumerable&lt;T&gt;</c>) whose changes are tracked live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination>
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(manySelector);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <summary>
    /// Provides an overload of <c>manySelector</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <typeparam name="TCollection">The type of the TCollection value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <c>IEnumerable&lt;T&gt;</c>) whose changes are tracked live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <summary>
    /// Provides an overload of <c>TransformManySafeAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload returns an <c>IObservableCache&lt;TObject, TKey&gt;</c> per parent. The child cache is live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(manySelector);
        ArgumentExceptionHelper.ThrowIfNull(errorHandler);

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <summary>
    /// Provides an overload of <c>manySelector</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="errorHandler">The errorHandler value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload returns an <c>IObservableCache&lt;TObject, TKey&gt;</c> per parent. The child cache is live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), errorHandler, equalityComparer, comparer);
}
