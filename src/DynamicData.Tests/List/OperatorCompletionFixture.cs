using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Aggregation;
using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

/// <summary>
/// Terminal event behaviour for list operators which previously never delivered OnCompleted.
/// </summary>
public class OperatorCompletionFixture
{
    private static readonly IComparer<Person> ByName = SortExpressionComparer<Person>.Ascending(p => p.Name);

    private static ChangeSet<Person> OneAdd() => [new Change<Person>(ListChangeReason.Add, new Person("a", 1), 0)];

    [Fact]
    public void QueryWhenChangedCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var completed = false;

        using var subscription = source.QueryWhenChanged().Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void QueryWhenChangedDeliversError()
    {
        using var source = new Subject<IChangeSet<Person>>();
        Exception? error = null;

        using var subscription = source.QueryWhenChanged().Subscribe(_ => { }, ex => error = ex, () => { });

        source.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void ToCollectionCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var completed = false;

        using var subscription = source.ToCollection().Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void ToSortedCollectionCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var completed = false;

        using var subscription = source.ToSortedCollection(ByName).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void GroupOnCompletesWhenNoRegrouperIsSupplied()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var completed = false;

        using var subscription = source.GroupOn(p => p.Age).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue("an absent regrouper can never fire and so must not hold the result open");
    }

    [Fact]
    public void GroupWithImmutableStateCompletesWhenNoRegrouperIsSupplied()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var completed = false;

        using var subscription = source.GroupWithImmutableState(p => p.Age).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void SortCompletesWhenGivenAComparerObservable()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var completed = false;

        using var subscription = source.Sort(Observable.Return(ByName)).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void BufferIfCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var completed = false;

        using var subscription = source.BufferIf(Observable.Return(false), Scheduler.Immediate).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void BufferIfFlushesHeldChangesBeforeCompleting()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var received = 0;
        var completed = false;

        using var subscription = source.BufferIf(Observable.Return(true), Scheduler.Immediate).Subscribe(_ => received++, () => completed = true);

        source.OnNext(OneAdd());
        source.OnCompleted();

        received.Should().Be(1, "changes held back by the pause would otherwise be lost");
        completed.Should().BeTrue();
    }

    [Fact]
    public void MaximumCompletesWhenSourceCompletes()
    {
        using var source = new Subject<IChangeSet<Person>>();
        var completed = false;

        using var subscription = source.Maximum(p => p.Age).Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void MaximumDeliversErrorWithoutThrowing()
    {
        using var source = new Subject<IChangeSet<Person>>();
        Exception? error = null;

        using var subscription = source.Maximum(p => p.Age).Subscribe(_ => { }, ex => error = ex, () => { });

        source.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void MergeManyChangeSetsDeliversErrorWithoutThrowing()
    {
        using var source = new Subject<IChangeSet<Person>>();
        Exception? error = null;

        using var subscription = source
            .MergeManyChangeSets(_ => Observable.Empty<IChangeSet<Person>>(), EqualityComparer<Person>.Default)
            .Subscribe(_ => { }, ex => error = ex, () => { });

        source.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void MergeChangeSetsWithComparerDoesNotRecurse()
    {
        using var sources = new SourceList<IObservable<IChangeSet<Person, string>>>();
        var completed = false;

        // This used to bind to itself and exhaust the stack before returning.
        using var subscription = sources.Connect()
            .MergeChangeSets(SortExpressionComparer<Person>.Ascending(p => p.Name))
            .Subscribe(_ => { }, () => completed = true);

        sources.Dispose();

        completed.Should().BeTrue();
    }
}
