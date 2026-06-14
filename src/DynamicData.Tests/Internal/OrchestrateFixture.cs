// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using Bogus;

using DynamicData.Cache.Internal;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// Tests for the <c>Orchestrate</c> primitive's behavioral contracts: source/inner serialization,
/// per-drain coalesced emission, completion counting, error propagation, and cross-cache safety.
/// Exercised via the <see cref="ICacheOrchestrator{TSource, TKey, TInner, TResult}"/> overload because it
/// maps 1:1 to the legacy CacheParentSubscription subclass shape these tests originally targeted.
/// </summary>
public sealed class OrchestrateFixture
{
    private const int SeedMin = 1;
    private const int SeedMax = 10000;
    private const int BatchSizeMin = 2;
    private const int BatchSizeMax = 8;

    private readonly Randomizer _rand = new(55);

    /// <summary>Test item with a typed key.</summary>
    private sealed record TestItem(int Key, string Value);

    /// <summary>
    /// Wires <paramref name="source"/> through <c>Orchestrate</c> with a fresh <see cref="TestOrchestrator"/>
    /// constructed per subscription. Returns the observable plus a thunk that yields the constructed
    /// orchestrator after subscribe. The factory pattern ensures per-subscription isolation and
    /// matches the production Orchestrate contract.
    /// </summary>
    private static (IObservable<IChangeSet<TestItem, int>> Observable, Func<TestOrchestrator> Orchestrator) Wire(
            IObservable<IChangeSet<TestItem, int>> source,
            Func<int, IObservable<string>>? childFactory = null,
            Action? onParent = null,
            Action? onChild = null)
    {
        TestOrchestrator? captured = null;
        var observable = source.Orchestrate<TestItem, int, string, IChangeSet<TestItem, int>, TestOrchestrator>(
            (ctx, em) => captured = new TestOrchestrator(ctx, em, childFactory, onParent, onChild));
        return (observable, () => captured ?? throw new InvalidOperationException("Subscribe to the returned observable first."));
    }

    [Fact]
    public void ParentOnNext_CalledForEachChangeSet()
    {
        var itemCount = _rand.Number(BatchSizeMin, BatchSizeMax);
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var observer = new TestObserver();
        var (observable, getOrchestrator) = Wire(source.Connect());
        using var sub = observable.Subscribe(observer);

        var items = Enumerable.Range(0, itemCount)
            .Select(i => new TestItem(_rand.Number(SeedMin, SeedMax) + i * 100, _rand.String2(_rand.Number(3, 10))))
            .ToList();

        foreach (var item in items)
            source.AddOrUpdate(item);

        getOrchestrator().ParentCallCount.Should().Be(items.Count, "OnSourceChangeSet should fire once per changeset");
        observer.EmitCount.Should().Be(items.Count, "Emit should fire after each parent update");
    }

