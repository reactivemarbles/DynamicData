using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Terminal event behaviour for operators which previously never delivered OnCompleted.
/// </summary>
public class OperatorCompletionFixture
{
    private static readonly IComparer<Person> ByName = SortExpressionComparer<Person>.Ascending(p => p.Name);

    [Fact]
    public void MonitorStatusCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var statuses = new List<ConnectionStatus>();
        var completed = false;

        using var subscription = source.MonitorStatus().Subscribe(statuses.Add, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue("the status stream is finished once the source is");
        statuses.Should().EndWith(ConnectionStatus.Completed);
    }

    [Fact]
    public void MonitorStatusReportsLoadedBeforeCompleted()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var statuses = new List<ConnectionStatus>();

        using var subscription = source.MonitorStatus().Subscribe(statuses.Add);

        source.OnNext(new ChangeSet<Person, string>());
        source.OnCompleted();

        statuses.Should().Equal(ConnectionStatus.Pending, ConnectionStatus.Loaded, ConnectionStatus.Completed);
    }

    [Fact]
    public void MonitorStatusDeliversErrorAfterReportingIt()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var statuses = new List<ConnectionStatus>();
        Exception? error = null;

        using var subscription = source.MonitorStatus().Subscribe(statuses.Add, ex => error = ex);

        source.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
        statuses.Should().EndWith(ConnectionStatus.Errored);
    }

    [Fact]
    public void MonitorStatusCompletesWhenTheSourceIsAlreadyFinished()
    {
        var completed = false;

        using var subscription = Observable.Empty<IChangeSet<Person, string>>()
            .MonitorStatus()
            .Subscribe(_ => { }, () => completed = true);

        completed.Should().BeTrue("a terminal event arriving during subscription must not be lost");
    }

    [Fact]
    public void DeferUntilLoadedCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.DeferUntilLoaded().Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void SkipInitialCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.SkipInitial().Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void GroupWithImmutableStateCompletesWhenNoRegrouperIsSupplied()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.GroupWithImmutableState(p => p.Age).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue("an absent regrouper can never fire and so must not hold the result open");
    }

    [Fact]
    public void GroupOnCompletesWhenNoRegrouperIsSupplied()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.Group(p => p.Age).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void SortCompletesWhenGivenAComparerObservable()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.Sort(Observable.Return(ByName)).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue("an absent resort signal can never fire and so must not hold the result open");
    }

    [Fact]
    public void SortCompletesWhenGivenAResorter()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.Sort(ByName, Observable.Never<Unit>().Take(0)).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void InnerJoinManyCompletesWhenBothSidesComplete()
    {
        using var left = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = left
            .InnerJoinMany(Observable.Empty<IChangeSet<Person, string>>(), p => p.Name, (_, person, _) => person)
            .Subscribe(_ => { }, () => completed = true);

        left.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void BatchIfCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.BatchIf(Observable.Return(false), Scheduler.Immediate).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void BatchIfFlushesHeldChangesBeforeCompleting()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        using var pause = new BehaviorSubject<bool>(true);
        var received = 0;
        var completed = false;

        using var subscription = source.BatchIf(pause, Scheduler.Immediate).Subscribe(_ => received++, () => completed = true);

        source.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "a", new Person("a", 1)) });
        source.OnCompleted();

        received.Should().Be(1, "changes held back by the pause would otherwise be lost");
        completed.Should().BeTrue();
    }

    [Fact]
    public void MergeManyItemsCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.MergeManyItems(_ => Observable.Empty<int>()).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void MergeManyItemsStaysOpenWhenAChildCompletes()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        var completed = false;

        using var subscription = source.Connect().MergeManyItems(_ => Observable.Return(1)).Subscribe(_ => { }, () => completed = true);

        source.AddOrUpdate(new Person("a", 1));

        completed.Should().BeFalse("one child finishing does not finish the merge");
    }

    [Fact]
    public void GroupDeliversErrorWithoutThrowing()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        Exception? error = null;

        using var subscription = source.Group(p => p.Age).Subscribe(_ => { }, ex => error = ex, () => { });

        source.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void TransformToTreeDeliversError()
    {
        Exception? error = null;

        using var subscription = Observable.Throw<IChangeSet<Person, string>>(new InvalidOperationException("boom"))
            .TransformToTree(p => p.Name)
            .Subscribe(_ => { }, ex => error = ex, () => { });

        error.Should().BeOfType<InvalidOperationException>("the intermediate caches must not swallow it");
    }

    [Fact]
    public void TransformToTreeCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person, string>>();
        var completed = false;

        using var subscription = source.TransformToTree(p => p.Name).Subscribe(_ => { }, () => completed = true);

        source.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "a", new Person("a", 1)) });
        source.OnCompleted();

        completed.Should().BeTrue();
    }
}
