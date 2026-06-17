// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// <para>
    /// Disposes items implementing <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/> when they are removed or replaced,
    /// and disposes all tracked items when the stream completes, errors, or the subscription is disposed.
    /// </para>
    /// <para>
    /// Individual items are disposed <b>after</b> the changeset has been forwarded downstream, so downstream operators
    /// see the removal before disposal occurs. Items implementing neither disposal interface are ignored.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of items in the cache.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to track for async disposal on removal.</param>
    /// <param name="disposalsCompletedAccessor">
    /// <para>
    /// Invoked once per subscription, providing an <see cref="IObservable{Unit}"/> that signals when all
    /// <see cref="IAsyncDisposable.DisposeAsync()"/> calls have finished. The signal emits a single value
    /// and then completes.
    /// </para>
    /// <para>
    /// This is delivered on a separate channel from the main changeset stream so it can be observed even
    /// if the source stream errors.
    /// </para>
    /// </param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Tracks the item. No disposal.</description></item>
    ///   <item><term>Update</term><description>Disposes the <b>previous</b> value (if it differs by reference from the current). Tracks the new value.</description></item>
    ///   <item><term>Remove</term><description>Disposes the removed item.</description></item>
    ///   <item><term>Refresh</term><description>Passed through. No disposal.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// On stream completion, error, or subscription disposal, all items still in the cache are disposed.
    /// <see cref="IDisposable"/> items are disposed synchronously; <see cref="IAsyncDisposable"/> items
    /// are dispatched via the <paramref name="disposalsCompletedAccessor"/> signal.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="disposalsCompletedAccessor"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DisposeMany{TObject,TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> AsyncDisposeMany<TObject, TKey>(
                this IObservable<IChangeSet<TObject, TKey>> source,
                Action<IObservable<Unit>> disposalsCompletedAccessor)
            where TObject : notnull
            where TKey : notnull
        => Cache.Internal.AsyncDisposeMany<TObject, TKey>.Create(
            source: source,
            disposalsCompletedAccessor: disposalsCompletedAccessor);
}