    [Fact]
    public void ChildOnNext_CalledForEachEmission()
    {
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        var (observable, getOrchestrator) = Wire(source.Connect(), key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        using var sub = observable.Subscribe(observer);

        var key = _rand.Number(SeedMin, SeedMax);
        source.AddOrUpdate(new TestItem(key, "parent"));

        childSubjects.Should().HaveCount(1);
        var childValue = _rand.String2(_rand.Number(5, 15));
        childSubjects[0].OnNext(childValue);

        getOrchestrator().ChildCalls.Should().ContainSingle()
            .Which.Should().Be((childValue, key));
    }

    [Fact]
    public void EmitChanges_FiresOnceForBatch()
    {
        var batchSize = _rand.Number(BatchSizeMin, BatchSizeMax);
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var observer = new TestObserver();
        var (observable, getOrchestrator) = Wire(source.Connect());
        using var sub = observable.Subscribe(observer);

        source.Edit(updater =>
        {
            for (var i = 0; i < batchSize; i++)
                updater.AddOrUpdate(new TestItem(i + 1, _rand.String2(_rand.Number(3, 8))));
        });

        var orchestrator = getOrchestrator();
        orchestrator.ParentCallCount.Should().Be(1, "single batch = single OnSourceChangeSet");
        orchestrator.EmitCallCount.Should().Be(1, "single batch = single Emit");
    }

    [Fact]
    public void Batching_ChildUpdatesSettleBeforeEmit()
    {
        var batchSize = _rand.Number(BatchSizeMin, BatchSizeMax);
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var observer = new TestObserver();
        var childCount = 0;
        var (observable, getOrchestrator) = Wire(source.Connect(), key =>
        {
            Interlocked.Increment(ref childCount);
            return new BehaviorSubject<string>($"sync-{key}");
        });
        using var sub = observable.Subscribe(observer);

        source.Edit(updater =>
        {
            for (var i = 0; i < batchSize; i++)
                updater.AddOrUpdate(new TestItem(i + 1, _rand.String2(_rand.Number(3, 8))));
        });

        childCount.Should().Be(batchSize, "each item should create a child");
        getOrchestrator().EmitCallCount.Should().BeGreaterThanOrEqualTo(1,
            "Emit fires after parent + children settle");
    }

    [Fact]
    public void Completion_RequiresParentAndAllChildren()
    {
        using var source = new TestSourceCache<TestItem, int>(x => x.Key);
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        var (observable, _) = Wire(source.Connect(), key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        using var sub = observable.Subscribe(observer);

        source.AddOrUpdate(new TestItem(_rand.Number(SeedMin, SeedMax), "item"));
        childSubjects.Should().HaveCount(1);

        source.Complete();
        observer.IsCompleted.Should().BeFalse("parent complete but child still active");

        childSubjects[0].OnCompleted();
        observer.IsCompleted.Should().BeTrue("OnCompleted fires when parent + all children complete");
    }

    [Fact]
    public void Completion_ParentOnly_NoChildren()
    {
        using var source = new TestSourceCache<TestItem, int>(x => x.Key);
        var observer = new TestObserver();
        var (observable, _) = Wire(source.Connect());
        using var sub = observable.Subscribe(observer);

        source.Complete();
        observer.IsCompleted.Should().BeTrue("immediate OnCompleted when no children");
    }

    [Fact]
    public void OnDrainComplete_IsFinalIsFalseUntilSourceAndAllInnersComplete()
    {
        using var source = new TestSourceCache<TestItem, int>(x => x.Key);
        var childSubject = new Subject<string>();
        var observer = new TestObserver();
        var (observable, getOrchestrator) = Wire(source.Connect(), _ => childSubject);
        using var sub = observable.Subscribe(observer);

        // Activity while source + inner are alive
        source.AddOrUpdate(new TestItem(_rand.Number(SeedMin, SeedMax), "item"));
        childSubject.OnNext("v1");

        var orchestrator = getOrchestrator();
        orchestrator.IsFinalLog.Should().NotBeEmpty("OnDrainComplete should fire while source is active");
        orchestrator.IsFinalLog.Should().AllBeEquivalentTo(false,
            "isFinal must be false on every call while source and inners are still active");

        // Source completes; inner still alive — isFinal must remain false
        var preSourceCompleteCount = orchestrator.IsFinalLog.Count;
        source.Complete();
        orchestrator.IsFinalLog.Skip(preSourceCompleteCount).Should().AllBeEquivalentTo(false,
            "isFinal must remain false while at least one inner subscription is still active");
        observer.IsCompleted.Should().BeFalse("downstream must not complete while inners are active");

        // Final inner completes — at least one subsequent OnDrainComplete must observe isFinal=true
        var preInnerCompleteCount = orchestrator.IsFinalLog.Count;
        childSubject.OnCompleted();
        orchestrator.IsFinalLog.Skip(preInnerCompleteCount).Should().Contain(true,
            "isFinal must be true on the OnDrainComplete fired after source and all tracked inners have completed");
        observer.IsCompleted.Should().BeTrue("downstream completion must follow isFinal=true");
    }

    [Fact]
    public void Disposal_StopsAllEmissions()
    {
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        var (observable, _) = Wire(source.Connect(), key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        var sub = observable.Subscribe(observer);

        source.AddOrUpdate(new TestItem(_rand.Number(SeedMin, SeedMax), "item"));
        var emitsBefore = observer.EmitCount;

        sub.Dispose();

        source.AddOrUpdate(new TestItem(_rand.Number(SeedMin + SeedMax, SeedMax * 2), "after"));
        if (childSubjects.Count > 0)
            childSubjects[0].OnNext("after-dispose");

        observer.EmitCount.Should().Be(emitsBefore, "no emissions after disposal");
    }

    [Fact]
    public void Error_Propagates()
    {
        using var source = new TestSourceCache<TestItem, int>(x => x.Key);
        var observer = new TestObserver();
        var (observable, _) = Wire(source.Connect());
        using var sub = observable.Subscribe(observer);

        var error = new InvalidOperationException("test error");
        source.SetError(error);

        observer.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void InnerError_PropagatesAndTerminatesStream()
    {
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        var (observable, _) = Wire(source.Connect(), key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        using var sub = observable.Subscribe(observer);

        source.AddOrUpdate(new TestItem(_rand.Number(SeedMin, SeedMax), "item"));
        childSubjects.Should().HaveCount(1);

        var error = new InvalidOperationException("inner-error");
        childSubjects[0].OnError(error);

        observer.Error.Should().BeSameAs(error,
            "an inner observable error terminates the merged stream with the same error");
        observer.IsCompleted.Should().BeFalse(
            "an errored stream must not also complete");
    }

    [Fact]
    public void SourceAlreadyCompleted_PropagatesCompletion()
    {
        var observer = new TestObserver();
        var (observable, _) = Wire(Observable.Empty<IChangeSet<TestItem, int>>());
        using var sub = observable.Subscribe(observer);

        observer.IsCompleted.Should().BeTrue(
            "a pre-completed source must propagate completion through the orchestrator on subscribe");
    }

    [Fact]
    public void SourceAlreadyErrored_PropagatesError()
    {
        var observer = new TestObserver();
        var error = new InvalidOperationException("sync-error");
        var (observable, _) = Wire(Observable.Throw<IChangeSet<TestItem, int>>(error));
        using var sub = observable.Subscribe(observer);

        observer.Error.Should().BeSameAs(error,
            "a synchronously-erroring source must propagate the error through the orchestrator");
    }

    [Fact]
    public void Serialization_ParentAndChildDoNotInterleave()
    {
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var callLog = new List<string>();
        var observer = new TestObserver();
        var (observable, _) = Wire(
            source.Connect(),
            childFactory: key => new Subject<string>(),
            onParent: () => { lock (callLog) callLog.Add("P-start"); Thread.Sleep(1); lock (callLog) callLog.Add("P-end"); },
            onChild: () => { lock (callLog) callLog.Add("C-start"); Thread.Sleep(1); lock (callLog) callLog.Add("C-end"); });
        using var sub = observable.Subscribe(observer);

        source.AddOrUpdate(new TestItem(_rand.Number(SeedMin, SeedMax), "item"));

        // Start/end pairs should not interleave
        for (var i = 0; i + 1 < callLog.Count; i += 2)
        {
            var prefix = callLog[i].Split('-')[0];
            callLog[i + 1].Should().StartWith(prefix, "operations should not interleave");
        }
    }

    /// <summary>
    /// Proves Orchestrate delivery runs without holding the lock. Two orchestrator instances
    /// whose Emit callbacks write into each other's source cache, creating a cross-cache cycle.
    /// Deadlocks if downstream delivery is held under the queue lock; passes when the queue is
    /// drained before invoking Emit.
    /// </summary>
    [Trait("Category", "ExplicitDeadlock")]
    [Fact]
    public async Task DeadlockProof_CrossFeedingSubscriptions()
    {
        var iterations = _rand.Number(50, 150);

        using var sourceA = new SourceCache<TestItem, int>(x => x.Key);
        using var sourceB = new SourceCache<TestItem, int>(x => x.Key);

        var observerA = new CrossFeedObserver(sourceB, 100_001, iterations);
        var (observableA, _) = Wire(sourceA.Connect());
        using var subA = observableA.Subscribe(observerA);

        var observerB = new CrossFeedObserver(sourceA, 200_001, iterations);
        var (observableB, _) = Wire(sourceB.Connect());
        using var subB = observableB.Subscribe(observerB);

        using var barrier = new Barrier(2);

        var taskA = Task.Run(() =>
        {
            var tRand = new Randomizer(56);
            barrier.SignalAndWait();
            for (var i = 0; i < iterations; i++)
                sourceA.AddOrUpdate(new TestItem(tRand.Number(1, 50_000), tRand.String2(5)));
        });

        var taskB = Task.Run(() =>
        {
            var tRand = new Randomizer(57);
            barrier.SignalAndWait();
            for (var i = 0; i < iterations; i++)
                sourceB.AddOrUpdate(new TestItem(tRand.Number(50_001, 100_000), tRand.String2(5)));
        });

        var completed = Task.WhenAll(taskA, taskB);
        var finished = await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(30)));
        finished.Should().BeSameAs(completed,
            "cross-feeding Orchestrate subscriptions should not deadlock");
    }

    /// <summary>
    /// Concurrent source/inner emissions during the orchestrator's per-drain emit must not be
    /// lost: items delivered via the reentrant drain inside Emitter.OnNext settle into the
    /// orchestrator's state and must be flushed before drain exits, otherwise downstream
    /// observers miss them.
    /// </summary>
    [Fact]
    public async Task ReentrantDrain_ConcurrentInnerEmissions_AllItemsReachDownstream()
    {
        var producerCount = 8;
        var emissionsPerProducer = 200;
        var totalEmissions = producerCount * emissionsPerProducer;

        using var source = new SourceCache<TestItem, int>(x => x.Key);

        // Per-source-item inner subject so each producer task has a stable inner stream to push into.
        var innerSubjects = new Dictionary<int, Subject<string>>();
        for (var i = 1; i <= producerCount; i++)
        {
            innerSubjects[i] = new Subject<string>();
        }

        var (observable, getOrchestrator) = Wire(source.Connect(), key => innerSubjects[key]);
        var observer = new TestObserver();
        using var sub = observable.Subscribe(observer);

        // Add a source item per producer to subscribe each inner subject.
        for (var i = 1; i <= producerCount; i++)
        {
            source.AddOrUpdate(new TestItem(i, "init"));
        }

        var orchestrator = getOrchestrator();

        // Reset child-call tracking captured during init so we count only the burst below.
        lock (orchestrator.ChildCalls)
        {
            orchestrator.ChildCalls.Clear();
        }

        using var barrier = new Barrier(producerCount);
        var producers = Enumerable.Range(1, producerCount).Select(producerId => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (var i = 0; i < emissionsPerProducer; i++)
            {
                innerSubjects[producerId].OnNext($"{producerId}-{i}");
            }
        })).ToArray();

        await Task.WhenAll(producers);

        // Drain may finish on a producer thread after WhenAll observes the task completing, so
        // settle briefly to let any in-flight delivery finish.
        await Task.Delay(50);

        lock (orchestrator.ChildCalls)
        {
            orchestrator.ChildCalls.Count.Should().Be(totalEmissions,
                "every concurrent inner emission must reach OnInner; reentrant drain during emit must not drop items");
        }

        foreach (var subj in innerSubjects.Values)
        {
            subj.Dispose();
        }
    }

    /// <summary>
    /// The lambda overload of Orchestrate must build a fresh orchestrator per subscription;
    /// the orchestrator holds mutable per-subscription state and reuse across subscribers corrupts
    /// the first subscriber's context.
    /// </summary>
    [Fact]
    public void LambdaOverload_MultipleSubscriptions_DoNotShareOrchestrator()
    {
        using var source = new SourceCache<TestItem, int>(x => x.Key);

        var emitCalls = 0;
        var contexts = new List<int>();

        // Build a chain that captures whichever context each orchestrator received. Two subscribers
        // should each see their own context instance.
        var observable = source.Connect().Orchestrate<TestItem, int, string, int>(
            onSourceChangeSet: (changes, context) =>
            {
                // Hash code of the context instance proves each subscription has its own.
                lock (contexts)
                {
                    contexts.Add(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(context));
                }
            },
            onInner: (_, _, _) => { },
            onDrainComplete: _ => Interlocked.Increment(ref emitCalls));

        using var subA = observable.Subscribe();
        using var subB = observable.Subscribe();

        source.AddOrUpdate(new TestItem(1, "item"));

        lock (contexts)
        {
            contexts.Distinct().Count().Should().Be(2,
                "each subscription must receive its own orchestrator instance with its own context");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Test Infrastructure
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Observer that writes into another cache on every emission — creates cross-cache cycle.</summary>
    private sealed class CrossFeedObserver(SourceCache<TestItem, int> target, int idBase, int maxCrossWrites) : IObserver<IChangeSet<TestItem, int>>
    {
        private int _counter;

        public void OnNext(IChangeSet<TestItem, int> value)
        {
            if (Interlocked.Increment(ref _counter) <= maxCrossWrites)
            {
                target.AddOrUpdate(new TestItem(idBase + _counter, "cross"));
            }
        }

        public void OnError(Exception error) { }

        public void OnCompleted() { }
    }

    /// <summary>
    /// Minimal ICacheOrchestrator implementation that mirrors the legacy CPS-subclass shape.
    /// </summary>
    private sealed class TestOrchestrator(
            ICacheOrchestratorContext<int, string> context,
            IObserver<IChangeSet<TestItem, int>> emitter,
            Func<int, IObservable<string>>? childFactory = null,
            Action? onParent = null,
            Action? onChild = null)
        : ICacheOrchestrator<TestItem, int, string, IChangeSet<TestItem, int>>
    {
        private readonly ChangeAwareCache<TestItem, int> _cache = new();

        public int ParentCallCount;
        public int EmitCallCount;
        public readonly List<(string Value, int Key)> ChildCalls = [];
        public readonly List<bool> IsFinalLog = [];
        public readonly List<bool> WasReentrantLog = [];

        public void OnSourceChangeSet(IChangeSet<TestItem, int> changes)
        {
            Interlocked.Increment(ref ParentCallCount);
            onParent?.Invoke();
            _cache.Clone(changes);

            if (childFactory is not null)
            {
                foreach (var change in (ChangeSet<TestItem, int>)changes)
                {
                    if (change.Reason is ChangeReason.Add or ChangeReason.Update)
                        context.Track(change.Key, childFactory(change.Key));
                    else if (change.Reason is ChangeReason.Remove)
                        context.Untrack(change.Key);
                }
            }
        }

        public void OnInner(string child, int parentKey)
        {
            onChild?.Invoke();
            ChildCalls.Add((child, parentKey));
            _cache.AddOrUpdate(new TestItem(parentKey, child), parentKey);
        }

        public void OnDrainComplete(bool isFinal, bool wasReentrant)
        {
            IsFinalLog.Add(isFinal);
            WasReentrantLog.Add(wasReentrant);

            var changes = _cache.CaptureChanges();
            if (changes.Count > 0)
            {
                Interlocked.Increment(ref EmitCallCount);
                emitter.OnNext(changes);
            }
        }
    }

    /// <summary>Observer that records emissions, completion, and errors.</summary>
    private sealed class TestObserver : IObserver<IChangeSet<TestItem, int>>
    {
        public int EmitCount;
        public bool IsCompleted;
        public Exception? Error;

        public void OnNext(IChangeSet<TestItem, int> value) => Interlocked.Increment(ref EmitCount);
        public void OnError(Exception error) => Error = error;
        public void OnCompleted() => IsCompleted = true;
    }
}
