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
    /// Flattens each source item into multiple destination items using <paramref name="manySelector"/>. Each source item produces zero or more children,
    /// all of which are merged into a single flat list changeset stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource&gt;&gt;</c> to expand each item into multiple children.</param>
    /// <param name="manySelector">A <c>Func&lt;T, TResult&gt;</c> function that returns the child items for each source item.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TDestination&gt;</c> used during Replace to determine which child items changed between old and new parent values.</param>
    /// <returns>A list changeset stream of all child items from all source items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="manySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Children expanded and added to the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old children diffed against new children (using <paramref name="equalityComparer"/>). Removed, added, or kept as appropriate.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>All children of the removed parents are removed from the output.</description></item>
    /// <item><term><b>Refresh</b></term><description>Children re-expanded and diffed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>Transform&lt;TSource, TDestination&gt;(IObservable&lt;IChangeSet&lt;TSource&gt;&gt;, Func&lt;TSource, TDestination&gt;, bool)</c></seealso>
    /// <seealso><c>MergeManyChangeSets&lt;TObject, TDestination&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, IObservable&lt;IChangeSet&lt;TDestination&gt;&gt;&gt;, IEqualityComparer&lt;TDestination&gt;?)</c></seealso>
    /// <seealso><c>ObservableCacheEx.TransformMany&lt;TDestination, TDestinationKey, TSource, TSourceKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TSourceKey&gt;&gt;, Func&lt;TSource, IEnumerable&lt;TDestination&gt;&gt;, Func&lt;TDestination, TDestinationKey&gt;)</c></seealso>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IEnumerable<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(manySelector);

        return new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();
    }

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Flattens each source item into children from an <c>ObservableCollection&lt;T&gt;</c>. The collection is observed for subsequent changes.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Flattens each source item into children from a <c>ReadOnlyObservableCollection&lt;T&gt;</c>. The collection is observed for subsequent changes.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();

    /// <para>This overload follows the same core behavior as the related overload.</para>
    /// <summary>
    /// Flattens each source item into children from an <c>IObservableList&lt;T&gt;</c>. The inner list is observed for subsequent changes.
    /// </summary>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <typeparam name="TSource">The type of the TSource value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="manySelector">The manySelector value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TDestination>> TransformMany<TDestination, TSource>(this IObservable<IChangeSet<TSource>> source, Func<TSource, IObservableList<TDestination>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TDestination : notnull
        where TSource : notnull => new TransformMany<TSource, TDestination>(source, manySelector, equalityComparer).Run();
}
