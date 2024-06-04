using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class OrFixture : OrFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable() => _source1.Connect().Or(_source2.Connect());
}

public class OrCollectionFixture : OrFixtureBase
{
    protected override IObservable<IChangeSet<int>> CreateObservable()
    {
        var list = new List<IObservable<IChangeSet<int>>> { _source1.Connect(), _source2.Connect() };
        return list.Or();
    }
}

public class OrRefreshFixture
{
    [Fact]
    public void RefreshPassesThrough()
    {
        SourceList<Item> source1 = new();
        source1.Add(new Item("A"));
        SourceList<Item> source2 = new();
        source2.Add(new Item("B"));

        var list = new List<IObservable<IChangeSet<Item>>> { source1.Connect().AutoRefresh(), source2.Connect().AutoRefresh() };
        var results = list.Or().AsAggregator();
        source1.Items.ElementAt(0).Name = "Test";

        results.Data.Count.Should().Be(2);
        results.Messages.Count.Should().Be(3);
        results.Messages[2].Refreshes.Should().Be(1);
        results.Messages[2].First().Item.Current.Should().Be(source1.Items[0]);
    }
}

public class OrReplaceFixture
{
    [Fact]
    public void ItemIsReplaced()
    {
        var item1 = new Item("A");
        var item2 = new Item("B");
        var item1Replacement = new Item("Test");

        SourceList<Item> source1 = new();
        source1.Add(item1);
        SourceList<Item> source2 = new();
        source2.Add(item2);

        var list = new List<IObservable<IChangeSet<Item>>> { source1.Connect(), source2.Connect() };
        var results = list.Or().AsAggregator();
        source1.ReplaceAt(0, item1Replacement);

        results.Data.Count.Should().Be(2);
        results.Messages.Count.Should().Be(3);
        results.Data.Items.Should().BeEquivalentTo(new[] { item1Replacement, item2});
    }
}

public abstract class OrFixtureBase : IDisposable
{
    protected ISourceList<int> _source1;

    protected ISourceList<int> _source2;

    private readonly ChangeSetAggregator<int> _results;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2214:Do not call overridable methods in constructors", Justification = "Accepted as part of a test.")]
    protected OrFixtureBase()
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
    public void IncludedWhenItemIsInTwoSources()
    {
        _source1.Add(1);
        _source2.Add(1);
        _results.Data.Count.Should().Be(1);
        _results.Data.Items[0].Should().Be(1);
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
