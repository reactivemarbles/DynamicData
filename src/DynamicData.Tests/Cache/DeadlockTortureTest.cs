// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Deadlock torture test. Every dangerous operator (one that holds a lock during
/// downstream delivery) is wired into a bidirectional cross-cache pipeline.
/// Two threads write simultaneously, creating the ABBA lock cycle:
///   Thread A: sourceA._locker -> operator lock -> PopulateInto -> sourceB._locker
///   Thread B: sourceB._locker -> operator lock -> PopulateInto -> sourceA._locker
///
/// On main (Synchronize(lock)): deadlocks reliably within seconds.
/// On the PR branch (SynchronizeSafe queue-drain): no deadlock possible.
/// </summary>
public sealed class DeadlockTortureTest
{
    private const int ItemCount = 200;
    private const int Iterations = 50;
    private const int TimeoutSeconds = 60;

    private static async Task<bool> RunBidirectionalDeadlockTest(
        Func<IObservable<IChangeSet<Person, string>>, IObservable<IChangeSet<Person, string>>> pipeline,
        Action? subjectPusher = null,
        int iterations = Iterations)
    {
        for (var iter = 0; iter < iterations; iter++)
        {
            using var sourceA = new SourceCache<Person, string>(p => p.UniqueKey);
            using var sourceB = new SourceCache<Person, string>(p => p.UniqueKey);

            using var aToB = pipeline(sourceA.Connect().Filter(x => x.Name.StartsWith("A"))).PopulateInto(sourceB);
            using var bToA = pipeline(sourceB.Connect().Filter(x => x.Name.StartsWith("B"))).PopulateInto(sourceA);

            var participants = subjectPusher is null ? 2 : 3;
            using var barrier = new Barrier(participants);
            var taskA = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceA.AddOrUpdate(new Person("A-" + iter + "-" + i, i)); });
            var taskB = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceB.AddOrUpdate(new Person("B-" + iter + "-" + i, i)); });
            var taskC = subjectPusher is null ? null : Task.Run(() => { barrier.SignalAndWait(); subjectPusher(); });

            var completed = taskC is null ? Task.WhenAll(taskA, taskB) : Task.WhenAll(taskA, taskB, taskC);
            if (await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds))) != completed)
                return false;
        }
        return true;
    }

    [Fact] public async Task Sort_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)))).Should().BeTrue();

    [Fact] public async Task AutoRefresh_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.AutoRefresh(p => p.Age))).Should().BeTrue();

    [Fact] public async Task GroupOn_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.Group(p => p.Age % 3).MergeMany(g => g.Cache.Connect()))).Should().BeTrue();

    [Fact] public async Task GroupWithImmutableState_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.GroupWithImmutableState(p => p.Age % 3).TransformMany(g => g.Items, p => p.UniqueKey))).Should().BeTrue();

    [Fact] public async Task GroupOnWithRegrouper_DoesNotDeadlock()
    {
        using var regrouper = new System.Reactive.Subjects.Subject<System.Reactive.Unit>();
        (await RunBidirectionalDeadlockTest(
            s => s.Group(p => p.Age % 3, regrouper).MergeMany(g => g.Cache.Connect()),
            subjectPusher: () => { for (var j = 0; j < ItemCount; j++) regrouper.OnNext(System.Reactive.Unit.Default); })).Should().BeTrue();
    }

    [Fact] public async Task GroupOnDynamicSelector_DoesNotDeadlock()
    {
        using var selector = new BehaviorSubject<Func<Person, string, int>>((p, _) => p.Age % 3);
        using var regrouper = new System.Reactive.Subjects.Subject<System.Reactive.Unit>();
        (await RunBidirectionalDeadlockTest(
            s => s.Group(selector, regrouper).MergeMany(g => g.Cache.Connect()),
            subjectPusher: () =>
            {
                for (var j = 0; j < ItemCount; j++)
                {
                    selector.OnNext((p, _) => p.Age % (2 + (j % 4)));
                    regrouper.OnNext(System.Reactive.Unit.Default);
                }
            })).Should().BeTrue();
    }

    [Fact] public async Task TransformAsyncWithForce_DoesNotDeadlock()
    {
        using var force = new System.Reactive.Subjects.Subject<Func<Person, string, bool>>();
        (await RunBidirectionalDeadlockTest(
            s => s.TransformAsync(p => Task.FromResult(new Person("T-" + p.Name, p.Age)), force),
            subjectPusher: () => { for (var j = 0; j < ItemCount; j++) force.OnNext(static (_, _) => true); })).Should().BeTrue();
    }

    [Fact] public async Task Page_DoesNotDeadlock()
    {
        using var req = new BehaviorSubject<IPageRequest>(new PageRequest(1, 50));
        (await RunBidirectionalDeadlockTest(
            s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Page(req),
            subjectPusher: () => { for (var j = 0; j < ItemCount; j++) req.OnNext(new PageRequest(1 + (j % 4), 25 + (j % 4) * 25)); })).Should().BeTrue();
    }

    [Fact] public async Task SortAndPage_DoesNotDeadlock()
    {
        using var req = new BehaviorSubject<IPageRequest>(new PageRequest(1, 50));
        (await RunBidirectionalDeadlockTest(
            s => s.SortAndPage(SortExpressionComparer<Person>.Ascending(p => p.Age), req),
            subjectPusher: () => { for (var j = 0; j < ItemCount; j++) req.OnNext(new PageRequest(1 + (j % 4), 25 + (j % 4) * 25)); })).Should().BeTrue();
    }

    [Fact] public async Task Virtualise_DoesNotDeadlock()
    {
        using var req = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 50));
        (await RunBidirectionalDeadlockTest(
            s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Virtualise(req),
            subjectPusher: () => { for (var j = 0; j < ItemCount; j++) req.OnNext(new VirtualRequest(j * 5, 25 + (j % 4) * 25)); })).Should().BeTrue();
    }

    [Fact] public async Task SortAndVirtualize_DoesNotDeadlock()
    {
        using var req = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 50));
        (await RunBidirectionalDeadlockTest(
            s => s.SortAndVirtualize(SortExpressionComparer<Person>.Ascending(p => p.Age), req),
            subjectPusher: () => { for (var j = 0; j < ItemCount; j++) req.OnNext(new VirtualRequest(j * 5, 25 + (j % 4) * 25)); })).Should().BeTrue();
    }

    [Fact] public async Task QueryWhenChanged_DoesNotDeadlock()
    {
        for (var iter = 0; iter < Iterations; iter++)
        {
            using var sourceA = new SourceCache<Person, string>(p => p.UniqueKey);
            using var sourceB = new SourceCache<Person, string>(p => p.UniqueKey);

            // QueryWhenChanged with an itemChangedTrigger exercises the Merge branch.
            // A side-channel write into the other cache closes the same ABBA cycle that
            // PopulateInto would close for changeset-shaped operators.
            using var aToB = sourceA.Connect()
                .Filter(p => p.Name.StartsWith("A"))
                .QueryWhenChanged(p => p.WhenPropertyChanged(x => x.Age))
                .Subscribe(_ => sourceB.AddOrUpdate(new Person("A-marker", 0)));
            using var bToA = sourceB.Connect()
                .Filter(p => p.Name.StartsWith("B"))
                .QueryWhenChanged(p => p.WhenPropertyChanged(x => x.Age))
                .Subscribe(_ => sourceA.AddOrUpdate(new Person("B-marker", 0)));

            using var barrier = new Barrier(2);
            var taskA = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceA.AddOrUpdate(new Person("A-" + iter + "-" + i, i)); });
            var taskB = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceB.AddOrUpdate(new Person("B-" + iter + "-" + i, i)); });

            var completed = Task.WhenAll(taskA, taskB);
            (await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)))).Should().BeSameAs(completed, "iteration " + iter);
        }
    }

    [Fact] public async Task TransformWithForce_DoesNotDeadlock()
    {
        using var force = new Subject<Func<Person, string, bool>>();
        (await RunBidirectionalDeadlockTest(
            s => s.Transform((p, k) => new Person("T-" + p.Name, p.Age), force),
            subjectPusher: () => { for (var j = 0; j < ItemCount; j++) force.OnNext(static (p, _) => true); })).Should().BeTrue();
    }

    [Fact] public async Task BatchIf_DoesNotDeadlock()
    {
        using var pause = new BehaviorSubject<bool>(false);
        (await RunBidirectionalDeadlockTest(
            s => s.BatchIf(pause, false, (TimeSpan?)null),
            subjectPusher: () => { for (var j = 0; j < ItemCount; j++) pause.OnNext(j % 2 == 0); })).Should().BeTrue();
    }

    [Fact] public async Task DisposeMany_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.DisposeMany())).Should().BeTrue();

    [Fact] public async Task OnItemRemoved_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.OnItemRemoved(_ => { }))).Should().BeTrue();

    [Fact] public async Task AllDangerous_Stacked_DoNotDeadlock()
    {
        using var pageReq = new BehaviorSubject<IPageRequest>(new PageRequest(1, 100));
        using var virtReq = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 100));
        using var force = new Subject<Func<Person, string, bool>>();
        (await RunBidirectionalDeadlockTest(
            s => s.GroupWithImmutableState(p => p.Age % 3)
                  .TransformMany(g => g.Items, p => p.UniqueKey)
                  .AutoRefresh(p => p.Age)
                  .Filter(p => p.Age >= 0)
                  .Transform((p, k) => new Person("X-" + p.Name, p.Age), force)
                  .OnItemRemoved(_ => { })
                  .DisposeMany()
                  .Sort(SortExpressionComparer<Person>.Ascending(p => p.Age))
                  .Virtualise(virtReq)
                  .Page(pageReq),
            subjectPusher: () =>
            {
                for (var j = 0; j < ItemCount; j++)
                {
                    force.OnNext(static (p, _) => true);
                    pageReq.OnNext(new PageRequest(1 + (j % 4), 50 + (j % 4) * 50));
                    virtReq.OnNext(new VirtualRequest(j * 5, 50 + (j % 4) * 50));
                }
            },
            iterations: Iterations * 2)).Should().BeTrue();
    }

    [Fact] public async Task MultiplePairs_Simultaneous_NoDeadlock()
    {
        using var pageReq = new BehaviorSubject<IPageRequest>(new PageRequest(1, 50));
        using var pageReq2 = new BehaviorSubject<IPageRequest>(new PageRequest(1, 50));
        using var virtReq = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 50));
        using var virtReq2 = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 50));
        using var pause = new BehaviorSubject<bool>(false);
        var results = await Task.WhenAll(
            RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)), iterations: 30),
            RunBidirectionalDeadlockTest(s => s.AutoRefresh(p => p.Age), iterations: 30),
            RunBidirectionalDeadlockTest(s => s.Group(p => p.Age % 3).MergeMany(g => g.Cache.Connect()), iterations: 30),
            RunBidirectionalDeadlockTest(s => s.GroupWithImmutableState(p => p.Age % 3).TransformMany(g => g.Items, p => p.UniqueKey), iterations: 30),
            RunBidirectionalDeadlockTest(s => s.OnItemRemoved(_ => { }), iterations: 30),
            RunBidirectionalDeadlockTest(s => s.DisposeMany(), iterations: 30),
            RunBidirectionalDeadlockTest(
                s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Page(pageReq),
                subjectPusher: () => { for (var j = 0; j < ItemCount; j++) pageReq.OnNext(new PageRequest(1 + (j % 4), 25 + (j % 4) * 25)); },
                iterations: 30),
            RunBidirectionalDeadlockTest(
                s => s.SortAndPage(SortExpressionComparer<Person>.Ascending(p => p.Age), pageReq2),
                subjectPusher: () => { for (var j = 0; j < ItemCount; j++) pageReq2.OnNext(new PageRequest(1 + (j % 4), 25 + (j % 4) * 25)); },
                iterations: 30),
            RunBidirectionalDeadlockTest(
                s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Virtualise(virtReq),
                subjectPusher: () => { for (var j = 0; j < ItemCount; j++) virtReq.OnNext(new VirtualRequest(j * 5, 25 + (j % 4) * 25)); },
                iterations: 30),
            RunBidirectionalDeadlockTest(
                s => s.SortAndVirtualize(SortExpressionComparer<Person>.Ascending(p => p.Age), virtReq2),
                subjectPusher: () => { for (var j = 0; j < ItemCount; j++) virtReq2.OnNext(new VirtualRequest(j * 5, 25 + (j % 4) * 25)); },
                iterations: 30),
            RunBidirectionalDeadlockTest(
                s => s.BatchIf(pause, false, (TimeSpan?)null),
                subjectPusher: () => { for (var j = 0; j < ItemCount; j++) pause.OnNext(j % 2 == 0); },
                iterations: 30));
        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    [Fact] public async Task ThreeWayCircular_DoesNotDeadlock()
    {
        for (var iter = 0; iter < Iterations; iter++)
        {
            using var a = new SourceCache<Person, string>(p => p.UniqueKey);
            using var b = new SourceCache<Person, string>(p => p.UniqueKey);
            using var c = new SourceCache<Person, string>(p => p.UniqueKey);

            using var ab = a.Connect().Filter(p => p.Name.StartsWith("A")).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).PopulateInto(b);
            using var bc = b.Connect().Filter(p => p.Name.StartsWith("A")).AutoRefresh(p => p.Age).PopulateInto(c);
            using var ca = c.Connect().Filter(p => p.Name.StartsWith("A")).Transform((p, _) => new Person("C-" + p.Name, p.Age)).Filter(p => p.Name.StartsWith("C")).PopulateInto(a);

            using var barrier = new Barrier(3);
            var tasks = new[]
            {
                Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) a.AddOrUpdate(new Person("A-" + iter + "-" + i, i)); }),
                Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) b.AddOrUpdate(new Person("B-" + iter + "-" + i, i)); }),
                Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) c.AddOrUpdate(new Person("CC-" + iter + "-" + i, i)); }),
            };
            var completed = Task.WhenAll(tasks);
            (await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)))).Should().BeSameAs(completed, "iteration " + iter);
        }
    }

    [Fact] public async Task TransformToTree_DoesNotDeadlock()
    {
        // Exercises TreeBuilder.cs:200 (_predicateChanged.SynchronizeSafe(queue).UnsynchronizedCombineLatest
        // (reFilterObservable.SynchronizeSafe(queue), ...)). Cross-cache cycle is closed via a side-channel
        // Subscribe that writes a marker into the other cache for every tree changeset.
        for (var iter = 0; iter < Iterations; iter++)
        {
            using var sourceA = new SourceCache<Person, string>(p => p.UniqueKey);
            using var sourceB = new SourceCache<Person, string>(p => p.UniqueKey);

            // The pivotOn function returns the parent's key (or the item's own key for roots). Half the
            // items become children of "A-{iter}-0" / "B-{iter}-0", populating the inner tree structure.
            using var aToB = sourceA.Connect()
                .TransformToTree(p => p.Age == 0 ? p.UniqueKey : "A-" + iter + "-0")
                .Subscribe(_ => sourceB.AddOrUpdate(new Person("from-a-tree-" + iter, 0)));
            using var bToA = sourceB.Connect()
                .TransformToTree(p => p.Age == 0 ? p.UniqueKey : "B-" + iter + "-0")
                .Subscribe(_ => sourceA.AddOrUpdate(new Person("from-b-tree-" + iter, 0)));

            using var barrier = new Barrier(2);
            var taskA = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceA.AddOrUpdate(new Person("A-" + iter + "-" + i, i)); });
            var taskB = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceB.AddOrUpdate(new Person("B-" + iter + "-" + i, i)); });

            var completed = Task.WhenAll(taskA, taskB);
            (await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)))).Should().BeSameAs(completed, "iteration " + iter);
        }
    }

    [Fact] public async Task Switch_DoesNotDeadlock() =>
        // Exercises the refactored Switch.cs (SerialDisposable + UnsynchronizedMerge of destination.Connect()
        // and the errors subject). Observable.Return(s).Switch() drives exactly one outer notification, which
        // is enough to wire up the destination cache and exercise the gate-free merge on every inner change.
        (await RunBidirectionalDeadlockTest(s => System.Reactive.Linq.Observable.Return(s).Switch())).Should().BeTrue();
}
