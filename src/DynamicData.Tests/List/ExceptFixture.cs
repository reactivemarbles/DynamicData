using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class ExceptFixture : ExceptFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable() => Source1.Connect().Except(Source2.Connect());
}

public class ExceptCollectionFixture : ExceptFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<int>>> { Source1.Connect(), Source2.Connect() };
        return l.Except();
    }
}

public abstract class ExceptFixtureBase : IDisposable
{
    protected ISourceList<int> Source1;

    protected ISourceList<int> Source2;

    private readonly ChangeSetAggregator<int> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected ExceptFixtureBase()
    {
        Source1 = new SourceList<int>();
        Source2 = new SourceList<int>();
        _results = CreateObservable().AsAggregator();
    }

    [Fact]
    public void AddedWhenNoLongerInSecond()
    {
        Source1.Add(1);
        Source2.Add(1);
        Source2.Remove(1);
        _results.Data.Count.Should().Be(1);
    }

    [Fact]
    public void ClearFirstClearsResult()
    {
        Source1.AddRange(Enumerable.Range(1, 5));
        Source2.AddRange(Enumerable.Range(1, 5));
        Source1.Clear();
        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void ClearSecondEnsuresFirstIsIncluded()
    {
        Source1.AddRange(Enumerable.Range(1, 5));
        Source2.AddRange(Enumerable.Range(1, 5));
        _results.Data.Count.Should().Be(0);
        Source2.Clear();
        _results.Data.Count.Should().Be(5);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(1, 5));
    }

    [Fact]
    public void CombineRange()
    {
        Source1.AddRange(Enumerable.Range(1, 10));
        Source2.AddRange(Enumerable.Range(6, 10));
        _results.Data.Count.Should().Be(5);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(1, 5));
    }

    public void Dispose()
    {
        Source1.Dispose();
        Source2.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void ExcludedWhenItemIsInTwoSources()
    {
        Source1.Add(1);
        Source2.Add(1);
        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void IncludedWhenItemIsInOneSource()
    {
        Source1.Add(1);
        _results.Data.Count.Should().Be(1);
    }

    [Fact]
    public void NothingFromOther()
    {
        Source2.Add(1);
        _results.Data.Count.Should().Be(0);
    }

    protected abstract IObservable<IChangeSet<int>> CreateObservable();
}
