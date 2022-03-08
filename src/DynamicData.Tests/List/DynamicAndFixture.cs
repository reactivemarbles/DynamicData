using System;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class DynamicAndFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<IObservable<IChangeSet<int>>> _source;

    private readonly ISourceList<int> _source1;

    private readonly ISourceList<int> _source2;

    private readonly ISourceList<int> _source3;

    public DynamicAndFixture()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _source3 = new SourceList<int>();
        _source = new SourceList<IObservable<IChangeSet<int>>>();
        _results = _source.And().AsAggregator();
    }

    [Fact]
    public void AddAndRemoveLists()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source3.AddRange(Enumerable.Range(1, 5));

        _source.Add(_source1.Connect());
        _source.Add(_source3.Connect());

        var result = Enumerable.Range(1, 5).ToArray();

        _results.Data.Count.Should().Be(5);
        _results.Data.Items.Should().BeEquivalentTo(result);

        _source2.AddRange(Enumerable.Range(6, 5));
        _results.Data.Count.Should().Be(5);

        _source.Add(_source2.Connect());
        _results.Data.Count.Should().Be(0);

        _source.RemoveAt(2);
        _results.Data.Count.Should().Be(5);
        _results.Data.Items.Should().BeEquivalentTo(result);
    }

    [Fact]
    public void ClearOneClearsResult()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(1, 5));
        _source1.Clear();
        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void CombineRange()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 10));
        _source2.AddRange(Enumerable.Range(6, 10));
        _results.Data.Count.Should().Be(5);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(6, 5));
    }

    public void Dispose()
    {
        _source1.Dispose();
        _source2.Dispose();
        _source3.Dispose();
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void ExcludedWhenItemIsInOneSource()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void IncludedWhenItemIsInTwoSources()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        _results.Data.Count.Should().Be(1);
    }

    [Fact]
    public void RemovedWhenNoLongerInBoth()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        _source1.Remove(1);
        _results.Data.Count.Should().Be(0);
    }
}
