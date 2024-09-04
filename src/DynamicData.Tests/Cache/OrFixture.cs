using System;
using System.Collections.Generic;
using System.Linq;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class OrFixture : OrFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable() => _source1.Connect().Or(_source2.Connect());
}

public sealed class OrCollectionFixture : OrFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<Person, string>>> { _source1.Connect(), _source2.Connect() };
        return l.Or();
    }
}

public abstract class OrFixtureBase : IDisposable
{
    protected ISourceCache<Person, string> _source1;

    protected ISourceCache<Person, string> _source2;

    private readonly ChangeSetAggregator<Person, string> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected OrFixtureBase()
    {
        _source1 = new SourceCache<Person, string>(p => p.Name);
        _source2 = new SourceCache<Person, string>(p => p.Name);
        _results = CreateObservable().AsAggregator();
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void RemovingFromOneDoesNotFromResult()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        _source2.Remove(person);
        _results.Messages.Count.Should().Be(1, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Cache should have no items");
    }

    [Fact]
    public void UpdatingBothProducesResultsAndDoesNotDuplicateTheMessage()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);
        _results.Messages.Count.Should().Be(1, "Should have no updates");
        _results.Data.Count.Should().Be(1, "Cache should have no items");
        _results.Data.Items[0].Should().Be(person, "Should be same person");
    }

    [Fact]
    public void UpdatingOneProducesOnlyOneUpdate()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);

        var personUpdated = new Person("Adult1", 51);
        _source2.AddOrUpdate(personUpdated);
        _results.Messages.Count.Should().Be(2, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Cache should have no items");
        _results.Data.Items[0].Should().Be(personUpdated, "Should be updated person");
    }

    [Fact]
    public void UpdatingOneSourceOnlyProducesResult()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);

        _results.Messages.Count.Should().Be(1, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");
    }

    protected abstract IObservable<IChangeSet<Person, string>> CreateObservable();
}
