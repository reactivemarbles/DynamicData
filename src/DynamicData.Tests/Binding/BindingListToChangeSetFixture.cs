using System;
using System.ComponentModel;
using System.Linq;

using DynamicData.Binding;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class BindingListToChangeSetFixture : IDisposable
{
    private readonly TestBindingList<int> _collection;

    private readonly ChangeSetAggregator<int> _results;

    public BindingListToChangeSetFixture()
    {
        _collection = new TestBindingList<int>();
        _results = _collection.ToObservableChangeSet().AsAggregator();
    }

    [Fact]
    public void Add()
    {
        _collection.Add(1);

        _results.Messages.Count.Should().Be(1);
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
    public void RaiseListChangedEvents()
    {
        _collection.RaiseListChangedEvents = true;
        _collection.Add(1);

        _results.Messages.Count.Should().Be(1);

        _collection.RaiseListChangedEvents = false;
        _collection.Add(1);

        _results.Messages.Count.Should().Be(1);
    }

    [Fact]
    public void RefreshCausesReplace()
    {
        // Arrange
        var sourceCache = new SourceCache<Item, Guid>(item => item.Id);

        var item1 = new Item("Old Name");

        sourceCache.AddOrUpdate(item1);

        var collection = new TestBindingList<Item>();

        var sourceCacheResults = sourceCache.Connect().AutoRefresh(item => item.Name).Bind(collection).AsAggregator();

        var collectionResults = collection.ToObservableChangeSet().AsAggregator();

        item1.Name = "New Name";

        // Source cache received add and refresh
        sourceCacheResults.Messages.Count.Should().Be(2);
        sourceCacheResults.Messages.First().Adds.Should().Be(1);
        sourceCacheResults.Messages.Last().Refreshes.Should().Be(1);


        /*
             List receives add and replace instead of refresh (and as of 23/02/2023 it receives a refresh too!)
         */

        collectionResults.Messages.Count.Should().Be(3);
        collectionResults.Messages.First().Adds.Should().Be(1);
        collectionResults.Messages.First().Refreshes.Should().Be(0);
        collectionResults.Messages.Last().Replaced.Should().Be(1);
        collectionResults.Messages.Last().Refreshes.Should().Be(0);

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
        _results.Data.Items.Should().BeEquivalentTo(_collection);
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
        _results.Data.Items.Should().BeEquivalentTo(_collection);

        var resetNotification = _results.Messages.Last();
        resetNotification.Removes.Should().Be(10);
        resetNotification.Adds.Should().Be(10);
    }


    [Fact]
    public void InsertInto()
    {
        //Fixes https://github.com/reactivemarbles/DynamicData/issues/507
        var target = new ObservableCollectionExtended<string>();

        var bindingList = new BindingList<string>() { "a", "b", "c", "d" };
        bindingList.ToObservableChangeSet()
            .Bind(target)
            .Subscribe();

        bindingList.Insert(2, "Z at index 2");

        target.Should().BeEquivalentTo("a", "b", "Z at index 2", "c", "d");
    }

    private class TestBindingList<T> : BindingList<T>
    {
        public void Reset() => OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }
}
