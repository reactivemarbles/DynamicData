using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

using DynamicData.Binding;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;

public class ObservableCollectionToChangeSetFixture : IDisposable
{
    private readonly TestObservableCollection<int> _collection;

    private readonly ChangeSetAggregator<int> _results;

    public ObservableCollectionToChangeSetFixture()
    {
        _collection = new TestObservableCollection<int>();
        _results = _collection.ToObservableChangeSet().AsAggregator();
    }

    [Fact]
    public void Add()
    {
        _collection.Add(1);

        _results.Messages.Count.Should().BeGreaterOrEqualTo(1);
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

        _results.Data.Items.Should().BeEquivalentTo(_collection);
        _collection.Move(5, 8);
        _results.Data.Items.Should().BeEquivalentTo(_collection);

        _collection.Move(7, 1);
        _results.Data.Items.Should().BeEquivalentTo(_collection);
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

    private class TestObservableCollection<T> : ObservableCollection<T>
    {
        public void Reset() => OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
