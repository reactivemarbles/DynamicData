using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

using DynamicData.Binding;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class ReadOnlyObservableCollectionToChangeSetFixture : IDisposable
{
    private readonly TestObservableCollection<int> _collection;

    private readonly ChangeSetAggregator<int> _results;

    private readonly ReadOnlyObservableCollection<int> _target;

    public ReadOnlyObservableCollectionToChangeSetFixture()
    {
        _collection = new TestObservableCollection<int>();
        _target = new ReadOnlyObservableCollection<int>(_collection);
        _results = _target.ToObservableChangeSet().AsAggregator();
    }

    [Fact]
    public void Add()
    {
        _collection.Add(1);

        _results.Messages.Count.Should().Be(2);
        _results.Data.Count.Should().Be(1);
        _results.Data.Items[0].Should().Be(1);
    }

    public void Dispose() => _results.Dispose();

    [Fact]
    public void Duplicates()
    {
        _collection.Add(1);
        _collection.Add(1);

        _results.Data.Count.Should().Be(2);
    }

    [Fact]
    public void Move()
    {
        _collection.AddRange(Enumerable.Range(1, 10));

        _results.Data.Items.Should().BeEquivalentTo(_target);
        _collection.Move(5, 8);
        _results.Data.Items.Should().BeEquivalentTo(_target);

        _collection.Move(7, 1);
        _results.Data.Items.Should().BeEquivalentTo(_target);
    }

    [Fact]
    public void RefreshNotSupported()
    {
        // Arrange
        var sourceCache = new SourceCache<Item, Guid>(item => item.Id);

        var item1 = new Item("Old Name");

        sourceCache.AddOrUpdate(item1);

        var sourceCacheResults = sourceCache.Connect().AutoRefresh(item => item.Name).Bind(out var collection).AsAggregator();

        var collectionResults = collection.ToObservableChangeSet().AsAggregator();

        item1.Name = "New Name";

        // Source cache received add and refresh
        sourceCacheResults.Messages.Count.Should().Be(2);
        sourceCacheResults.Messages.First().Adds.Should().Be(1);
        sourceCacheResults.Messages.Last().Refreshes.Should().Be(1);

        // Collection only receives add and NOT refresh
        // System.Collections.Specialized.NotifyCollectionChangedAction does not have an enum to describe the same item being refreshed.
        // https://docs.microsoft.com/en-us/dotnet/api/system.collections.specialized.notifycollectionchangedaction
        collectionResults.Messages.Count.Should().Be(1);
        collectionResults.Messages.First().Adds.Should().Be(1);

        sourceCache.Dispose();
        sourceCacheResults.Dispose();
        collectionResults.Dispose();
    }

    [Fact]
    public void Remove()
    {
        _collection.AddRange(Enumerable.Range(1, 10));

        _collection.Remove(3);

        _results.Data.Count.Should().Be(9);
        _results.Data.Items.Contains(3).Should().BeFalse();
        _results.Data.Items.Should().BeEquivalentTo(_target);
    }

    [Fact]
    public void Replace()
    {
        _collection.AddRange(Enumerable.Range(1, 10));
        _collection[8] = 20;

        _results.Data.Items.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 20, 10 });
    }

    [Fact]
    public void ResetFiresClearsAndAdds()
    {
        _collection.AddRange(Enumerable.Range(1, 10));

        _collection.Reset();
        _results.Data.Items.Should().BeEquivalentTo(_target);

        var resetNotification = _results.Messages.Last();
        resetNotification.Removes.Should().Be(10);
        resetNotification.Adds.Should().Be(10);
    }

    private class TestObservableCollection<T> : ObservableCollection<T>
    {
        public void Reset() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
