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
    /// <para>
    /// Disposes items implementing <see cref="IDisposable"/> when they are removed or replaced,
    /// and disposes all tracked items when the stream completes, errors, or the subscription is disposed.
    /// </para>
    /// <para>
    /// Individual items are disposed <b>after</b> the changeset has been forwarded downstream, so downstream operators
    /// see the removal before disposal occurs. Items that do not implement <see cref="IDisposable"/> are ignored.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to track for disposal on removal.</param>
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
    /// On stream completion, error, or subscription disposal, all remaining tracked items are disposed.
    /// All disposal is synchronous via <see cref="IDisposable.Dispose()"/>.
    /// For items that implement <see cref="IAsyncDisposable"/>, use <see cref="AsyncDisposeMany{TObject,TKey}"/> instead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="AsyncDisposeMany{TObject,TKey}"/>
    /// <seealso cref="SubscribeMany{TObject,TKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IDisposable})"/>
    /// <seealso cref="ObservableListEx.DisposeMany"/>
    public static IObservable<IChangeSet<TObject, TKey>> DisposeMany<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DisposeMany<TObject, TKey>(source).Run();
    }
}
