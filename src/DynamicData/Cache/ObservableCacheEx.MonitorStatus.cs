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
    /// Monitors the source observable and emits <see cref="ConnectionStatus"/> values: <c>Pending</c> initially,
    /// <c>Loaded</c> when the first value arrives, <c>Errored</c> on error, and <c>Completed</c> on completion.
    /// This is not a changeset operator.
    /// </summary>
    /// <typeparam name="T">The type of the source observable.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> to monitor for connection status.</param>
    /// <returns>An observable that emits <see cref="ConnectionStatus"/> values reflecting the source's lifecycle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="DeferUntilLoaded{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<ConnectionStatus> MonitorStatus<T>(this IObservable<T> source) => new StatusMonitor<T>(source).Run();
}
