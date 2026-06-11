// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Linq;
using System.Threading.Tasks;

using DynamicData.Cache.Internal;
using DynamicData.Kernel;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// In-isolation tests for <see cref="TransformManyAsync{TSource, TKey, TDestination, TDestinationKey}.Orchestrator"/>.
/// Verifies that async transformer results are tracked per key, that removals untrack and clean the
/// merge tracker, that drain end flushes accumulated changes, and that a transformer exception is
/// routed through the user-provided error handler.
/// </summary>
public sealed class TransformManyAsyncOrchestratorFixture
{
    private sealed record Item(int Id);

    [Fact]
    public void OnItemAdded_TracksTransformedObservable()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = new TransformManyAsync<Item, int, string, string>.Orchestrator(
            context, emitter,
            transformer: (item, key) => Task.FromResult<IObservable<IChangeSet<string, string>>>(
                Observable.Empty<IChangeSet<string, string>>()),
            equalityComparer: null,
            comparer: null,
            errorHandler: null);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });

        context.TrackCalls.Should().HaveCount(1);
        context.TrackCalls[0].Key.Should().Be(1);
    }

    [Fact]
    public void OnItemRemoved_UntracksAndDropsFromTracker()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = new TransformManyAsync<Item, int, string, string>.Orchestrator(
            context, emitter,
            transformer: (item, key) => Task.FromResult<IObservable<IChangeSet<string, string>>>(
                Observable.Empty<IChangeSet<string, string>>()),
            equalityComparer: null,
            comparer: null,
            errorHandler: null);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Remove, 1, new Item(1)) });

        context.UntrackCalls.Should().Equal(new[] { 1 });
    }

    [Fact]
    public void OnDrainComplete_EmitsAccumulatedTrackerState()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        var orchestrator = new TransformManyAsync<Item, int, string, string>.Orchestrator(
            context, emitter,
            transformer: (item, key) => Task.FromResult<IObservable<IChangeSet<string, string>>>(
                Observable.Empty<IChangeSet<string, string>>()),
            equalityComparer: null,
            comparer: null,
            errorHandler: null);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });
        orchestrator.OnInner(new ChangeSet<string, string> { new(ChangeReason.Add, "x", "value-x") }, 1);

        orchestrator.OnDrainComplete(isFinal: false, wasReentrant: false);

        emitter.Values.Should().HaveCount(1);
        emitter.Values[0].Should().ContainSingle(c => c.Key == "x");
    }

    [Fact]
    public async Task TransformerThrows_RoutesThroughErrorHandler()
    {
        var context = new FakeOrchestratorContext<int, IChangeSet<string, string>>();
        var emitter = new CollectingObserver<IChangeSet<string, string>>();
        Error<Item, int>? capturedError = null;

        var orchestrator = new TransformManyAsync<Item, int, string, string>.Orchestrator(
            context, emitter,
            transformer: (item, key) => throw new InvalidOperationException("transformer-broke"),
            equalityComparer: null,
            comparer: null,
            errorHandler: err => capturedError = err);

        orchestrator.OnSourceChangeSet(new ChangeSet<Item, int> { new(ChangeReason.Add, 1, new Item(1)) });

        // The deferred transform subscribes when we drive the tracked observable; subscribe now and
        // settle the asynchronous defer to surface the error to the handler.
        var trackedObs = context.Tracked[1];
        using var sub = trackedObs.Subscribe();
        await Task.Delay(20);

        capturedError.Should().NotBeNull();
        capturedError!.Exception!.Message.Should().Be("transformer-broke");
    }
}
