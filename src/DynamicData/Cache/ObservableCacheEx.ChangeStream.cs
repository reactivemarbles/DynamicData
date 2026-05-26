// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableCache extensions for changeset stream lifecycle helpers.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Buffers the initial burst of changesets for the specified duration, merges them into a single
    /// changeset, then passes all subsequent changesets through without buffering.
    /// </summary>
    /// <typeparam name="TObject">The object type.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to buffer during the initial loading period.</param>
    /// <param name="initialBuffer">The <see cref="TimeSpan"/> time window to buffer, measured from when the first changeset arrives.</param>
    /// <param name="scheduler">The scheduler for timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits one merged changeset for the initial burst, then passthrough for the rest.</returns>
    /// <remarks>
    /// <para>
    /// Useful for aggregating the initial snapshot (which may arrive as many small changesets) into a
    /// single changeset for efficient downstream processing, while leaving subsequent live updates untouched.
    /// </para>
    /// <para>Internally uses <see cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>, Rx <c>Buffer</c>, and <see cref="FlattenBufferResult{TObject, TKey}"/>.</para>
    /// </remarks>
    /// <seealso cref="Batch{TObject, TKey}"/>
    /// <seealso cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> BufferInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan initialBuffer, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => source.DeferUntilLoaded().Publish(
            shared =>
            {
                var initial = shared.Buffer(initialBuffer, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult().Take(1);

                return initial.Concat(shared);
            });

    /// <summary>
    /// Suppresses all emissions until the first non-empty changeset arrives, then replays that changeset and all subsequent ones.
    /// If the source never produces a non-empty changeset, the stream waits indefinitely.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to defer until the first changeset arrives.</param>
    /// <returns>An observable that begins emitting changesets once the first non-empty changeset is received.</returns>
    /// <remarks>
    /// <para><b>Worth noting:</b> Blocks indefinitely if the cache or stream never receives any data. Ensure the source will eventually emit at least one changeset.</para>
    /// </remarks>
    /// <seealso cref="SkipInitial{TObject, TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<TObject, TKey>(source).Run();
    }

    /// <inheritdoc cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<TObject, TKey>(source).Run();
    }

    /// <summary>
    /// Skips the initial snapshot changeset that <c>Connect()</c> typically emits, then forwards all subsequent changesets.
    /// Internally uses <c>DeferUntilLoaded().Skip(1)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to skip the initial changeset.</param>
    /// <returns>An observable that skips the first changeset and forwards all others.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DeferUntilLoaded{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}})"/>
    /// <seealso cref="StartWithEmpty{TObject,TKey}(IObservable{IChangeSet{TObject,TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> SkipInitial<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.DeferUntilLoaded().Skip(1);
    }

    /// <summary>
    /// Prepends an empty changeset to the source stream, ensuring subscribers always receive an immediate
    /// (empty) notification on subscription. Uses Rx's <c>StartWith</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty changeset first, then all source changesets.</returns>
    /// <seealso cref="ObservableListEx.StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(ChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source">The source <see cref="IObservable{ISortedChangeSet{TObject, TKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty sorted changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="ISortedChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<ISortedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(SortedChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source">The source <see cref="IObservable{IVirtualChangeSet{TObject, TKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty virtual changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IVirtualChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<IVirtualChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IVirtualChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(VirtualChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <param name="source">The source <see cref="IObservable{IPagedChangeSet{TObject, TKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty paged changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IPagedChangeSet{TObject, TKey}"/>.</remarks>
    public static IObservable<IPagedChangeSet<TObject, TKey>> StartWithEmpty<TObject, TKey>(this IObservable<IPagedChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull => source.StartWith(PagedChangeSet<TObject, TKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IGroupChangeSet{TObject, TKey, TGroupKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty group changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IGroupChangeSet{TObject, TKey, TGroupKey}"/>.</remarks>
    public static IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull => source.StartWith(GroupChangeSet<TObject, TKey, TGroupKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TGroupKey">The grouping key type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IImmutableGroupChangeSet{TObject, TKey, TGroupKey}}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty immutable group changeset first, then all source changesets.</returns>
    /// <remarks>Overload for <see cref="IImmutableGroupChangeSet{TObject, TKey, TGroupKey}"/>.</remarks>
    public static IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> StartWithEmpty<TObject, TKey, TGroupKey>(this IObservable<IImmutableGroupChangeSet<TObject, TKey, TGroupKey>> source)
        where TObject : notnull
        where TKey : notnull
        where TGroupKey : notnull => source.StartWith(ImmutableGroupChangeSet<TObject, TKey, TGroupKey>.Empty);

    /// <inheritdoc cref="StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IReadOnlyCollection{T}"/> to prepend an empty changeset to.</param>
    /// <returns>An observable that emits an empty collection first, then all source collections.</returns>
    /// <remarks>Overload for <see cref="IReadOnlyCollection{T}"/>.</remarks>
    public static IObservable<IReadOnlyCollection<T>> StartWithEmpty<T>(this IObservable<IReadOnlyCollection<T>> source) => source.StartWith(ReadOnlyCollectionLight<T>.Empty);

    /// <inheritdoc cref="StartWithItem{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TObject, TKey)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to prepend an initial item to.</param>
    /// <param name="item">The item to prepend. The key is extracted from <see cref="IKey{TKey}.Key"/>.</param>
    /// <remarks>Overload for items that implement <see cref="IKey{TKey}"/>. Delegates to the explicit key overload.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TObject item)
        where TObject : IKey<TKey>
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.StartWithItem(item, item.Key);
    }

    /// <summary>
    /// Prepends a changeset containing a single <b>Add</b> for the given item and key to the source stream.
    /// The Rx equivalent of <c>StartWith</c>, but wrapped as a DynamicData changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to prepend an initial item to.</param>
    /// <param name="item">The <typeparamref name="TObject"/> item to prepend.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key for the item.</param>
    /// <returns>An observable that emits a single-item Add changeset first, then all source changesets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> StartWithItem<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TObject item, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        var change = new Change<TObject, TKey>(ChangeReason.Add, key, item);
        return source.StartWith(new ChangeSet<TObject, TKey> { change });
    }
}
