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
/// ObservableCache extensions for populating caches.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Subscribes to the observable and calls <c>AddOrUpdate</c> on the source cache for each emitted batch of items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to operate on.</param>
    /// <param name="observable">The <see cref="IObservable{IEnumerable{TObject}}"/> that emits batches of items.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from <paramref name="observable"/>.</returns>
    /// <remarks>
    /// <para>Each emission from <paramref name="observable"/> is passed to <see cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TObject})"/>, producing one changeset per emission containing <b>Add</b> or <b>Update</b> events for each item. Errors from <paramref name="observable"/> propagate and terminate the subscription. Completion ends the subscription; the cache retains all items.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observable"/> is <see langword="null"/>.</exception>
    /// <seealso cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    /// <seealso cref="ToObservableChangeSet{TObject, TKey}(IObservable{IEnumerable{TObject}}, Func{TObject, TKey}, Func{TObject, TimeSpan?}, int, IScheduler?)"/>
    public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<IEnumerable<TObject>> observable)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return observable.Subscribe(source.AddOrUpdate);
    }

    /// <summary>
    /// Subscribes to the observable and calls <c>AddOrUpdate</c> on the source cache for each emitted item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to operate on.</param>
    /// <param name="observable">The <see cref="IObservable{TObject}"/> that emits individual items.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from <paramref name="observable"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observable"/> is <see langword="null"/>.</exception>
    public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<TObject> observable)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return observable.Subscribe(source.AddOrUpdate);
    }

    /// <summary>
    /// Subscribes to the changeset stream and clones each changeset into the destination cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to pipe into a target cache.</param>
    /// <param name="destination">The <see cref="ISourceCache{TObject, TKey}"/> that will receive the changes.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from the source.</returns>
    /// <remarks>
    /// <para>
    /// Each changeset from the source is applied to the destination cache inside an Edit call.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The item is added to the destination cache via AddOrUpdate.</description></item>
    /// <item><term>Update</term><description>The item is updated in the destination cache via AddOrUpdate.</description></item>
    /// <item><term>Remove</term><description>The item is removed from the destination cache.</description></item>
    /// <item><term>Refresh</term><description>A Refresh is issued on the destination cache for the item.</description></item>
    /// <item><term>OnError</term><description>The subscription is terminated. The destination cache is not rolled back.</description></item>
    /// <item><term>OnCompleted</term><description>The subscription ends. The destination cache retains all items.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <seealso cref="PopulateFrom{TObject, TKey}(ISourceCache{TObject, TKey}, IObservable{IEnumerable{TObject}})"/>
    /// <seealso cref="AsObservableCache{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, bool)"/>
    /// <seealso cref="ObservableListEx.PopulateInto{T}(IObservable{IChangeSet{T}}, ISourceList{T})"/>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ISourceCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <inheritdoc cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to pipe into a target cache.</param>
    /// <param name="destination">The <see cref="IIntermediateCache{TObject, TKey}"/> that will receive the changes.</param>
    /// <remarks>Overload that targets an <see cref="IIntermediateCache{TObject, TKey}"/>.</remarks>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IIntermediateCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <inheritdoc cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to pipe into a target cache.</param>
    /// <param name="destination">The <see cref="LockFreeObservableCache{TObject, TKey}"/> that will receive the changes.</param>
    /// <remarks>Overload that targets a <see cref="LockFreeObservableCache{TObject, TKey}"/>.</remarks>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, LockFreeObservableCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        destination.ThrowArgumentNullExceptionIfNull(nameof(destination));

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }
}
