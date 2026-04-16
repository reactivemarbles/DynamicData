// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using Bogus;

using DynamicData.Internal;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Internal;

/// <summary>
/// Tests for <see cref="CacheParentSubscription{TParent, TKey, TChild, TObserver}"/>
/// behavioral contracts using a minimal concrete subclass.
/// </summary>
public sealed class CacheParentSubscriptionFixture
{
    private const int SeedMin = 1;
    private const int SeedMax = 10000;
    private const int BatchSizeMin = 2;
    private const int BatchSizeMax = 8;

    private readonly Randomizer _rand = new(55);

    /// <summary>Test item with a typed key — no string parsing.</summary>
    private sealed record TestItem(int Key, string Value);

    [Fact]
    public void ParentOnNext_CalledForEachChangeSet()
    {
        var itemCount = _rand.Number(BatchSizeMin, BatchSizeMax);
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer);
        sub.ExposeCreateParent(source.Connect());

        var items = Enumerable.Range(0, itemCount)
            .Select(i => new TestItem(_rand.Number(SeedMin, SeedMax) + i * 100, _rand.String2(_rand.Number(3, 10))))
            .ToList();

        foreach (var item in items)
            source.AddOrUpdate(item);

        sub.ParentCallCount.Should().Be(items.Count, "ParentOnNext should fire once per changeset");
        observer.EmitCount.Should().Be(items.Count, "EmitChanges should fire after each parent update");
    }

    [Fact]
    public void ChildOnNext_CalledForEachEmission()
    {
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer, key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        sub.ExposeCreateParent(source.Connect());

        var key = _rand.Number(SeedMin, SeedMax);
        source.AddOrUpdate(new TestItem(key, "parent"));

        childSubjects.Should().HaveCount(1);
        var childValue = _rand.String2(_rand.Number(5, 15));
        childSubjects[0].OnNext(childValue);

        sub.ChildCalls.Should().ContainSingle()
            .Which.Should().Be((childValue, key));
    }

    [Fact]
    public void EmitChanges_FiresOnceForBatch()
    {
        var batchSize = _rand.Number(BatchSizeMin, BatchSizeMax);
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer);
        sub.ExposeCreateParent(source.Connect());

        source.Edit(updater =>
        {
            for (var i = 0; i < batchSize; i++)
                updater.AddOrUpdate(new TestItem(i + 1, _rand.String2(_rand.Number(3, 8))));
        });

        sub.ParentCallCount.Should().Be(1, "single batch = single ParentOnNext");
        sub.EmitCallCount.Should().Be(1, "single batch = single EmitChanges");
    }

    [Fact]
    public void Batching_ChildUpdatesSettleBeforeEmit()
    {
        var batchSize = _rand.Number(BatchSizeMin, BatchSizeMax);
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var observer = new TestObserver();
        var childCount = 0;
        using var sub = new TestSubscription(observer, key =>
        {
            Interlocked.Increment(ref childCount);
            return new BehaviorSubject<string>($"sync-{key}");
        });
        sub.ExposeCreateParent(source.Connect());

        source.Edit(updater =>
        {
            for (var i = 0; i < batchSize; i++)
                updater.AddOrUpdate(new TestItem(i + 1, _rand.String2(_rand.Number(3, 8))));
        });

        childCount.Should().Be(batchSize, "each item should create a child");
        sub.EmitCallCount.Should().BeGreaterThanOrEqualTo(1,
            "EmitChanges fires after parent + children settle");
    }

    [Fact]
    public void Completion_RequiresParentAndAllChildren()
    {
        using var source = new TestSourceCache<TestItem, int>(x => x.Key);
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        using var sub = new TestSubscription(observer, key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        sub.ExposeCreateParent(source.Connect());

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
        using var sub = new TestSubscription(observer);
        sub.ExposeCreateParent(source.Connect());

        source.Complete();
        observer.IsCompleted.Should().BeTrue("immediate OnCompleted when no children");
    }

    [Fact]
    public void Disposal_StopsAllEmissions()
    {
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var childSubjects = new List<Subject<string>>();
        var observer = new TestObserver();
        var sub = new TestSubscription(observer, key =>
        {
            var subj = new Subject<string>();
            childSubjects.Add(subj);
            return subj;
        });
        sub.ExposeCreateParent(source.Connect());

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
        using var sub = new TestSubscription(observer);
        sub.ExposeCreateParent(source.Connect());

        var error = new InvalidOperationException("test error");
        source.SetError(error);

        observer.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Serialization_ParentAndChildDoNotInterleave()
    {
        using var source = new SourceCache<TestItem, int>(x => x.Key);
        var callLog = new List<string>();
        var observer = new TestObserver();
        using var sub = new TestSubscription(
            observer,
            key =>
            {
                var subj = new Subject<string>();
                return subj;
            },
            onParent: () => { lock (callLog) callLog.Add("P-start"); Thread.Sleep(1); lock (callLog) callLog.Add("P-end"); },
            onChild: () => { lock (callLog) callLog.Add("C-start"); Thread.Sleep(1); lock (callLog) callLog.Add("C-end"); });
        sub.ExposeCreateParent(source.Connect());

        source.AddOrUpdate(new TestItem(_rand.Number(SeedMin, SeedMax), "item"));

        // Start/end pairs should not interleave
        for (var i = 0; i + 1 < callLog.Count; i += 2)
        {
            var prefix = callLog[i].Split('-')[0];
            callLog[i + 1].Should().StartWith(prefix, "operations should not interleave");
        }
    }

    /// <summary>
    /// Proves CPS delivery runs without holding the lock. Two TestSubscription instances
    /// whose EmitChanges callbacks write into each other's source cache — creating a
    /// cross-cache cycle. Deadlocks on unfixed code, passes after the fix.
    /// </summary>
    [Trait("Category", "ExplicitDeadlock")]
    [Fact]
    public async Task DeadlockProof_CrossFeedingSubscriptions()
    {
        var iterations = _rand.Number(50, 150);

        using var sourceA = new SourceCache<TestItem, int>(x => x.Key);
        using var sourceB = new SourceCache<TestItem, int>(x => x.Key);

        // Each TestSubscription's EmitChanges writes into the OTHER source (limited to prevent infinite loops)
        var observerA = new CrossFeedObserver(sourceB, 100_001, iterations);
        using var subA = new TestSubscription(observerA);
        subA.ExposeCreateParent(sourceA.Connect());

        var observerB = new CrossFeedObserver(sourceA, 200_001, iterations);
        using var subB = new TestSubscription(observerB);
        subB.ExposeCreateParent(sourceB.Connect());

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
            "cross-feeding CacheParentSubscriptions should not deadlock");
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
            // Limit cross-writes to prevent infinite feedback loops
            if (Interlocked.Increment(ref _counter) <= maxCrossWrites)
            {
                target.AddOrUpdate(new TestItem(idBase + _counter, "cross"));
            }
        }

        public void OnError(Exception error) { }

        public void OnCompleted() { }
    }

    /// <summary>
    /// Minimal concrete CacheParentSubscription for testing.
    /// </summary>
    private sealed class TestSubscription : CacheParentSubscription<TestItem, int, string, IChangeSet<TestItem, int>>
    {
        private readonly Func<int, IObservable<string>>? _childFactory;
        private readonly Action? _onParent;
        private readonly Action? _onChild;
        private readonly ChangeAwareCache<TestItem, int> _cache = new();

        public int ParentCallCount;
        public int EmitCallCount;
        public readonly List<(string Value, int Key)> ChildCalls = [];

        public TestSubscription(
            IObserver<IChangeSet<TestItem, int>> observer,
            Func<int, IObservable<string>>? childFactory = null,
            Action? onParent = null,
            Action? onChild = null)
            : base(observer)
        {
            _childFactory = childFactory;
            _onParent = onParent;
            _onChild = onChild;
        }

        public void ExposeCreateParent(IObservable<IChangeSet<TestItem, int>> source)
            => CreateParentSubscription(source);

        protected override void ParentOnNext(IChangeSet<TestItem, int> changes)
        {
            Interlocked.Increment(ref ParentCallCount);
            _onParent?.Invoke();
            _cache.Clone(changes);

            if (_childFactory is not null)
            {
                foreach (var change in (ChangeSet<TestItem, int>)changes)
                {
                    if (change.Reason is ChangeReason.Add or ChangeReason.Update)
                        AddChildSubscription(MakeChildObservable(_childFactory(change.Key)), change.Key);
                    else if (change.Reason is ChangeReason.Remove)
                        RemoveChildSubscription(change.Key);
                }
            }
        }

        protected override void ChildOnNext(string child, int parentKey)
        {
            _onChild?.Invoke();
            ChildCalls.Add((child, parentKey));
            _cache.AddOrUpdate(new TestItem(parentKey, child), parentKey);
        }

        protected override void EmitChanges(IObserver<IChangeSet<TestItem, int>> observer)
        {
            Interlocked.Increment(ref EmitCallCount);
            var changes = _cache.CaptureChanges();
            if (changes.Count > 0)
                observer.OnNext(changes);
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