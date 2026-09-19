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
    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Merges multiple list changeset streams from an observable-of-observables into a single unified changeset stream.
    /// Unlike <c>ObservableCacheEx.MergeChangeSets&lt;TObject, TKey&gt;(IObservable&lt;IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;&gt;, IEqualityComparer&lt;TObject&gt;)</c>, list merging performs no key-based deduplication.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;T&gt;</c> of nested changeset observables.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> used by the merge tracker to compare items.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new MergeChangeSets<TObject>(source, equalityComparer).Run();
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Merges two list changeset streams into a single unified stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <param name="source">The first <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to merge.</param>
    /// <param name="other">The second <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to merge with.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> used to compare items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<TObject>> source, IObservable<IChangeSet<TObject>> other, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(other);

        return new[] { source, other }.MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Merges the source list changeset stream with additional changeset streams into a single unified stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <param name="source">The primary source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to merge.</param>
    /// <param name="others">The additional <c>IEnumerable&lt;T&gt;</c> of list changeset streams to merge with.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> used to compare items.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<TObject>> source, IEnumerable<IObservable<IChangeSet<TObject>>> others, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(others);

        return source.EnumerateOne().Concat(others).MergeChangeSets(equalityComparer, scheduler, completable);
    }

    /// <summary>
    /// Merges a collection of list changeset streams into a single unified changeset stream.
    /// This is the canonical list MergeChangeSets overload: other overloads accepting <c>IObservable&lt;T&gt;</c>, <c>IObservableList&lt;T&gt;</c>, or pair/params variants ultimately produce equivalent behavior.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The <c>IEnumerable&lt;T&gt;</c> collection of list changeset streams to merge.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> used by the merge tracker to compare items. Defaults to <c>EqualityComparer&lt;T&gt;.Default</c> when <see langword="null"/>.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling enumeration.</param>
    /// <param name="completable">When <see langword="true"/> (default), the result completes when all sources complete.</param>
    /// <returns>A single list changeset stream containing all changes from all sources.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// All changes from inner streams are forwarded to the output. There is no key-based deduplication (unlike <c>ObservableCacheEx.MergeChangeSets&lt;TObject, TKey&gt;(IObservable&lt;IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;&gt;, IEqualityComparer&lt;TObject&gt;)</c>): if the same item appears in multiple inner streams, it will appear multiple times in the merged output.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Replace</b></term><description>The old value is replaced by the new value in the merged output. If the old value is not found (by <paramref name="equalityComparer"/>), the new value is added instead.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Refresh</b></term><description>Forwarded to the merged output.</description></item>
    /// <item><term><b>Moved</b></term><description>Ignored.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>MergeChangeSets&lt;TObject&gt;(IObservable&lt;IObservable&lt;IChangeSet&lt;TObject&gt;&gt;&gt;, IEqualityComparer&lt;TObject&gt;?)</c></seealso>
    /// <seealso><c>MergeManyChangeSets&lt;TObject, TDestination&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, IObservable&lt;IChangeSet&lt;TDestination&gt;&gt;&gt;, IEqualityComparer&lt;TDestination&gt;?)</c></seealso>
    /// <seealso><c>Or&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;IChangeSet&lt;T&gt;&gt;[])</c></seealso>
    /// <seealso><c>ObservableCacheEx.MergeChangeSets&lt;TObject, TKey&gt;(IObservable&lt;IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;&gt;, IEqualityComparer&lt;TObject&gt;)</c></seealso>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IEnumerable<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null, IScheduler? scheduler = null, bool completable = true)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new MergeChangeSets<TObject>(source, equalityComparer, completable, scheduler).Run();
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Merges list changeset streams from an <c>IObservableList&lt;T&gt;</c> into a single stream. Sources can be added or removed dynamically.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservableList<IObservable<IChangeSet<TObject>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Connect().MergeChangeSets(equalityComparer);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Merges list changeset streams from a list-of-list-changeset-observables into a single stream.
    /// Each inner list changeset observable in the source list is merged, and parent item removal triggers child cleanup.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject>> MergeChangeSets<TObject>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject>>>> source, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.MergeManyChangeSets(static src => src, equalityComparer);
    }

    /// <summary>
    /// Merges cache changeset streams from an <c>IObservableList&lt;T&gt;</c> into a single cache changeset stream.
    /// Uses <paramref name="comparer"/> to resolve conflicts when the same key appears in multiple child streams.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TKey">The type of the object key.</typeparam>
    /// <param name="source">The <c>IObservableList&lt;T&gt;</c> of cache changeset observables.</param>
    /// <param name="comparer"><c>IComparer&lt;TObject&gt;</c> to resolve which value wins when the same key appears in multiple sources.</param>
    /// <returns>A single cache changeset stream with key-based deduplication.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Sources can be added or removed dynamically from the observable list. Parent item removal triggers cleanup of all child items from that source.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b> (child)</term><description>If the destination key is new, an <b>Add</b> is emitted. If another source already contributed a child with the same key, <paramref name="comparer"/> resolves the conflict (lowest-ordered value wins). The losing value is tracked internally but not emitted.</description></item>
    /// <item><term><b>Update</b> (child)</term><description>If this source currently owns the destination key downstream, an <b>Update</b> is emitted. Otherwise <paramref name="comparer"/> re-evaluates all sources; a different source's value may win, producing an <b>Update</b> to that value instead.</description></item>
    /// <item><term><b>Remove</b> (child)</term><description>If this source's value was the one published downstream for that destination key, the operator scans other sources for the same key. If found, an <b>Update</b> is emitted with the replacement (per <paramref name="comparer"/>). Otherwise a <b>Remove</b> is emitted.</description></item>
    /// <item><term><b>Refresh</b> (child)</term><description>If the child item is the one currently published downstream, the <b>Refresh</b> is forwarded. Otherwise <paramref name="comparer"/> re-evaluates all sources; if a different value now wins, an <b>Update</b> is emitted instead.</description></item>
    /// <item><term>Source list <b>Add</b></term><description>Subscribes to the new child changeset stream and merges its keys into the output.</description></item>
    /// <item><term>Source list <b>Remove</b></term><description>Disposes that source's subscription. All keys it contributed are removed. For keys also contributed by other sources, the next-best value (per <paramref name="comparer"/>) is promoted as an <b>Update</b>, not an Add.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>MergeChangeSets&lt;TObject, TKey&gt;(IObservableList&lt;IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;&gt;, IEqualityComparer&lt;TObject&gt;?, IComparer&lt;TObject&gt;?)</c></seealso>
    /// <seealso><c>MergeManyChangeSets&lt;TObject, TDestination, TDestinationKey&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, IObservable&lt;IChangeSet&lt;TDestination, TDestinationKey&gt;&gt;&gt;, IEqualityComparer&lt;TDestination&gt;?, IComparer&lt;TDestination&gt;?)</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Connect().MergeChangeSets(comparer);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Merges cache changeset streams from an <c>IObservableList&lt;T&gt;</c> into a single cache changeset stream, with optional equality and ordering comparers.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The <c>IObservableList&lt;T&gt;</c> of cache changeset observables.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> to determine if two elements are the same.</param>
    /// <param name="comparer">An optional <c>IComparer&lt;TObject&gt;</c> to resolve conflicts when the same key appears in multiple sources.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, IEqualityComparer<TObject>? equalityComparer = null, IComparer<TObject>? comparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Connect().MergeChangeSets(equalityComparer, comparer);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Merges cache changeset streams from a list changeset of cache changeset observables, using a comparer for conflict resolution.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;T&gt;</c> whose items are cache changeset observables.</param>
    /// <param name="comparer"><c>IComparer&lt;TObject&gt;</c> to resolve which value wins when the same key appears in multiple sources.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject, TKey>>>> source, IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(comparer);

        return source.MergeChangeSets(comparer);
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Merges cache changeset streams from a list changeset of cache changeset observables, with optional equality and ordering comparers.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;T&gt;</c> whose items are cache changeset observables.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> to determine if two elements are the same.</param>
    /// <param name="comparer">An optional <c>IComparer&lt;TObject&gt;</c> to resolve conflicts when the same key appears in multiple sources.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> MergeChangeSets<TObject, TKey>(this IObservable<IChangeSet<IObservable<IChangeSet<TObject, TKey>>>> source, IEqualityComparer<TObject>? equalityComparer = null, IComparer<TObject>? comparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.MergeManyChangeSets(static src => src, equalityComparer, comparer);
    }
}
