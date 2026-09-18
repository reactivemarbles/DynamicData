// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

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
[NotInParallel]
public sealed class DeadlockTortureTest
{
    private const int ItemCount = 200;
    private const int Iterations = 50;
    private const int TimeoutSeconds = 15;

    private static async Task<bool> RunBidirectionalDeadlockTest(
        Func<IObservable<IChangeSet<Person, string>>, IObservable<IChangeSet<Person, string>>> pipeline,
        int iterations = Iterations)
    {
        for (var iter = 0; iter < iterations; iter++)
        {
            using var sourceA = new SourceCache<Person, string>(p => p.UniqueKey);
            using var sourceB = new SourceCache<Person, string>(p => p.UniqueKey);

            using var aToB = pipeline(sourceA.Connect().Filter(x => x.Name.StartsWith("A"))).PopulateInto(sourceB);
            using var bToA = pipeline(sourceB.Connect().Filter(x => x.Name.StartsWith("B"))).PopulateInto(sourceA);

            using var barrier = new Barrier(2);
            var taskA = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceA.AddOrUpdate(new Person("A-" + iter + "-" + i, i)); });
            var taskB = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceB.AddOrUpdate(new Person("B-" + iter + "-" + i, i)); });

            var completed = Task.WhenAll(taskA, taskB);
            if (await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds))) != completed)
                return false;
        }
        return true;
    }

    [Test]
    public async Task Sort_DoesNotDeadlock() =>
await Assert.That((await RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age))))).IsTrue();

    [Test]
    public async Task AutoRefresh_DoesNotDeadlock() =>
await Assert.That((await RunBidirectionalDeadlockTest(s => s.AutoRefresh(p => p.Age)))).IsTrue();

    [Test]
    public async Task GroupOn_DoesNotDeadlock() =>
await Assert.That((await RunBidirectionalDeadlockTest(s => s.Group(p => p.Age % 3).MergeMany(g => g.Cache.Connect())))).IsTrue();

    [Test]
    public async Task Page_DoesNotDeadlock()
    {
        using var req = new ReactiveUI.Primitives.Signals.StateSignal<IPageRequest>(new PageRequest(1, 50));
        await Assert.That((await RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Page(req)))).IsTrue();
    }

    [Test]
    public async Task Virtualise_DoesNotDeadlock()
    {
        using var req = new ReactiveUI.Primitives.Signals.StateSignal<IVirtualRequest>(new VirtualRequest(0, 50));
        await Assert.That((await RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Virtualise(req)))).IsTrue();
    }

    [Test]
    public async Task TransformWithForce_DoesNotDeadlock()
    {
        using var force = new ReactiveUI.Primitives.Signals.Signal<Func<Person, string, bool>>();
        await Assert.That((await RunBidirectionalDeadlockTest(s => s.Transform((p, k) => new Person("T-" + p.Name, p.Age), force)))).IsTrue();
    }

    [Test]
    public async Task BatchIf_DoesNotDeadlock() =>
await Assert.That((await RunBidirectionalDeadlockTest(s => s.BatchIf(new ReactiveUI.Primitives.Signals.StateSignal<bool>(false), false, (TimeSpan?)null)))).IsTrue();

    [Test]
    public async Task DisposeMany_DoesNotDeadlock() =>
await Assert.That((await RunBidirectionalDeadlockTest(s => s.DisposeMany()))).IsTrue();

    [Test]
    public async Task OnItemRemoved_DoesNotDeadlock() =>
await Assert.That((await RunBidirectionalDeadlockTest(s => s.OnItemRemoved(_ => { })))).IsTrue();

    [Test]
    public async Task AllDangerous_Stacked_DoNotDeadlock()
    {
        using var pageReq = new ReactiveUI.Primitives.Signals.StateSignal<IPageRequest>(new PageRequest(1, 100));
        using var force = new ReactiveUI.Primitives.Signals.Signal<Func<Person, string, bool>>();
        await Assert.That(await RunBidirectionalDeadlockTest(
            s => s.AutoRefresh(p => p.Age)
                  .Filter(p => p.Age >= 0)
                  .Transform((p, k) => new Person("X-" + p.Name, p.Age), force)
                  .OnItemRemoved(_ => { })
                  .DisposeMany()
                  .Sort(SortExpressionComparer<Person>.Ascending(p => p.Age))
                  .Page(pageReq),
            iterations: Iterations * 2)).IsTrue();
    }

    [Test]
    public async Task MultiplePairs_Simultaneous_NoDeadlock()
    {
        using var pageReq = new ReactiveUI.Primitives.Signals.StateSignal<IPageRequest>(new PageRequest(1, 50));
        using var virtReq = new ReactiveUI.Primitives.Signals.StateSignal<IVirtualRequest>(new VirtualRequest(0, 50));
        var results = await Task.WhenAll(
            RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)), 30),
            RunBidirectionalDeadlockTest(s => s.AutoRefresh(p => p.Age), 30),
            RunBidirectionalDeadlockTest(s => s.Group(p => p.Age % 3).MergeMany(g => g.Cache.Connect()), 30),
            RunBidirectionalDeadlockTest(s => s.OnItemRemoved(_ => { }), 30),
            RunBidirectionalDeadlockTest(s => s.DisposeMany(), 30),
            RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Page(pageReq), 30),
            RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Virtualise(virtReq), 30),
            RunBidirectionalDeadlockTest(s => s.BatchIf(new ReactiveUI.Primitives.Signals.StateSignal<bool>(false), false, (TimeSpan?)null), 30));
        foreach (var result in results)
        {
            await Assert.That(result).IsTrue();
        }
    }

    [Test]
    public async Task ThreeWayCircular_DoesNotDeadlock()
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
            await Assert.That((await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds))))).IsSameReferenceAs(completed).Because("iteration " + iter);
        }
    }
}
