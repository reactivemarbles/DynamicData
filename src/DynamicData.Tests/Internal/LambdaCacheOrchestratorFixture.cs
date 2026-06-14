// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using DynamicData.Cache.Internal;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// In-isolation smoke tests for <see cref="IntObservableCacheEx.LambdaCacheOrchestrator{TSource, TKey, TInner, TResult}"/>.
/// Verifies that calls forward to the captured lambdas and that the Track callback is wired to the
/// supplied context. Most production behavior is covered through the lambda-overload tests in
/// OrchestrateFixture; these tests confirm the forwarding contract in isolation.
/// </summary>
public sealed class LambdaCacheOrchestratorFixture
{
    private sealed record Item(int Id);

    [Fact]
    public void OnSourceChangeSet_ForwardsToLambdaWithContext()
    {
        var context = new FakeOrchestratorContext<int, string>();
        var emitter = new CollectingObserver<int>();
        var receivedChanges = new List<IChangeSet<Item, int>>();
        ICacheOrchestratorContext<int, string>? receivedContext = null;

        var orchestrator = new IntObservableCacheEx.LambdaCacheOrchestrator<Item, int, string, int>(
            context, emitter,
            onSourceChangeSet: (changes, ctx) =>
            {
                receivedChanges.Add(changes);
                receivedContext = ctx;
            },
            onInner: (value, key, _) => { },
            onDrainComplete: obs => { });

        var changeset = new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) };
        orchestrator.OnSourceChangeSet(changeset);

        receivedChanges.Should().HaveCount(1);
        receivedChanges[0].Should().BeSameAs(changeset);
        receivedContext.Should().BeSameAs(context, "the lambda overload forwards the captured context as-is");
    }

    [Fact]
    public void OnInner_ForwardsToLambdaWithEmitter()
    {
        var context = new FakeOrchestratorContext<int, string>();
        var emitter = new CollectingObserver<int>();
        var received = new List<(string Value, int Key)>();
        IObserver<int>? receivedEmitter = null;

        var orchestrator = new IntObservableCacheEx.LambdaCacheOrchestrator<Item, int, string, int>(
            context, emitter,
            onSourceChangeSet: (_, _) => { },
            onInner: (v, k, em) =>
            {
                received.Add((v, k));
                receivedEmitter = em;
            },
            onDrainComplete: _ => { });

        orchestrator.OnInner("hello", 42);

        received.Should().Equal(new[] { ("hello", 42) });
        receivedEmitter.Should().BeSameAs(emitter, "the lambda overload forwards the emitter as-is to onInner");
    }

    [Fact]
    public void OnDrainComplete_ForwardsEmitterToLambda()
    {
        var context = new FakeOrchestratorContext<int, string>();
        var emitter = new CollectingObserver<int>();
        IObserver<int>? receivedObserver = null;

        var orchestrator = new IntObservableCacheEx.LambdaCacheOrchestrator<Item, int, string, int>(
            context, emitter,
            onSourceChangeSet: (_, _) => { },
            onInner: (_, _, _) => { },
            onDrainComplete: obs => receivedObserver = obs);

        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        receivedObserver.Should().BeSameAs(emitter, "the lambda overload forwards the emitter as-is to onDrainComplete");
    }
}
