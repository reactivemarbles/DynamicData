using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.Cache;

public class ToSortedCollectionFixture : IDisposable
{
    private readonly SourceCache<Person, int> _cache;

    private readonly CompositeDisposable _cleanup = new();

    private readonly List<Person> _sortedCollection = new();

    private readonly List<Person> _unsortedCollection = new();

    public ToSortedCollectionFixture()
    {
        _cache = new SourceCache<Person, int>(p => p.Age);
        _cache.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray());
    }

    public void Dispose()
    {
        _cache.Dispose();
        _cleanup.Dispose();
    }

    [Fact]
    public void SortAscending()
    {
        TestScheduler testScheduler = new();

        _cleanup.Add(
            _cache.Connect().ObserveOn(testScheduler).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).ToCollection().Do(
                persons =>
                {
                    _unsortedCollection.Clear();
                    _unsortedCollection.AddRange(persons);
                }).Subscribe());

        _cleanup.Add(
            _cache.Connect().ObserveOn(testScheduler).ToSortedCollection(p => p.Age).Do(
                persons =>
                {
                    _sortedCollection.Clear();
                    _sortedCollection.AddRange(persons);
                }).Subscribe());

        // Insert an item with a lower sort order
        _cache.AddOrUpdate(new Person("Name", 0));

        testScheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        _cache.Items.Should().Equal(_unsortedCollection);
        _cache.Items.Should().NotEqual(_sortedCollection);
        _cache.Items.OrderBy(p => p.Age).Should().Equal(_sortedCollection);
    }

    [Fact]
    public void SortDescending()
    {
        TestScheduler testScheduler = new();

        _cleanup.Add(
            _cache.Connect().ObserveOn(testScheduler).Sort(SortExpressionComparer<Person>.Ascending(p => p.Age)).ToCollection().Do(
                persons =>
                {
                    _unsortedCollection.Clear();
                    _unsortedCollection.AddRange(persons);
                }).Subscribe());

        _cleanup.Add(
            _cache.Connect().ObserveOn(testScheduler).ToSortedCollection(p => p.Age, SortDirection.Descending).Do(
                persons =>
                {
                    _sortedCollection.Clear();
                    _sortedCollection.AddRange(persons);
                }).Subscribe());

        // Insert an item with a lower sort order
        _cache.AddOrUpdate(new Person("Name", 0));

        testScheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        _cache.Items.Should().Equal(_unsortedCollection);
        _cache.Items.Should().NotEqual(_sortedCollection);
        _cache.Items.OrderByDescending(p => p.Age).Should().Equal(_sortedCollection);
    }
}
