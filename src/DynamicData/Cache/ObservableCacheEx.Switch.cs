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
    /// <inheritdoc cref="Switch{TObject, TKey}(IObservable{IObservable{IChangeSet{TObject, TKey}}})"/>
    /// <param name="sources">An observable that emits <see cref="IObservableCache{TObject, TKey}"/> instances.</param>
    /// <remarks>Overload that accepts observable caches. Internally calls <c>Connect()</c> on each cache and delegates to the changeset overload.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Select(cache => cache.Connect()).Switch();
    }

    /// <summary>
    /// Subscribes to the latest inner changeset stream, unsubscribing from the previous one on each switch.
    /// When switching, the old source's items are removed and the new source's items are added.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">An <see cref="IObservable{T}"/> of <see cref="IObservable{T}"/> changeset streams. The operator subscribes to the latest inner stream.</param>
    /// <returns>A changeset stream reflecting the items from the most recently emitted inner source.</returns>
    /// <remarks>
    /// <para>On switch: <b>Remove</b> is emitted for all items from the previous source, then <b>Add</b> for all items from the new source.</para>
    /// <para><b>Worth noting:</b> Each switch clears the entire downstream cache before populating from the new source. Subscribers see a full remove-then-add reset on every switch.</para>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Switch<TObject, TKey>(this IObservable<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return new Switch<TObject, TKey>(sources).Run();
    }
}
