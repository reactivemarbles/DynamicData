// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableList extensions for type and shape conversions.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Adds a key to each item in a list changeset, converting it to a cache changeset that supports all keyed DynamicData operators.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to add keys to, converting to a cache changeset.</param>
    /// <param name="keySelector">A <see cref="Func{T, TResult}"/> function to extract a unique key from each item.</param>
    /// <returns>A cache <see cref="IObservable{IChangeSet{TObject, TKey}}"/> changeset stream with keyed items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="keySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// All index information is dropped during conversion because cache changesets are unordered by default.
    /// Use this when you need to transition from list-based pipelines to cache-based operators (Filter by key, Join, Group, etc.).
    /// </para>
    /// </remarks>
    /// <seealso cref="ObservableCacheEx.RemoveKey{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> AddKey<TObject, TKey>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return source.Select(changes => new ChangeSet<TObject, TKey>(new AddKeyEnumerator<TObject, TKey>(changes, keySelector)));
    }

    /// <summary>
    /// Wraps a <see cref="ISourceList{T}"/> as a read-only <see cref="IObservableList{T}"/>, hiding mutation methods.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The <see cref="ISourceList{T}"/> mutable source list to wrap.</param>
    /// <returns>A read-only observable list that mirrors the source.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static IObservableList<T> AsObservableList<T>(this ISourceList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new AnonymousObservableList<T>(source);
    }

    /// <summary>
    /// Materializes a changeset stream into a read-only <see cref="IObservableList{T}"/>.
    /// The list is kept in sync with the source stream for the lifetime of the subscription.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to materialize into a read-only list.</param>
    /// <returns>A read-only observable list reflecting the current state of the stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the primary way to <b>multicast</b> a changeset pipeline. Materializing once into an <see cref="IObservableList{T}"/>,
    /// then calling <c>Connect()</c> on the result for each downstream consumer, ensures the upstream operators are evaluated only once
    /// regardless of how many subscribers consume the result.
    /// </para>
    /// </remarks>
    /// <seealso cref="AsObservableList{T}(ISourceList{T})"/>
    public static IObservableList<T> AsObservableList<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new AnonymousObservableList<T>(source);
    }

    /// <summary>
    /// Casts each item in the changeset from <c>object</c> to <typeparamref name="TDestination"/> using a direct cast.
    /// </summary>
    /// <typeparam name="TDestination">The target type to cast to.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{object}}"/> of <c>object</c> items.</param>
    /// <returns>A list changeset stream of cast items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Cast{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination})"/>
    /// <seealso cref="CastToObject{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<TDestination>> Cast<TDestination>(this IObservable<IChangeSet<object>> source)
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => changes.Transform(t => (TDestination)t));
    }

    /// <summary>
    /// Transforms each item in the changeset using a conversion function.
    /// </summary>
    /// <typeparam name="TSource">The source item type.</typeparam>
    /// <typeparam name="TDestination">The destination item type.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource}}"/> to cast.</param>
    /// <param name="conversionFactory">A <see cref="Func{T, TResult}"/> function to convert each item from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.</param>
    /// <returns>A list changeset stream of converted items.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="conversionFactory"/> is <see langword="null"/>.</exception>
    /// <remarks>Use this overload when type inference requires explicit specification of both source and destination types. Alternatively, call <see cref="CastToObject{T}"/> first, then the single-type-parameter <see cref="Cast{TDestination}"/> overload.</remarks>
    /// <seealso cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/>
    /// <seealso cref="Transform{TSource, TDestination}(IObservable{IChangeSet{TSource}}, Func{TSource, TDestination}, bool)"/>
    public static IObservable<IChangeSet<TDestination>> Cast<TSource, TDestination>(this IObservable<IChangeSet<TSource>> source, Func<TSource, TDestination> conversionFactory)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Select(changes => changes.Transform(conversionFactory));
    }

    /// <summary>
    /// Casts each item in the changeset to <c>object</c>. Typically used before <see cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/> to work around type inference limitations.
    /// </summary>
    /// <typeparam name="T">The source item type (must be a reference type).</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to cast to object.</param>
    /// <returns>A list changeset stream of <c>object</c> items.</returns>
    /// <seealso cref="Cast{TDestination}(IObservable{IChangeSet{object}})"/>
    public static IObservable<IChangeSet<object>> CastToObject<T>(this IObservable<IChangeSet<T>> source)
        where T : class => source.Select(changes => changes.Transform(t => (object)t));

    /// <summary>
    /// Applies each changeset to the target list as a side effect, keeping it synchronized with the source.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to clone.</param>
    /// <param name="target">The <see cref="IList{T}"/> target list to clone changes into.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Lower-level than <see cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>. Uses <see cref="IList{T}"/>.Clone() to apply all changeset operations directly.</para>
    /// </remarks>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <seealso cref="PopulateInto{T}(IObservable{IChangeSet{T}}, ISourceList{T})"/>
    public static IObservable<IChangeSet<T>> Clone<T>(this IObservable<IChangeSet<T>> source, IList<T> target)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Do(target.Clone);
    }

    /// <summary>
    /// <para>Convert the object using the specified conversion function.</para>
    /// <para>This is a lighter equivalent of Transform and is designed to be used with non-disposable objects.</para>
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <typeparam name="TDestination">The type of the destination items.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to convert.</param>
    /// <param name="conversionFactory">The <see cref="Func{T, TResult}"/> conversion factory.</param>
    /// <returns>An observable which emits the change set.</returns>
    [Obsolete("Prefer Cast as it is does the same thing but is semantically correct")]
    public static IObservable<IChangeSet<TDestination>> Convert<TObject, TDestination>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TDestination> conversionFactory)
        where TObject : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Select(changes => changes.Transform(conversionFactory));
    }

    /// <summary>
    /// Flattens buffered changesets (e.g. from <see cref="System.Reactive.Linq.Observable.Buffer{TSource}(IObservable{TSource}, TimeSpan)"/>) back into single changesets.
    /// Empty buffers are dropped.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <see cref="IObservable{T}"/> of buffered changeset lists.</param>
    /// <returns>A list changeset stream with all buffered changes concatenated into single changesets.</returns>
    /// <remarks>
    /// <para>Use this after applying <c>Observable.Buffer()</c> to a changeset stream to re-merge the batched changesets into a single stream.</para>
    /// </remarks>
    /// <seealso cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, IScheduler?)"/>
    /// <seealso cref="BufferInitial{T}(IObservable{IChangeSet{T}}, TimeSpan, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> FlattenBufferResult<T>(this IObservable<IList<IChangeSet<T>>> source)
        where T : notnull => source.Where(x => x.Count != 0).Select(updates => new ChangeSet<T>(updates.SelectMany(u => u)));

    /// <summary>
    /// Invokes <paramref name="action"/> once for every <see cref="Change{T}"/> in each changeset. Range changes
    /// (AddRange, RemoveRange, Clear) are delivered as a single <see cref="Change{T}"/>; they are not flattened into per-item changes.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to observe each change in.</param>
    /// <param name="action">The action invoked for each <see cref="Change{T}"/>.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a side-effect operator. It does not modify the changeset. If you need each individual item from range operations flattened out, use <see cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/> instead.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/Replace/Remove/Moved/Refresh</term><description>Callback invoked with the <see cref="Change{T}"/> (single-item change). Changeset forwarded.</description></item>
    /// <item><term>AddRange/RemoveRange/Clear</term><description>Callback invoked once with the <see cref="Change{T}"/> containing the range (accessible via <c>Range</c> property). Changeset forwarded.</description></item>
    /// <item><term>OnError</term><description>If the callback throws, the exception propagates as OnError.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ForEachItemChange{TObject}(IObservable{IChangeSet{TObject}}, Action{ItemChange{TObject}})"/>
    /// <seealso cref="OnItemAdded{T}(IObservable{IChangeSet{T}}, Action{T})"/>
    /// <seealso cref="OnItemRemoved{T}(IObservable{IChangeSet{T}}, Action{T}, bool)"/>
    /// <seealso cref="ObservableCacheEx.ForEachChange{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Action{Change{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject>> ForEachChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<Change<TObject>> action)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.ForEach(action));
    }

    /// <summary>
    /// Invokes <paramref name="action"/> for every individual <see cref="ItemChange{TObject}"/> in each changeset.
    /// Range changes are flattened into individual item changes first, so the callback only receives Add, Replace, Remove, and Refresh.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to observe each item-level change in.</param>
    /// <param name="action">The <see cref="Action{ItemChange{TObject}}"/> action invoked for each individual item change.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ForEachChange{TObject}(IObservable{IChangeSet{TObject}}, Action{Change{TObject}})"/>, this operator flattens
    /// <b>AddRange</b>, <b>RemoveRange</b>, and <b>Clear</b> into individual <see cref="ItemChange{TObject}"/> entries before invoking the callback.
    /// </para>
    /// </remarks>
    /// <seealso cref="ForEachChange{TObject}(IObservable{IChangeSet{TObject}}, Action{Change{TObject}})"/>
    public static IObservable<IChangeSet<TObject>> ForEachItemChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<ItemChange<TObject>> action)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.Flatten().ForEach(action));
    }

    /// <summary>
    /// Reference-counted materialization of the source changeset stream into an <see cref="IObservableList{T}"/>.
    /// The shared list is created on the first subscriber and disposed when the last subscriber unsubscribes.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to share via reference counting.</param>
    /// <returns>A list changeset stream backed by a shared, reference-counted <see cref="IObservableList{T}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Equivalent to <c>Publish().RefCount()</c> for changeset streams. The underlying list is created lazily on first subscription.</para>
    /// </remarks>
    /// <seealso cref="AsObservableList{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> RefCount<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new RefCount<T>(source).Run();
    }

    /// <summary>
    /// Strips index information from all changes in the stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to strip index information.</param>
    /// <returns>A list changeset stream with all index values removed from changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Removes index positions from every change in each changeset. This is useful when downstream operators do not require or support index-based operations.</para>
    /// </remarks>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> RemoveIndex<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => new ChangeSet<T>(changes.YieldWithoutIndex()));
    }
}
