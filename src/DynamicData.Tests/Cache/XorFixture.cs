using System;
using System.Collections.Generic;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Tests.Cache;

public class XOrFixture : XOrFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable() => _source1.Connect().Xor(_source2.Connect());
}

public class XOrCollectionFixture : XOrFixtureBase
{
    protected override IObservable<IChangeSet<Person, string>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<Person, string>>> { _source1.Connect(), _source2.Connect() };
        return l.Xor();
    }
}

public abstract class XOrFixtureBase : IDisposable
{
    protected ISourceCache<Person, string> _source1;

    protected ISourceCache<Person, string> _source2;

    private readonly ChangeSetAggregator<Person, string> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected XOrFixtureBase()
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
        _results.Messages.Count.Should().Be(3, "Should be 2 updates");
        _results.Data.Count.Should().Be(1, "Cache should have no items");
    }

    [Fact]
    public void UpdatingBothDoeNotProducesResult()
    {
        var person = new Person("Adult1", 50);
        _source1.AddOrUpdate(person);
        _source2.AddOrUpdate(person);
        _results.Data.Count.Should().Be(0, "Cache should have no items");
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
        _results.Data.Count.Should().Be(0, "Cache should have no items");
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

    [Fact]
    public void CompletesOnlyWhenEverySourceCompletes()
    {
        var completed = false;

        using var first = new Subject<IChangeSet<Person, string>>();
        using var second = new Subject<IChangeSet<Person, string>>();
        using var subscription = first.Xor(second).Subscribe(_ => { }, () => completed = true);

        first.OnCompleted();
        completed.Should().BeFalse("the second source is still live");

        second.OnCompleted();
        completed.Should().BeTrue("every source has now finished");
    }

    [Fact]
    public void DeliversAnErrorFromAnySource()
    {
        Exception? error = null;

        using var first = new Subject<IChangeSet<Person, string>>();
        using var second = new Subject<IChangeSet<Person, string>>();
        using var subscription = first.Xor(second).Subscribe(_ => { }, ex => error = ex, () => { });

        second.OnError(new InvalidOperationException("boom"));

        error.Should().BeOfType<InvalidOperationException>();
    }
}
