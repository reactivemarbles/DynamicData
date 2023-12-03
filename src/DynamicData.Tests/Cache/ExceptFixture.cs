using System;
using System.Collections.Generic;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class ExceptFixture : ExceptFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable() => _targetSource.Connect().Except(_exceptSource.Connect());
}

public class ExceptCollectionFixture : ExceptFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<Person, string>>> { _targetSource.Connect(), _exceptSource.Connect() };
        return l.Except();
    }
}

public abstract class ExceptFixtureBase : IDisposable
{
    protected ISourceCache<Person, string> _exceptSource;

    protected ISourceCache<Person, string> _targetSource;

    private readonly ChangeSetAggregator<Person, string> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected ExceptFixtureBase()
    {
        _targetSource = new SourceCache<Person, string>(p => p.Name);
        _exceptSource = new SourceCache<Person, string>(p => p.Name);
        _results = CreateObservable().AsAggregator();
    }

    public void Dispose()
    {
        _targetSource.Dispose();
        _exceptSource.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void DoNotIncludeExceptListItems()
    {
        var person = new Person("Adult1", 50);
        _exceptSource.AddOrUpdate(person);
        _targetSource.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(0, "Should have no updates");
        _results.Data.Count.Should().Be(0, "Cache should have no items");
    }

    [Fact]
    public void RemovedAnItemFromExceptThenIncludesTheItem()
    {
        var person = new Person("Adult1", 50);
        _exceptSource.AddOrUpdate(person);
        _targetSource.AddOrUpdate(person);

        _exceptSource.Remove(person);
        _results.Messages.Count.Should().Be(1, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Cache should have no items");
    }

    [Fact]
    public void UpdatingOneSourceOnlyProducesResult()
    {
        var person = new Person("Adult1", 50);
        _targetSource.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
    }

    protected abstract IObservable<IChangeSet<Person, string>> CreateObservable();
}
