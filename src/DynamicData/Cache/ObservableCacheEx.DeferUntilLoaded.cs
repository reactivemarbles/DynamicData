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
    /// Suppresses all emissions until the first non-empty changeset arrives, then replays that changeset and all subsequent ones.
    /// If the source never produces a non-empty changeset, the stream waits indefinitely.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to defer until the first changeset arrives.</param>
    /// <returns>An observable that begins emitting changesets once the first non-empty changeset is received.</returns>
    /// <remarks>
    /// <para><b>Worth noting:</b> Blocks indefinitely if the cache or stream never receives any data. Ensure the source will eventually emit at least one changeset.</para>
    /// </remarks>
    /// <seealso cref="SkipInitial{TObject, TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new DeferUntilLoaded<TObject, TKey>(source).Run();
    }

    /// <inheritdoc cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<TObject, TKey>> DeferUntilLoaded<TObject, TKey>(this IObservableCache<TObject, TKey> source)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new DeferUntilLoaded<TObject, TKey>(source).Run();
    }
}
