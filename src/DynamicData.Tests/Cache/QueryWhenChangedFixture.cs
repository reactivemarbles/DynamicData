using System;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class QueryWhenChangedFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    public QueryWhenChangedFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _results = new ChangeSetAggregator<Person, string>(_source.Connect(p => p.Age > 20));
    }

    [Fact]
    public void ChangeInvokedOnNext()
    {
        var invoked = false;

        var subscription = _source.Connect().QueryWhenChanged().Subscribe(x => invoked = true);

        invoked.Should().BeFalse();

        _source.AddOrUpdate(new Person("A", 1));
        invoked.Should().BeTrue();

        subscription.Dispose();
    }

    [Fact]
    public void ChangeInvokedOnNext_WithSelector()
    {
        var invoked = false;

        var subscription = _source.Connect().QueryWhenChanged(query => query.Count).Subscribe(x => invoked = true);

        invoked.Should().BeFalse();

        _source.AddOrUpdate(new Person("A", 1));
        invoked.Should().BeTrue();

        subscription.Dispose();
    }

    [Fact]
    public void ChangeInvokedOnSubscriptionIfItHasData()
    {
        var invoked = false;
        _source.AddOrUpdate(new Person("A", 1));
        var subscription = _source.Connect().QueryWhenChanged().Subscribe(x => invoked = true);
        invoked.Should().BeTrue();
        subscription.Dispose();
    }

    [Fact]
    public void ChangeInvokedOnSubscriptionIfItHasData_WithSelector()
    {
        var invoked = false;
        _source.AddOrUpdate(new Person("A", 1));
        var subscription = _source.Connect().QueryWhenChanged(query => query.Count).Subscribe(x => invoked = true);
        invoked.Should().BeTrue();
        subscription.Dispose();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
