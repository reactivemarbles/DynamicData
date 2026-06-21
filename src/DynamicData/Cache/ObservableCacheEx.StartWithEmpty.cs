// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
    /// Prepends an empty changeset to the source stream, ensuring subscribers always receive an immediate
    /// (empty) notification on subscription. Uses Rx's <c>StartWith</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty changeset first, then all source changesets.</returns>
    /// <seealso><c>ObservableListEx.StartWithEmpty&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWith(ChangeSet<TObject, TKey>.Empty);
    }

    /// <summary>
    /// Provides an overload of <c>StartWithEmpty</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;ISortedChangeSet&lt;TObject, TKey&gt;&gt;</c> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty sorted changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <c>ISortedChangeSet&lt;TObject, TKey&gt;</c>.</remarks>
    public static IObservable<ISortedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWith(SortedChangeSet<TObject, TKey>.Empty);
    }

    /// <summary>
    /// Provides an overload of <c>StartWithEmpty</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IVirtualChangeSet&lt;TObject, TKey&gt;&gt;</c> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty virtual changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <c>IVirtualChangeSet&lt;TObject, TKey&gt;</c>.</remarks>
    public static IObservable<IVirtualChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWith(VirtualChangeSet<TObject, TKey>.Empty);
    }

    /// <summary>
    /// Provides an overload of <c>StartWithEmpty</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IPagedChangeSet&lt;TObject, TKey&gt;&gt;</c> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty paged changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <c>IPagedChangeSet&lt;TObject, TKey&gt;</c>.</remarks>
    public static IObservable<IPagedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWith(PagedChangeSet<TObject, TKey>.Empty);
    }

    /// <summary>
    /// Provides an overload of <c>StartWithEmpty</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IGroupChangeSet&lt;TObject, TKey, TGroupKey&gt;&gt;</c> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty group changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <c>IGroupChangeSet&lt;TObject, TKey, TGroupKey&gt;</c>.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWith(GroupChangeSet<TObject, TKey, TGroupKey>.Empty);
    }

    /// <summary>
    /// Provides an overload of <c>StartWithEmpty</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IImmutableGroupChangeSet&lt;TObject, TKey, TGroupKey&gt;&gt;</c> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty immutable group changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <c>IImmutableGroupChangeSet&lt;TObject, TKey, TGroupKey&gt;</c>.</remarks>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWith(ImmutableGroupChangeSet<TObject, TKey, TGroupKey>.Empty);
    }

    /// <summary>
    /// Provides an overload of <c>StartWithEmpty</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;T&gt;</c> of <c>IReadOnlyCollection&lt;T&gt;</c> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty collection first, then all source collections.</returns>
    /// <remarks>Overload for <c>IReadOnlyCollection&lt;T&gt;</c>.</remarks>
    public static IObservable<IReadOnlyCollection<T>> StartWithEmpty<T>(this IObservable<IReadOnlyCollection<T>> source)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.StartWith(ReadOnlyCollectionLight<T>.Empty);
    }
}
