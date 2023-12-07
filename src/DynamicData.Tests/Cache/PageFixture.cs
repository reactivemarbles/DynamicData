using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class PageFixture : IDisposable
{
    private readonly PagedChangeSetAggregator<Person, string> _aggregators;

    private readonly IComparer<Person> _comparer;

    private readonly RandomPersonGenerator _generator = new();

    private readonly ISubject<IPageRequest> _pager;

    private readonly ISubject<IComparer<Person>> _sort;

    private readonly ISourceCache<Person, string> _source;

    public PageFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _comparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);
        _sort = new BehaviorSubject<IComparer<Person>>(_comparer);
        _pager = new BehaviorSubject<IPageRequest>(new PageRequest(1, 25));

        _aggregators = _source.Connect().Sort(_sort, resetThreshold: 200).Page(_pager).AsAggregator();
    }

    [Fact]
    public void ChangePage()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);
        _pager.OnNext(new PageRequest(2, 25));

        var expectedResult = people.OrderBy(p => p, _comparer).Skip(25).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages[1].SortedItems.ToList();

        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void ChangePageSize()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);
        _pager.OnNext(new PageRequest(1, 50));

        _aggregators.Messages[1].Response.Page.Should().Be(1, "Should be page 1");

        var expectedResult = people.OrderBy(p => p, _comparer).Take(50).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages[1].SortedItems.ToList();

        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    public void Dispose()
    {
        _source.Dispose();
        _aggregators.Dispose();
    }

    [Fact]
    public void PageGreaterThanNumberOfPagesAvailable()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);
        _pager.OnNext(new PageRequest(10, 25));

        _aggregators.Messages[1].Response.Page.Should().Be(4, "Page should move to the last page");

        var expectedResult = people.OrderBy(p => p, _comparer).Skip(75).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages[1].SortedItems.ToList();

        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void PageInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddOrUpdate(people);

        _aggregators.Data.Count.Should().Be(25, "Should be 25 people in the cache");
        _aggregators.Messages[0].Response.PageSize.Should().Be(25, "Page size should be 25");
        _aggregators.Messages[0].Response.Page.Should().Be(1, "Should be page 1");
        _aggregators.Messages[0].Response.Pages.Should().Be(4, "Should be page 4 pages");

        var expectedResult = people.OrderBy(p => p, _comparer).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages[0].SortedItems.ToList();

        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void ReorderBelowThreshold()
    {
        var people = _generator.Take(50).ToArray();
        _source.AddOrUpdate(people);

        var changed = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);
        _sort.OnNext(changed);

        var expectedResult = people.OrderBy(p => p, changed).Take(25).Select(p => new KeyValuePair<string, Person>(p.Name, p)).ToList();
        var actualResult = _aggregators.Messages.Last().SortedItems.ToList();
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void ThrowsForNegativePage() => Assert.Throws<ArgumentException>(() => _pager.OnNext(new PageRequest(-1, 1)));

    [Fact]
    public void ThrowsForNegativeSizeParameters() => Assert.Throws<ArgumentException>(() => _pager.OnNext(new PageRequest(1, -1)));
}
