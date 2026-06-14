// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Cache.Internal;

namespace DynamicData.Tests.Utilities;

/// <summary>
/// In-memory implementation of <see cref="ICacheOrchestratorContext{TKey, TInner}"/> for testing
/// orchestrators in isolation, without spinning up the full SharedDeliveryQueue + CacheOrchestration
/// runtime. Records every <see cref="Track"/> and <see cref="Untrack"/> call so tests can assert on
/// the orchestrator's subscription lifecycle behavior, and exposes the currently tracked observables
/// via <see cref="Tracked"/>.
/// </summary>
/// <typeparam name="TKey">Source changeset key type.</typeparam>
/// <typeparam name="TInner">Value type emitted by per-key inner observables.</typeparam>
internal sealed class FakeOrchestratorContext<TKey, TInner> : ICacheOrchestratorContext<TKey, TInner>
    where TKey : notnull
    where TInner : notnull
{
    private readonly Dictionary<TKey, IObservable<TInner>> _tracked = [];

    /// <summary>Snapshot of every Track call made on this context, in order of receipt.</summary>
    public List<(TKey Key, IObservable<TInner> Observable)> TrackCalls { get; } = [];

    /// <summary>Snapshot of every Untrack call made on this context, in order of receipt.</summary>
    public List<TKey> UntrackCalls { get; } = [];

    /// <summary>Currently registered observables, keyed by their source key. Reflects Track/Untrack history.</summary>
    public IReadOnlyDictionary<TKey, IObservable<TInner>> Tracked => _tracked;

    public void Track(TKey key, IObservable<TInner> observable)
    {
        TrackCalls.Add((key, observable));
        _tracked[key] = observable;
    }

    public void Untrack(TKey key)
    {
        UntrackCalls.Add(key);
        _tracked.Remove(key);
    }

    /// <summary>
    /// Returns <paramref name="observable"/> unchanged. The fake does not provide queue-style
    /// serialization; tests that need ordering guarantees should drive the orchestrator
    /// synchronously from a single thread.
    /// </summary>
    public IObservable<T> Serialize<T>(IObservable<T> observable) => observable;
}
