using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class XOrFixture : XOrFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable() => _source1.Connect().Xor(_source2.Connect());
}

public class XOrCollectionFixture : XOrFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<int>>> { _source1.Connect(), _source2.Connect() };
        return l.Xor();
    }
}

public abstract class XOrFixtureBase : IDisposable
{
    protected ISourceList<int> _source1;

    protected ISourceList<int> _source2;

    private readonly ChangeSetAggregator<int> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected XOrFixtureBase()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _results = CreateObservable().AsAggregator();
    }

    [Fact]
    public void ClearOnlyClearsOneSource()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _source1.Clear();
        _results.Data.Count.Should().Be(5);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(6, 5));
    }

    [Fact]
    public void CombineRange()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _results.Data.Count.Should().Be(10);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(1, 10));
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void IncludedWhenItemIsInOneSource()
    {
        _source1.Add(1);

        _results.Data.Count.Should().Be(1);
        _results.Data.Items[0].Should().Be(1);
    }

    [Fact]
    public void NotIncludedWhenItemIsInTwoSources()
    {
        _source1.Add(1);
        _source2.Add(1);
        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void OverlappingRangeExludesInteresct()
    {
        _source1.AddRange(Enumerable.Range(1, 10));
        _source2.AddRange(Enumerable.Range(6, 10));
        _results.Data.Count.Should().Be(10);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(1, 5).Union(Enumerable.Range(11, 5)));
    }

    [Fact]
    public void RemovedWhenNoLongerInBoth()
    {
        _source1.Add(1);
        _source2.Add(1);
        _source1.Remove(1);
        _results.Data.Count.Should().Be(1);
    }

    [Fact]
    public void RemovedWhenNoLongerInEither()
    {
        _source1.Add(1);
        _source1.Remove(1);
        _results.Data.Count.Should().Be(0);
    }

    protected abstract IObservable<IChangeSet<int>> CreateObservable();
}
