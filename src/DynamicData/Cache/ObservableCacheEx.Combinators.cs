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
/// ObservableCache extensions for set-style combinators (And, Or, Xor, Except).
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Applied a logical And operator between the collections i.e items which are in all of the
    /// sources are included.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to combine.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{TObject, TKey}}"/> streams to combine with.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">source or others.</exception>
    /// <seealso cref="ObservableListEx.And"/>
    public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return others is null || others.Length == 0
            ? throw new ArgumentNullException(nameof(others))
            : source.Combine(CombineOperator.And, others);
    }

    /// <summary>
    /// Applied a logical And operator between the collections i.e items which are in all of the sources are included.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="ICollection{T}"/> of streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.And);
    }

    /// <summary>
    /// Dynamically apply a logical And operator between the items in the outer observable list.
    /// Items which are in all of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{T}"/> of streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.And);
    }

    /// <summary>
    /// Dynamically apply a logical And operator between the items in the outer observable list.
    /// Items which are in all of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{IObservableCache{TObject, TKey}}"/> of changeset streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.And);
    }

    /// <summary>
    /// Dynamically apply a logical And operator between the items in the outer observable list.
    /// Items which are in all of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{ISourceCache{TObject, TKey}}"/> of changeset streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> And<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.And);
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                var subscriber = connections.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(connections, subscriber);
            });
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                var subscriber = connections.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(connections, subscriber);
            });
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DynamicCombiner<TObject, TKey>(source, type).Run();
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                void UpdateAction(IChangeSet<TObject, TKey> updates)
                {
                    try
                    {
                        observer.OnNext(updates);
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                }

                var subscriber = Disposable.Empty;
                try
                {
                    var combiner = new Combiner<TObject, TKey>(type, UpdateAction);
                    subscriber = combiner.Subscribe([.. sources]);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                    observer.OnCompleted();
                }

                return subscriber;
            });
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, CombineOperator type, params IObservable<IChangeSet<TObject, TKey>>[] combineTarget)
        where TObject : notnull
        where TKey : notnull
    {
        combineTarget.ThrowArgumentNullExceptionIfNull(nameof(combineTarget));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                void UpdateAction(IChangeSet<TObject, TKey> updates)
                {
                    try
                    {
                        observer.OnNext(updates);
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                        observer.OnCompleted();
                    }
                }

                var subscriber = Disposable.Empty;
                try
                {
                    var list = combineTarget.ToList();
                    list.Insert(0, source);

                    var combiner = new Combiner<TObject, TKey>(type, UpdateAction);
                    subscriber = combiner.Subscribe([.. list]);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                    observer.OnCompleted();
                }

                return subscriber;
            });
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the collections
    /// Items from the first collection in the outer list are included unless contained in any of the other lists.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to combine.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{TObject, TKey}}"/> streams to combine with.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    /// <seealso cref="ObservableListEx.Except"/>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (others is null || others.Length == 0)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.Except, others);
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the collections
    /// Items from the first collection in the outer list are included unless contained in any of the other lists.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="ICollection{T}"/> of streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Except);
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the collections
    /// Items from the first collection in the outer list are included unless contained in any of the other lists.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{T}"/> of streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Except);
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{IObservableCache{TObject, TKey}}"/> of changeset streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Except);
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{ISourceCache{TObject, TKey}}"/> of changeset streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Except);
    }

    /// <summary>
    /// Combines multiple changeset streams using logical OR (union). An item appears downstream if it exists in any source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to combine.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{TObject, TKey}}"/> streams to combine with.</param>
    /// <returns>A changeset stream containing items present in any of the sources.</returns>
    /// <remarks>
    /// <para>
    /// Items are tracked via reference counting across all sources. An item appears downstream as long as
    /// at least one source contains it. When the last source holding a key removes it, the item is removed downstream.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If this is the first source to provide the key, an <b>Add</b> is emitted. If other sources already have the key, the reference count is incremented but no emission occurs.</description></item>
    /// <item><term>Update</term><description>If the item is currently downstream, an <b>Update</b> is emitted.</description></item>
    /// <item><term>Remove</term><description>Reference count decremented. If the count reaches zero (no source holds the key), a <b>Remove</b> is emitted. Otherwise no emission.</description></item>
    /// <item><term>Refresh</term><description>If the item is downstream, a <b>Refresh</b> is forwarded.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="others"/> is <see langword="null"/>.</exception>
    /// <seealso cref="And{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="Except{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="Xor{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="MergeChangeSets{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <seealso cref="ObservableListEx.Or"/>
    public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (others is null || others.Length == 0)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.Or, others);
    }

    /// <inheritdoc cref="Or{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <param name="sources">The <see cref="ICollection{T}"/> of streams to combine.</param>
    /// <remarks>This overload accepts a pre-built collection of sources instead of a params array.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Or);
    }

    /// <summary>
    /// Dynamically apply a logical Or operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{T}"/> of streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Or);
    }

    /// <summary>
    /// Dynamically apply a logical Or operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{IObservableCache{TObject, TKey}}"/> of changeset streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Or);
    }

    /// <summary>
    /// Dynamically apply a logical Or operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{ISourceCache{TObject, TKey}}"/> of changeset streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Or<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Or);
    }

    /// <summary>
    /// Combines multiple changeset streams using logical XOR (symmetric difference).
    /// An item appears downstream only if it exists in exactly one source.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to combine.</param>
    /// <param name="others">The additional <see cref="IObservable{IChangeSet{TObject, TKey}}"/> streams to combine with.</param>
    /// <returns>A changeset stream containing items present in exactly one source.</returns>
    /// <remarks>
    /// <para>
    /// Items are tracked via reference counting. An item appears downstream only when exactly one
    /// source holds it. Adding the same key from a second source removes it from the result;
    /// removing from that second source restores it.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>If the key is now held by exactly one source, an <b>Add</b> is emitted. If adding causes the count to reach 2+, a <b>Remove</b> is emitted (the item is no longer exclusive).</description></item>
    /// <item><term>Update</term><description>If the item is currently downstream (count is 1), an <b>Update</b> is emitted.</description></item>
    /// <item><term>Remove</term><description>Reference count decremented. If the count drops to exactly 1, an <b>Add</b> is emitted (the item is now exclusive to one source). If it drops to 0, a <b>Remove</b> is emitted.</description></item>
    /// <item><term>Refresh</term><description>If the item is downstream, a <b>Refresh</b> is forwarded.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="others"/> is <see langword="null"/>.</exception>
    /// <seealso cref="And{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="Or{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="Except{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <seealso cref="ObservableListEx.Xor"/>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (others is null || others.Length == 0)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.Xor, others);
    }

    /// <inheritdoc cref="Xor{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{IChangeSet{TObject, TKey}}[])"/>
    /// <param name="sources">The <see cref="ICollection{T}"/> of streams to combine.</param>
    /// <remarks>This overload accepts a pre-built collection of sources instead of a params array.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Xor);
    }

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are only in one of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{T}"/> of streams to combine.</param>
    /// <returns>An observable which emits a change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Xor);
    }

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{IObservableCache{TObject, TKey}}"/> of changeset streams to combine.</param>
    /// <returns>An observable which emits a change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Xor);
    }

    /// <summary>
    /// Dynamically apply a logical Xor operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <see cref="IObservableList{ISourceCache{TObject, TKey}}"/> of changeset streams to combine.</param>
    /// <returns>An observable which emits a change set.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Xor<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return sources.Combine(CombineOperator.Xor);
    }
}
