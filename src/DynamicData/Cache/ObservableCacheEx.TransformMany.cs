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
/// ObservableCache extensions for TransformMany, TransformManyAsync, and TransformManySafeAsync.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Flattens each source item into zero or more destination items (1:N), producing a single flat changeset.
    /// Each child item must have a globally unique key across all parents.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TSourceKey}}"/> to expand each item into multiple children.</param>
    /// <param name="manySelector">A function that expands a parent item into its children. For <see cref="ObservableCollection{T}"/> or <see cref="IObservableCache{TObject, TKey}"/> overloads, subsequent changes to the child collection are automatically tracked.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that extracts a unique key from each child item. Keys must be unique across ALL parents, not just within one parent.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls <paramref name="manySelector"/>, emits Add for each child.</description></item>
    ///   <item><term>Update</term><description>Diffs old children vs new children: emits Remove for removed children, Add for new children, Update for children with matching keys.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove for all children of the removed parent.</description></item>
    ///   <item><term>Refresh</term><description>Propagated as Refresh to all children (no re-expansion).</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> If two source items produce children with the same key, last-in-wins. <b>Refresh</b> does NOT re-expand children (only <b>Update</b> does).</para>
    /// <para>If two parents produce children with the same key, last-in-wins. Use the async variant with a <see cref="IComparer{T}"/> to control conflict resolution.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="manySelector"/>, or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <seealso cref="ObservableListEx.TransformMany"/>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IEnumerable<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts an <see cref="ObservableCollection{T}"/> selector. Changes to the child collection (adds, removes, replacements) are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts a <see cref="ReadOnlyObservableCollection{T}"/> selector. Changes to the child collection are automatically observed and reflected downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, ReadOnlyObservableCollection<TDestination>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <inheritdoc cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// <remarks>This overload accepts an <see cref="IObservableCache{TObject, TKey}"/> selector. The child cache is live: subsequent changes to it are automatically propagated downstream.</remarks>
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, IObservableCache<TDestination, TDestinationKey>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => new TransformMany<TDestination, TDestinationKey, TSource, TSourceKey>(source, manySelector, keySelector).Run();

    /// <summary>
    /// Async version of <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>.
    /// Flattens each source item into zero or more destination items using an async factory.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TSourceKey}}"/> to expand each item into multiple children asynchronously.</param>
    /// <param name="manySelector">An async function that expands a parent item (and its key) into an <see cref="IEnumerable{T}"/> of children.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that extracts a unique key from each child item.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional comparer to determine if two child items with the same key are equal. Used to suppress no-op updates.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that optional comparer to resolve key collisions when the same destination key is produced by multiple parents. The winning item is determined by this comparer.</param>
    /// <returns>An observable changeset of flattened child items.</returns>
    /// <remarks>
    /// <para>
    /// Because each parent's expansion is async, child collections may arrive via separate changesets
    /// (unlike the synchronous <c>TransformMany</c> which batches all children into one changeset).
    /// </para>
    /// <para>
    /// Factory exceptions propagate as <see cref="IObserver{T}.OnError"/>. Use
    /// <see cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination>
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManyAsync((val, _) => manySelector(val), keySelector, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live: its changes propagate downstream. No <c>keySelector</c> is needed since the cache already has keys. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector), equalityComparer, comparer).Run();
    }

    /// <inheritdoc cref="TransformManyAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManyAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManyAsync((val, _) => manySelector(val), equalityComparer, comparer);

    /// <summary>
    /// Async version of <see cref="TransformMany{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, IEnumerable{TDestination}}, Func{TDestination, TDestinationKey})"/>
    /// with error handling. Factory exceptions are caught and routed to <paramref name="errorHandler"/> instead of
    /// terminating the stream.
    /// </summary>
    /// <typeparam name="TDestination">The type of the child items.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the child item keys.</typeparam>
    /// <typeparam name="TSource">The type of the source (parent) items.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source (parent) keys.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TSourceKey}}"/> to expand each item into multiple children asynchronously with error handling.</param>
    /// <param name="manySelector">An async function that expands a parent item (and its key) into an <see cref="IEnumerable{T}"/> of children.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> that extracts a unique key from each child item.</param>
    /// <param name="errorHandler">A <see cref="Action{T}"/> that called when <paramref name="manySelector"/> throws. The faulting item is skipped and the stream continues.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TDestination}"/> that optional comparer to determine if two child items with the same key are equal.</param>
    /// <param name="comparer">An <see cref="IComparer{TDestination}"/> that optional comparer to resolve key collisions when the same destination key is produced by multiple parents.</param>
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
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload takes a factory that receives only the source item (without the key).</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination>
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector, keySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an observable collection (of type <typeparamref name="TCollection"/> implementing both <see cref="INotifyCollectionChanged"/> and <see cref="IEnumerable{T}"/>) whose changes are tracked live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => source.TransformManySafeAsync((val, _) => manySelector(val), keySelector, errorHandler, equalityComparer, comparer);

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives the source item and its key.</remarks>
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        manySelector.ThrowArgumentNullExceptionIfNull(nameof(manySelector));
        errorHandler.ThrowArgumentNullExceptionIfNull(nameof(errorHandler));

        return new TransformManyAsync<TSource, TSourceKey, TDestination, TDestinationKey>(source, CreateChangeSetTransformer(manySelector), equalityComparer, comparer, errorHandler).Run();
    }

    /// <inheritdoc cref="TransformManySafeAsync{TDestination, TDestinationKey, TSource, TSourceKey}(IObservable{IChangeSet{TSource, TSourceKey}}, Func{TSource, TSourceKey, Task{IEnumerable{TDestination}}}, Func{TDestination, TDestinationKey}, Action{Error{TSource, TSourceKey}}, IEqualityComparer{TDestination}?, IComparer{TDestination}?)"/>
    /// <remarks>This overload returns an <see cref="IObservableCache{TObject, TKey}"/> per parent. The child cache is live. The factory receives only the source item.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "By Design.")]
    public static IObservable<IChangeSet<TDestination, TDestinationKey>> TransformManySafeAsync<TDestination, TDestinationKey, TSource, TSourceKey>(this IObservable<IChangeSet<TSource, TSourceKey>> source, Func<TSource, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector, Action<Error<TSource, TSourceKey>> errorHandler, IEqualityComparer<TDestination>? equalityComparer = null, IComparer<TDestination>? comparer = null)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => source.TransformManySafeAsync((val, _) => manySelector(val), errorHandler, equalityComparer, comparer);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IEnumerable<TDestination>>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).AsObservableChangeSet(keySelector);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey, TCollection>(Func<TSource, TSourceKey, Task<TCollection>> manySelector, Func<TDestination, TDestinationKey> keySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull
        where TCollection : INotifyCollectionChanged, IEnumerable<TDestination> => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).ToObservableChangeSet<TCollection, TDestination>().AddKey(keySelector);

    private static Func<TSource, TSourceKey, Task<IObservable<IChangeSet<TDestination, TDestinationKey>>>> CreateChangeSetTransformer<TDestination, TDestinationKey, TSource, TSourceKey>(Func<TSource, TSourceKey, Task<IObservableCache<TDestination, TDestinationKey>>> manySelector)
        where TDestination : notnull
        where TDestinationKey : notnull
        where TSource : notnull
        where TSourceKey : notnull => async (val, key) => (await manySelector(val, key).ConfigureAwait(false)).Connect();
}
