// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Cache.Internal;

namespace DynamicData.Tests.Utilities;

/// <summary>
/// Test fake for <see cref="ICacheOrchestratorContext{TKey, TInner}"/> that records
/// <see cref="Track"/> and <see cref="Untrack"/> calls and exposes the currently registered
/// observables via <see cref="Tracked"/>.
/// </summary>
/// <typeparam name="TKey">Source changeset key type.</typeparam>
/// <typeparam name="TInner">Value type emitted by per-key inner observables.</typeparam>
internal sealed class FakeOrchestratorContext<TKey, TInner> : ICacheOrchestratorContext<TKey, TInner>
    where TKey : notnull
    where TInner : notnull
{
    private readonly Dictionary<TKey, IObservable<TInner>> _tracked = [];

    public List<(TKey Key, IObservable<TInner> Observable)> TrackCalls { get; } = [];

    public List<TKey> UntrackCalls { get; } = [];

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

    public IObservable<T> Serialize<T>(IObservable<T> observable) => observable;
}
