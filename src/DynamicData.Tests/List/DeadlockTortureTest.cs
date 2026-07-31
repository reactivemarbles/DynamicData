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

namespace DynamicData.Tests.List;

/// <summary>
/// Deadlock torture test for list operators. Every operator that previously used
/// <c>Synchronize(locker)</c> (holding the lock during downstream delivery) is wired into a
/// bidirectional cross-list pipeline. Two threads write simultaneously, creating the ABBA
/// lock cycle:
///   Thread A: sourceA._locker -> operator lock -> PopulateInto -> sourceB._locker
///   Thread B: sourceB._locker -> operator lock -> PopulateInto -> sourceA._locker
///
/// On origin/main (Synchronize(lock)): deadlocks reliably within seconds.
/// On the PR branch (SynchronizeSafe queue-drain): no deadlock possible.
/// </summary>
public sealed class DeadlockTortureTest
{
    private const int ItemCount = 200;
    private const int Iterations = 50;
    private const int TimeoutSeconds = 15;

    private static async Task<bool> RunBidirectionalDeadlockTest(
        Func<IObservable<IChangeSet<Person>>, IObservable<IChangeSet<Person>>> pipeline,
        int iterations = Iterations)
    {
        for (var iter = 0; iter < iterations; iter++)
        {
            using var sourceA = new SourceList<Person>();
            using var sourceB = new SourceList<Person>();

            // Filter prefixes prevent the feedback loop: items from A flowing into B keep their
            // "A" prefix, so the B-to-A filter rejects them. Likewise for the reverse direction.
            using var aToB = pipeline(sourceA.Connect().Filter(p => p.Name.StartsWith("A"))).PopulateInto(sourceB);
            using var bToA = pipeline(sourceB.Connect().Filter(p => p.Name.StartsWith("B"))).PopulateInto(sourceA);

            using var barrier = new Barrier(2);
            var taskA = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceA.Add(new Person("A-" + iter + "-" + i, i)); });
            var taskB = Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) sourceB.Add(new Person("B-" + iter + "-" + i, i)); });

            var completed = Task.WhenAll(taskA, taskB);
            if (await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds))) != completed)
                return false;
        }
        return true;
    }

    [Fact] public async Task Sort_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)))).Should().BeTrue();

    [Fact] public async Task AutoRefresh_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.AutoRefresh(p => p.Age))).Should().BeTrue();

    [Fact] public async Task Page_DoesNotDeadlock()
    {
        using var req = new BehaviorSubject<IPageRequest>(new PageRequest(1, 50));
        (await RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Page(req))).Should().BeTrue();
    }

    [Fact] public async Task Virtualise_DoesNotDeadlock()
    {
        using var req = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 50));
        (await RunBidirectionalDeadlockTest(s => s.Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).Virtualise(req))).Should().BeTrue();
    }

    [Fact] public async Task FilterDynamic_DoesNotDeadlock()
    {
        using var pred = new BehaviorSubject<Func<Person, bool>>(_ => true);
        (await RunBidirectionalDeadlockTest(s => s.Filter(pred))).Should().BeTrue();
    }

    [Fact] public async Task BufferIf_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.BufferIf(new BehaviorSubject<bool>(false), false, (TimeSpan?)null))).Should().BeTrue();

    [Fact] public async Task DisposeMany_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.DisposeMany())).Should().BeTrue();

    [Fact] public async Task GroupOn_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.GroupOn(p => p.Age % 3).MergeMany(g => g.List.Connect()))).Should().BeTrue();

    [Fact] public async Task TransformMany_DoesNotDeadlock() =>
        (await RunBidirectionalDeadlockTest(s => s.TransformMany(p => new[] { p }))).Should().BeTrue();

    [Fact] public async Task AllDangerous_Stacked_DoNotDeadlock()
    {
        using var pageReq = new BehaviorSubject<IPageRequest>(new PageRequest(1, 100));
        using var pred = new BehaviorSubject<Func<Person, bool>>(_ => true);
        (await RunBidirectionalDeadlockTest(
            s => s.AutoRefresh(p => p.Age)
                  .Filter(pred)
                  .DisposeMany()
                  .Sort(SortExpressionComparer<Person>.Ascending(p => p.Age))
                  .Page(pageReq),
            iterations: Iterations * 2)).Should().BeTrue();
    }

    [Fact] public async Task ThreeWayCircular_DoesNotDeadlock()
    {
        for (var iter = 0; iter < Iterations; iter++)
        {
            using var a = new SourceList<Person>();
            using var b = new SourceList<Person>();
            using var c = new SourceList<Person>();

            using var ab = a.Connect().Filter(p => p.Name.StartsWith("A")).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).PopulateInto(b);
            using var bc = b.Connect().Filter(p => p.Name.StartsWith("A")).AutoRefresh(p => p.Age).PopulateInto(c);
            using var ca = c.Connect().Filter(p => p.Name.StartsWith("A")).Transform(p => new Person("C-" + p.Name, p.Age)).Filter(p => p.Name.StartsWith("C")).PopulateInto(a);

            using var barrier = new Barrier(3);
            var tasks = new[]
            {
                Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) a.Add(new Person("A-" + iter + "-" + i, i)); }),
                Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) b.Add(new Person("B-" + iter + "-" + i, i)); }),
                Task.Run(() => { barrier.SignalAndWait(); for (var i = 0; i < ItemCount; i++) c.Add(new Person("CC-" + iter + "-" + i, i)); }),
            };
            var completed = Task.WhenAll(tasks);
            (await Task.WhenAny(completed, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)))).Should().BeSameAs(completed, "iteration " + iter);
        }
    }
}
