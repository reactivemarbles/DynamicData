using System;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class DynamicOrRefreshFixture
{
    [Fact]
    public void RefreshPassesThrough()
    {
        var source1 = new SourceList<Item>();
        var source2 = new SourceList<Item>();
        var source = new SourceList<IObservable<IChangeSet<Item>>>();
        var results = source.Or().AsAggregator();

        source1.Add(new Item("A"));
        source2.Add(new Item("B"));
        source.AddRange(new[] { source1.Connect().AutoRefresh(), source2.Connect().AutoRefresh() });

        source1.Items.ElementAt(0).Name = "Test";

        results.Data.Count.Should().Be(2);
        results.Messages.Count.Should().Be(3);
        results.Messages[2].Refreshes.Should().Be(1);
        results.Messages[2].First().Item.Current.Should().Be(source1.Items[0]);
    }
}

public class DynamicOrFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<IObservable<IChangeSet<int>>> _source;

    private readonly ISourceList<int> _source1;

    private readonly ISourceList<int> _source2;

    private readonly ISourceList<int> _source3;

    public DynamicOrFixture()
    {
        _source1 = new SourceList<int>();
        _source2 = new SourceList<int>();
        _source3 = new SourceList<int>();
        _source = new SourceList<IObservable<IChangeSet<int>>>();
        _results = _source.Or().AsAggregator();
    }

    [Fact]
    public void AddAndRemoveLists()
    {
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _source3.AddRange(Enumerable.Range(100, 5));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        var result = Enumerable.Range(1, 5).Union(Enumerable.Range(6, 5)).Union(Enumerable.Range(100, 5));

        _results.Data.Count.Should().Be(15);
        _results.Data.Items.Should().BeEquivalentTo(result);

        _source.RemoveAt(1);
        _results.Data.Count.Should().Be(10);

        result = Enumerable.Range(1, 5).Union(Enumerable.Range(100, 5));
        _results.Data.Items.Should().BeEquivalentTo(result);
    }

    [Fact]
    public void ClearOnlyClearsOneSource()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _source1.Clear();
        _results.Data.Count.Should().Be(5);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(6, 5));
    }

    [Fact]
    public void ClearSource()
    {
        _source1.Add(0);
        _source2.Add(1);
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Clear();

        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void CombineRange()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.AddRange(Enumerable.Range(1, 5));
        _source2.AddRange(Enumerable.Range(6, 5));
        _results.Data.Count.Should().Be(10);
        _results.Data.Items.Should().BeEquivalentTo(Enumerable.Range(1, 10));
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
    public void IncludedWhenItemIsInOneSource()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);

        _results.Data.Count.Should().Be(1);
        _results.Data.Items[0].Should().Be(1);
    }

    [Fact]
    public void IncludedWhenItemIsInTwoSources()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source2.Add(1);
        _results.Data.Count.Should().Be(1);
        _results.Data.Items[0].Should().Be(1);
    }

    [Fact]
    public void ItemIsReplaced()
    {
        _source1.Add(0);
        _source2.Add(1);
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.ReplaceAt(0, 9);

        _results.Data.Count.Should().Be(2);
        _results.Messages.Count.Should().Be(3);
        _results.Data.Items.Should().BeEquivalentTo(new[] { 9, 1});
    }

    [Fact]
    public void RemoveAllLists()
    {
        _source1.AddRange(Enumerable.Range(1, 5));

        _source3.AddRange(Enumerable.Range(100, 5));

        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source.Add(_source3.Connect());

        _source2.AddRange(Enumerable.Range(6, 5));
        _source.Clear();

        _results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void RemovedWhenNoLongerInEither()
    {
        _source.Add(_source1.Connect());
        _source.Add(_source2.Connect());
        _source1.Add(1);
        _source1.Remove(1);
        _results.Data.Count.Should().Be(0);
    }
}
