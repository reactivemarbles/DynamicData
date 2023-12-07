using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class AndFixture : AndFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable() => _source1.Connect().And(_source2.Connect());
}

public class AndCollectionFixture : AndFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable()
    {
        var l = new List<IObservable<IChangeSet<int>>> { _source1.Connect(), _source2.Connect() };
        return l.And();
    }
}

public abstract class AndFixtureBase : IDisposable
{
    protected ISourceList<int> _source1;

    protected ISourceList<int> _source2;

    private readonly ChangeSetAggregator<int> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected AndFixtureBase()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _results = CreateObservable().AsAggregator();
    }

    [Fact]
    public void ClearOneClearsResult()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(1, 5));
        _source1.Clear();
        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void CombineRange()
    {
        _source1.AddRange(Enumerable.Range(1, 10));
        _source2.AddRange(Enumerable.Range(6, 10));
        _results.Data.Count.Should().Be(5);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(6, 5));
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void ExcludedWhenItemIsInOneSource()
    {
        _source1.Add(1);
        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void IncludedWhenItemIsInTwoSources()
    {
        _source1.Add(1);
        _source2.Add(1);
        _results.Data.Count.Should().Be(1);
    }

    [Fact]
    public void RemovedWhenNoLongerInBoth()
    {
        _source1.Add(1);
        _source2.Add(1);
        _source1.Remove(1);
        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void StartingWithNonEmptySourceProducesNoResult()
    {
        _source1.Add(1);

        using var result = CreateObservable().AsAggregator();
        result.Data.Count.Should().Be(0);
    }

    protected abstract IObservable<IChangeSet<int>> CreateObservable();
}
