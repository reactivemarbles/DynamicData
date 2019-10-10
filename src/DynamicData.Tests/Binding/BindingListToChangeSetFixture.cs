using DynamicData.Binding;
using FluentAssertions;
using System;
using System.ComponentModel;
using System.Linq;
using Xunit;

namespace DynamicData.Tests.Binding
{
    public class BindingListToChangeSetFixture : IDisposable
    {
        private readonly TestBindingList<int> _collection;
        private readonly ChangeSetAggregator<int> _results;

        public BindingListToChangeSetFixture()
        {
            _collection = new TestBindingList<int>();
            _results = _collection.ToObservableChangeSet().AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
        }

        [Fact]
        public void Add()
        {
            _collection.Add(1);

            _results.Messages.Count.Should().Be(1);
            _results.Data.Count.Should().Be(1);
            _results.Data.Items.First().Should().Be(1);
        }

        [Fact]
        public void Remove()
        {
            _collection.AddRange(Enumerable.Range(1, 10));

            _collection.Remove(3);

            _results.Data.Count.Should().Be(9);
            _results.Data.Items.Contains(3).Should().BeFalse();
            _results.Data.Items.ShouldAllBeEquivalentTo(_collection);
        }

        [Fact]
        public void Duplicates()
        {
            _collection.Add(1);
            _collection.Add(1);

            _results.Data.Count.Should().Be(2);
        }

        [Fact]
        public void Replace()
        {
            _collection.AddRange(Enumerable.Range(1, 10));
            _collection[8] = 20;

            _results.Data.Items.ShouldBeEquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 20, 10 });
        }

        [Fact]
        public void ResetFiresClearsAndAdds()
        {
            _collection.AddRange(Enumerable.Range(1, 10));

            _collection.Reset();
            _results.Data.Items.ShouldAllBeEquivalentTo(_collection);

            var resetNotification = _results.Messages.Last();
            resetNotification.Removes.Should().Be(10);
            resetNotification.Adds.Should().Be(10);
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

        private class TestBindingList<T> : BindingList<T>
        {
            public void Reset()
            {
                OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
            }
        }
    }
}
