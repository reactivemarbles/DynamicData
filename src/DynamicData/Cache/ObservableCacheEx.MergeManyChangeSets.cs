// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. This overload requires a comparer for resolving destination key conflicts.
    /// The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="observableSelector"/> or <paramref name="comparer"/> is null.</exception>
    /// <seealso cref="ObservableListEx.MergeManyChangeSets"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return source.MergeManyChangeSets((t, _) => observableSelector(t), comparer);
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. This overload requires a comparer for resolving destination key conflicts.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="comparer"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TDestination> comparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);
        ArgumentExceptionHelper.ThrowIfNull(comparer);

        return source.MergeManyChangeSets(observableSelector, equalityComparer: null, comparer: comparer);
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child changes
    /// into a single flattened output. The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value for a destination key.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that optional comparer to resolve key conflicts when multiple child streams provide items with the same destination key. The lowest-ordered item wins.</param>
    /// <returns>A merged changeset stream containing items from all active child streams.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return source.MergeManyChangeSets((t, _) => observableSelector(t), equalityComparer, comparer);
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child changeset stream and merges all child
    /// changes into a single flattened output stream. Child subscriptions track the parent item lifecycle:
    /// created on Add, replaced on Update, disposed on Remove.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source (parent) cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying parent items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a parent item and its key, and returns a child cache changeset stream. Called once per parent Add/Update.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress no-op child updates. When a child key's new value equals the current value per this comparer, the update is not emitted.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that optional comparer to resolve child key conflicts when multiple parents contribute children with the same destination key. The lowest-ordered child value wins. Without a comparer, the first parent to provide a key retains priority.</param>
    /// <returns>A merged changeset stream containing all child items from all active parent subscriptions.</returns>
    /// <remarks>
    /// <para>
    /// This is the changeset-aware counterpart to <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>.
    /// Where MergeMany produces a flat <c>IObservable&lt;T&gt;</c>, MergeManyChangeSets produces an <c>IObservable&lt;IChangeSet&gt;</c>
    /// that tracks the full lifecycle of child items, including key conflict resolution across parents.
    /// </para>
    /// <para>
    /// <b>Parent-side change handling (source changeset events):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="observableSelector"/> with the new parent item to obtain a child changeset stream, then subscribes. As the child stream emits changesets, those child items are merged into the output. The downstream observer sees <b>Add</b> changes for each new child item.</description></item>
    /// <item><term>Update</term><description>Disposes the previous parent's child subscription (removing all of its contributed child items from the output as <b>Remove</b> changes), then creates a new child subscription for the updated parent. The new child's items appear as <b>Add</b> changes.</description></item>
    /// <item><term>Remove</term><description>Disposes the parent's child subscription. All child items contributed by that parent are emitted as <b>Remove</b> changes in the output. If another parent also provides a child with the same destination key, that parent's value is promoted as an <b>Update</b> (not an Add).</description></item>
    /// <item><term>Refresh</term><description>No effect on the child subscription. The parent's child stream continues unchanged.</description></item>
    /// </list>
    /// <para>
    /// <b>Child-side change handling (changes arriving from child changeset streams):</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the destination key is new, an <b>Add</b> is emitted. If another parent already contributed a child with the same key, the conflict is resolved by <paramref name="comparer"/> (lowest wins) or first-in-wins if no comparer. The losing value is tracked internally but not emitted.</description></item>
    /// <item><term>Update</term><description>If this parent currently owns the destination key downstream, an <b>Update</b> is emitted. With a comparer, all parents are re-evaluated for that key; a different parent's value may win, producing an <b>Update</b> to that value instead.</description></item>
    /// <item><term>Remove</term><description>If this parent's value was the one published downstream for that destination key, the operator scans other parents for the same key. If found, an <b>Update</b> is emitted with the replacement. If not, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>If the child item is the one currently published downstream, the <b>Refresh</b> is forwarded. With a comparer, all parents are re-evaluated first; if a different value now wins, an <b>Update</b> is emitted instead.</description></item>
    /// </list>
    /// <para>
    /// <b>Error and completion:</b>
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>OnError</term><description>An error from the source (parent) stream or from any child changeset stream terminates the entire output. Unlike <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>, child errors are NOT swallowed.</description></item>
    /// <item><term>OnCompleted</term><description>The output completes when the source (parent) stream completes <b>and</b> all active child changeset streams have also completed.</description></item>
    /// </list>
    /// <para>
    /// <b>Worth noting:</b> When multiple parents contribute children with the same destination key, only one value is published
    /// downstream at a time. The <paramref name="comparer"/> controls which value wins; without it, the first parent to add the key
    /// retains priority. Removing a parent that owned a contested key causes the next-best value (per comparer or next available)
    /// to surface as an <b>Update</b>, not an Add. The <paramref name="equalityComparer"/> independently controls whether a child
    /// Update for an already-published key is suppressed when the new value equals the old.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <seealso cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IObservable{IChangeSet{TDestination, TDestinationKey}}}, Func{TDestination, TDestinationKey})"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return new MergeManyCacheChangeSets<TObject, TKey, TDestination, TDestinationKey>(source, observableSelector, equalityComparer, comparer).Run();
    }

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/>.
    /// Uses <paramref name="sourceComparer"/> to resolve destination key conflicts by source priority.
    /// The selector receives only the item, not its key.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, DefaultResortOnSourceRefresh, equalityComparer: null, childComparer);
    }

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/>.
    /// Uses <paramref name="sourceComparer"/> to resolve destination key conflicts by source priority.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, DefaultResortOnSourceRefresh, equalityComparer: null, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/> and
    /// explicit <paramref name="resortOnSourceRefresh"/> control. The selector receives only the item.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, resortOnSourceRefresh, equalityComparer: null, childComparer);
    }

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with a required <paramref name="childComparer"/> and
    /// explicit <paramref name="resortOnSourceRefresh"/> control.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IComparer<TDestination> childComparer)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, resortOnSourceRefresh, equalityComparer: null, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets. Uses <paramref name="sourceComparer"/> to resolve
    /// destination key conflicts. The selector receives only the item, not its key.
    /// Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, DefaultResortOnSourceRefresh, equalityComparer, childComparer);
    }

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets. Uses <paramref name="sourceComparer"/> to resolve
    /// destination key conflicts. Source priorities are always re-evaluated on Refresh (default behavior).
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull => source.MergeManyChangeSets(observableSelector, sourceComparer, DefaultResortOnSourceRefresh, equalityComparer, childComparer);

    /// <summary>
    /// Source-priority variant of MergeManyChangeSets with full control over all conflict resolution parameters.
    /// The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/>, a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that optional fallback comparer for destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream with conflicts resolved by source priority.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return source.MergeManyChangeSets((t, _) => observableSelector(t), sourceComparer, resortOnSourceRefresh, equalityComparer, childComparer);
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child cache changeset stream and merges all child
    /// changes into a single flattened output. When multiple source items produce children with the same destination key,
    /// <paramref name="sourceComparer"/> determines which source has priority (the source ordering lower wins).
    /// If sources compare equal, <paramref name="childComparer"/> (if provided) breaks the tie.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child changeset streams.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the key identifying child items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child cache changeset stream.</param>
    /// <param name="sourceComparer">An <see cref="IComparer{TObject}"/> that comparer to prioritize between source items when their children produce the same destination key. Lower-ordered source wins.</param>
    /// <param name="resortOnSourceRefresh">If <see langword="true"/> (default), a Refresh in the source stream re-evaluates source priorities. If <see langword="false"/>, Refresh events are ignored for priority recalculation.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to suppress updates when the incoming child value equals the current value for a destination key.</param>
    /// <param name="childComparer">An <see cref="IComparer{TDestination}"/> that optional fallback comparer to resolve destination key conflicts when source items compare equal.</param>
    /// <returns>A merged changeset stream containing items from all active child streams, with conflicts resolved by source priority.</returns>
    /// <remarks>
    /// <para>
    /// The <paramref name="sourceComparer"/> provides a layer of conflict resolution above the child values themselves.
    /// This is useful when source items represent priority tiers (e.g., user settings overriding defaults).
    /// </para>
    /// <para>
    /// Errors from child streams propagate to the output. An error from the source or any child terminates the merged output.
    /// The output completes when the source completes and all active child streams have also completed.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="sourceComparer"/> is null.</exception>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> MergeManyChangeSets<TObject, TKey, TDestination, TDestinationKey>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination, TDestinationKey>>> observableSelector, IComparer<TObject> sourceComparer, bool resortOnSourceRefresh, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? childComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
        where TDestinationKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);
        ArgumentExceptionHelper.ThrowIfNull(sourceComparer);

        return new MergeManyCacheChangeSetsSourceCompare<TObject, TKey, TDestination, TDestinationKey>(source, observableSelector, sourceComparer, equalityComparer, childComparer, resortOnSourceRefresh).Run();
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child list changeset stream produced by
    /// <paramref name="observableSelector"/> and merges all child changes into a single flattened list changeset output.
    /// Child subscriptions follow the source item lifecycle: created on Add, replaced on Update, disposed on Remove.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child list changeset streams.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and its key, and returns a child list changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to detect duplicate items in the merged list output.</param>
    /// <returns>A merged list changeset stream containing items from all active child streams.</returns>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return new MergeManyListChangeSets<TObject, TKey, TDestination>(source, observableSelector, equalityComparer).Run();
    }

    /// <summary>
    /// For each item in the source cache, subscribes to a child list changeset stream and merges all child changes
    /// into a single flattened list changeset output. The selector receives only the item, not its key.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of items in the child list changeset streams.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce a child changeset stream.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives a source item and returns a child list changeset stream.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional equality comparer to detect duplicate items in the merged list output.</param>
    /// <returns>A merged list changeset stream containing items from all active child streams.</returns>
    public static IObservable<IChangeSet<TDestination>> MergeManyChangeSets<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<IChangeSet<TDestination>>> observableSelector, IEqualityComparer<TDestination>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);
        return source.MergeManyChangeSets((obj, _) => observableSelector(obj), equalityComparer);
    }

    private const bool DefaultResortOnSourceRefresh = true;
}
