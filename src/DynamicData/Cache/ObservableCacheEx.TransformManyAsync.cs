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
    /// Async version of <c>TransformMany&lt;TDestination, TDestinationKey, TSource, TSourceKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TSourceKey&gt;&gt;, Func&lt;TSource, IEnumerable&lt;TDestination&gt;&gt;, Func&lt;TDestination, TDestinationKey&gt;)</c>.
    /// Flattens each source item into zero or more destination items using an async factory.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource, TSourceKey&gt;&gt;</c> to expand each item into multiple children asynchronously.</param>
    /// <param name="manySelector">An async function that expands a parent item (and its key) into an <c>IEnumerable&lt;T&gt;</c> of children.</param>
    /// <param name="keySelector">A <c>Func&lt;T, TResult&gt;</c> that extracts a unique key from each child item.</param>
    /// <param name="equalityComparer">An <c>IEqualityComparer&lt;TDestination&gt;</c> that optional comparer to determine if two child items with the same key are equal. Used to suppress no-op updates.</param>
    /// <param name="comparer">An <c>IComparer&lt;TDestination&gt;</c> that optional comparer to resolve key collisions when the same destination key is produced by multiple parents. The winning item is determined by this comparer.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>
    /// <para>
    /// Because each parent's expansion is async, child collections may arrive via separate changesets
    /// (unlike the synchronous <c>TransformMany</c> which batches all children into one changeset).
    /// </para>
    /// <para>
    /// Factory exceptions propagate as <c>IObserver&lt;T&gt;.OnError</c>. Use
    /// <c>TransformManySafeAsync&lt;TDestination, TDestinationKey, TSource, TSourceKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TSourceKey&gt;&gt;, Func&lt;TSource, TSourceKey, Task&lt;IEnumerable&lt;TDestination&gt;&gt;&gt;, Func&lt;TDestination, TDestinationKey&gt;, Action&lt;Error&lt;TSource, TSourceKey&gt;&gt;, IEqualityComparer&lt;TDestination&gt;?, IComparer&lt;TDestination&gt;?)</c>
    /// to catch errors without killing the stream.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="manySelector"/> is <see langword="null"/>.</exception>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(manySelector);

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer).Run();
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
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <summary>
    /// Provides an overload of <c>TransformManyAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <typeparam name="TCollection">The type of the TCollection value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="keySelector">The keySelector value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <c>IEnumerable&lt;T&gt;</c>) whose changes are tracked live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination>
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(manySelector);

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer).Run();
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
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <c>IEnumerable&lt;T&gt;</c>) whose changes are tracked live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <summary>
    /// Provides an overload of <c>TransformManyAsync</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <typeparam name="TSourceKey">The type of the TSourceKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload returns an <c>IObservableCache&lt;TObject, TKey&gt;</c> per parent. The child cache is live: its changes propagate downstream. No <c>keySelector</c> is needed since the cache already has keys. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(manySelector);

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector), equalityComparer, comparer).Run();
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
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload returns an <c>IObservableCache&lt;TObject, TKey&gt;</c> per parent. The child cache is live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), equalityComparer, comparer);
}
