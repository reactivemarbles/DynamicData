using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class QueryWhenChangedFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public QueryWhenChangedFixture()
    {
        _source = new SourceList<Person>();
        _results = new ChangeSetAggregator<Person>(_source.Connect(p => p.Age > 20));
    }

    [Fact]
    public void CanHandleAddsAndUpdates()
    {
        var invoked = false;
        var subscription = _source.Connect().QueryWhenChanged(q => q.Count).Subscribe(query => invoked = true);

        var person = new Person("A", 1);
        _source.Add(person);
        _source.Remove(person);

        invoked.Should().BeTrue();
        subscription.Dispose();
    }

    [Fact]
    public void ChangeInvokedOnNext()
    {
        var invoked = false;

        var subscription = _source.Connect().QueryWhenChanged().Subscribe(x => invoked = true);

        invoked.Should().BeFalse();

        _source.Add(new Person("A", 1));
        invoked.Should().BeTrue();

        subscription.Dispose();
    }

    [Fact]
    public void ChangeInvokedOnNext_WithSelector()
    {
        var invoked = false;

        var subscription = _source.Connect().QueryWhenChanged(query => query.Count).Subscribe(x => invoked = true);

        invoked.Should().BeFalse();

        _source.Add(new Person("A", 1));
        invoked.Should().BeTrue();

        subscription.Dispose();
    }

    [Fact]
    public void ChangeInvokedOnSubscriptionIfItHasData()
    {
        var invoked = false;
        _source.Add(new Person("A", 1));
        var subscription = _source.Connect().QueryWhenChanged().Subscribe(x => invoked = true);
        invoked.Should().BeTrue();
        subscription.Dispose();
    }

    [Fact]
    public void ChangeInvokedOnSubscriptionIfItHasData_WithSelector()
    {
        var invoked = false;
        _source.Add(new Person("A", 1));
        var subscription = _source.Connect().QueryWhenChanged(query => query.Count).Subscribe(x => invoked = true);
        invoked.Should().BeTrue();
        subscription.Dispose();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void CompletesWhenTheSourceCompletes()
    {
        var completed = false;

        using var source = new Subject<IChangeSet<Person>>();
        using var subscription = source.QueryWhenChanged().Subscribe(_ => { }, () => completed = true);

        source.OnCompleted();

        completed.Should().BeTrue();
    }

    [Fact]
    public void DeliversTheError()
    {
        Exception? error = null;

        using var source = new Subject<IChangeSet<Person>>();
        using var subscription = source.QueryWhenChanged().Subscribe(_ => { }, ex => error = ex, () => { });

        source.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }
}
