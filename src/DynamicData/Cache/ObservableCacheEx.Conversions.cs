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
/// ObservableCache extensions for type and shape conversions.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Wraps an <see cref="IObservableCache{TObject, TKey}"/> in a read-only facade, hiding the mutable API.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="IObservableCache{TObject, TKey}"/> to operate on.</param>
    /// <returns>A read-only <see cref="IObservableCache{TObject, TKey}"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AsObservableCache{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, bool)"/>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new AnonymousObservableCache<TObject, TKey>(source);
    }

    /// <summary>
    /// Materializes a changeset stream into a queryable, read-only <see cref="IObservableCache{TObject, TKey}"/>.
    /// The cache subscribes to the source on first access and maintains a live snapshot of all items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to materialize into a read-only cache.</param>
    /// <param name="applyLocking">If <see langword="true"/> (default), all cache operations are synchronized. Set to <see langword="false"/> when the caller guarantees single-threaded access.</param>
    /// <returns>A read-only observable cache that reflects the current state of the pipeline.</returns>
    /// <remarks>
    /// <para>
    /// Disposing the returned cache unsubscribes from the source stream. The cache's <c>Connect()</c>
    /// method provides a changeset stream of its own, which re-emits the current state on each new subscriber.
    /// </para>
    /// <para>When <paramref name="applyLocking"/> is <see langword="false"/>, a <see cref="LockFreeObservableCache{TObject, TKey}"/> is used internally.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AsObservableCache{TObject, TKey}(IObservableCache{TObject, TKey})"/>
    /// <seealso cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    public static IObservableCache<TObject, TKey> AsObservableCache<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, bool applyLocking = true)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (applyLocking)
        {
            return new AnonymousObservableCache<TObject, TKey>(source);
        }

        return new LockFreeObservableCache<TObject, TKey>(source);
    }

    /// <summary>
    /// Casts each item in the changeset to a new type using the provided converter function.
    /// Equivalent to <see cref="Transform{TDestination, TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TDestination}, bool)"/>
    /// but named for discoverability when a simple type cast or conversion is needed.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TSource, TKey}}"/> to cast.</param>
    /// <param name="converter">The <see cref="Func{TSource, TDestination}"/> conversion function applied to each item.</param>
    /// <returns>An observable changeset of converted items.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="converter"/> and emits an <b>Add</b> with the converted item.</description></item>
    /// <item><term>Update</term><description>Calls <paramref name="converter"/> on the new value and emits an <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Emits a <b>Remove</b>. The converter is not called.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b>. The converter is not called.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="OfType{TObject, TKey, TDestination}"/>
    public static IObservable<IChangeSet<TDestination, TKey>> Cast<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new Cast<TSource, TKey, TDestination>(source, converter).Run();
    }

    /// <summary>
    /// Re-keys each item in the changeset by applying <paramref name="keySelector"/> to the current item.
    /// The original change reason is preserved; only the key is remapped.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TSourceKey">The type of the source key.</typeparam>
    /// <typeparam name="TDestinationKey">The type of the destination key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TSourceKey}}"/> to re-key.</param>
    /// <param name="keySelector">The <see cref="Func{TObject, TDestinationKey}"/> that computes the destination key from the item, e.g. <c>(item) =&gt; item.NewId</c>.</param>
    /// <returns>An observable changeset with items re-keyed using <paramref name="keySelector"/>.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description><paramref name="keySelector"/> is called on the item. An <b>Add</b> is emitted with the destination key.</description></item>
    /// <item><term>Update</term><description><paramref name="keySelector"/> is called on the current item. An <b>Update</b> is emitted with the destination key. If the key selector produces a different destination key for the updated value than it did for the original value, downstream consumers will see an <b>Update</b> for a key that may not match the original <b>Add</b>.</description></item>
    /// <item><term>Remove</term><description><paramref name="keySelector"/> is called on the item. A <b>Remove</b> is emitted with the destination key.</description></item>
    /// <item><term>Refresh</term><description><paramref name="keySelector"/> is called on the item. A <b>Refresh</b> is emitted with the destination key.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Transform{TDestination, TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TDestination}, bool)"/>
    public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TObject, TDestinationKey> keySelector)
        where TObject : notnull
        where TSourceKey : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return source.Select(
            updates =>
            {
                var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Current), u.Current, u.Previous));
                return new ChangeSet<TObject, TDestinationKey>(changed);
            });
    }

    /// <inheritdoc cref="ChangeKey{TObject, TSourceKey, TDestinationKey}(IObservable{IChangeSet{TObject, TSourceKey}}, Func{TObject, TDestinationKey})"/>
    /// <remarks>
    /// This overload also provides the source key to <paramref name="keySelector"/>,
    /// allowing the destination key to be derived from both the item and its original key.
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TDestinationKey>> ChangeKey<TObject, TSourceKey, TDestinationKey>(this IObservable<IChangeSet<TObject, TSourceKey>> source, Func<TSourceKey, TObject, TDestinationKey> keySelector)
        where TObject : notnull
        where TSourceKey : notnull
        where TDestinationKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return source.Select(
            updates =>
            {
                var changed = updates.Select(u => new Change<TObject, TDestinationKey>(u.Reason, keySelector(u.Key, u.Current), u.Current, u.Previous));
                return new ChangeSet<TObject, TDestinationKey>(changed);
            });
    }

    /// <summary>
    /// Obsolete: use <see cref="Transform{TDestination, TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TDestination}, bool)"/> instead.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to convert.</param>
    /// <param name="conversionFactory">The <see cref="Func{TObject, TDestination}"/> conversion factory.</param>
    /// <returns>An observable which emits change sets.</returns>
    [Obsolete("This was an experiment that did not work. Use Transform instead")]
    public static IObservable<IChangeSet<TDestination, TKey>> Convert<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TDestination> conversionFactory)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        conversionFactory.ThrowArgumentNullExceptionIfNull(nameof(conversionFactory));

        return source.Select(
            changes =>
            {
                var transformed = changes.Select(change => new Change<TDestination, TKey>(change.Reason, change.Key, conversionFactory(change.Current), change.Previous.Convert(conversionFactory), change.CurrentIndex, change.PreviousIndex));
                return new ChangeSet<TDestination, TKey>(transformed);
            });
    }

    /// <summary>
    /// Unwraps each <see cref="IChangeSet{TObject, TKey}"/> into individual <see cref="Change{TObject, TKey}"/>
    /// values via <see cref="Observable.SelectMany{TSource, TResult}(IObservable{TSource}, Func{TSource, IEnumerable{TResult}})"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to flatten into individual changes.</param>
    /// <returns>An observable of individual <see cref="Change{TObject, TKey}"/> values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="ForEachChange{TObject, TKey}"/>
    public static IObservable<Change<TObject, TKey>> Flatten<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.SelectMany(changes => changes);
    }

    /// <summary>
    /// Merges a list of changesets (typically from an Rx <c>Buffer</c> operation) into a single changeset
    /// by concatenating all changes. Empty buffers are filtered out.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> to flatten.</param>
    /// <returns>An observable changeset combining all changes from each buffer into a single emission.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> FlattenBufferResult<TObject, TKey>(this IObservable<IList<IChangeSet<TObject, TKey>>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Where(x => x.Count != 0).Select(updates => new ChangeSet<TObject, TKey>(updates.SelectMany(u => u)));
    }

    /// <summary>
    /// Filters and casts items in the changeset to <typeparamref name="TDestination"/>. Items that are not of type
    /// <typeparamref name="TDestination"/> are excluded. Combines filter and transform in one step without an intermediate cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the objects in the source changeset.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The destination type to filter and cast to.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to filter by type.</param>
    /// <param name="suppressEmptyChangeSets">If <see langword="true"/>, changesets that become empty after filtering are suppressed.</param>
    /// <returns>An observable changeset of <typeparamref name="TDestination"/> items.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>If the item is <typeparamref name="TDestination"/>, cast and emit as <b>Add</b>. Otherwise dropped.</description></item>
    ///   <item><term><b>Update</b></term><description>Re-evaluated. If the new item is <typeparamref name="TDestination"/>, emit accordingly. If the old item was downstream but the new one is not, emit <b>Remove</b>.</description></item>
    ///   <item><term><b>Remove</b></term><description>If the item was downstream, emit <b>Remove</b>.</description></item>
    ///   <item><term><b>Refresh</b></term><description>If the item is downstream, forwarded as <b>Refresh</b>.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> OfType<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, bool suppressEmptyChangeSets = true)
        where TObject : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new OfType<TObject, TKey, TDestination>(source, suppressEmptyChangeSets).Run();
    }

    /// <summary>
    /// Cache-aware equivalent of <c>Publish().RefCount()</c>. An internal cache is created on the first subscriber
    /// and disposed when the last subscriber unsubscribes. All subscribers share the same upstream subscription.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to share via reference counting.</param>
    /// <returns>A ref-counted observable changeset stream.</returns>
    /// <seealso cref="AsObservableCache{TObject,TKey}(IObservable{IChangeSet{TObject, TKey}}, bool)"/>
    public static IObservable<IChangeSet<TObject, TKey>> RefCount<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new RefCount<TObject, TKey>(source).Run();
    }
}
