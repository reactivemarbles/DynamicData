// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Subscribes to the changeset stream and clones each changeset into the destination cache.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to pipe into a target cache.</param>
    /// <param name="destination">The <c>ISourceCache&lt;TObject, TKey&gt;</c> that will receive the changes.</param>
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
    /// <seealso><c>PopulateFrom&lt;TObject, TKey&gt;(ISourceCache&lt;TObject, TKey&gt;, IObservable&lt;IEnumerable&lt;TObject&gt;&gt;)</c></seealso>
    /// <seealso><c>AsObservableCache&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, bool)</c></seealso>
    /// <seealso><c>ObservableListEx.PopulateInto&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, ISourceList&lt;T&gt;)</c></seealso>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ISourceCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(destination);

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <summary>
    /// Provides an overload of <c>PopulateInto</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to pipe into a target cache.</param>
    /// <param name="destination">The <c>IIntermediateCache&lt;TObject, TKey&gt;</c> that will receive the changes.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>Overload that targets an <c>IIntermediateCache&lt;TObject, TKey&gt;</c>.</remarks>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IIntermediateCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(destination);

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }

    /// <summary>
    /// Provides an overload of <c>PopulateInto</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to pipe into a target cache.</param>
    /// <param name="destination">The <c>LockFreeObservableCache&lt;TObject, TKey&gt;</c> that will receive the changes.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>Overload that targets a <c>LockFreeObservableCache&lt;TObject, TKey&gt;</c>.</remarks>
    public static IDisposable PopulateInto<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, LockFreeObservableCache<TObject, TKey> destination)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(destination);

        return source.Subscribe(changes => destination.Edit(updater => updater.Clone(changes)));
    }
}
