using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using DynamicData.Binding;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Binding
{
    [TestFixture]
    public class ReadOnlyObservableCollectionToChangeSetFixture
    {
        private TestObservableCollection<int> _collection;
        private ChangeSetAggregator<int> _results;
        private ReadOnlyObservableCollection<int> _target;

        [SetUp]
        public void SetUp()
        {
            _collection = new TestObservableCollection<int>();
            _target = new ReadOnlyObservableCollection<int>(_collection);
            _results = _target.ToObservableChangeSet().AsAggregator();
        }

        [TearDown]
        public void CleanUp()
        {
            _results.Dispose();
        }

        [Test]
        public void Move()
        {
            _collection.AddRange(Enumerable.Range(1, 10));

            _results.Data.Items.ShouldAllBeEquivalentTo(_target);
            _collection.Move(5, 8);
            _results.Data.Items.ShouldAllBeEquivalentTo(_target);

            _collection.Move(7, 1);
            _results.Data.Items.ShouldAllBeEquivalentTo(_target);
        }

        [Test]
        public void Add()
        {
            _collection.Add(1);

            _results.Messages.Count.Should().Be(1);
            _results.Data.Count.Should().Be(1);
            _results.Data.Items.First().Should().Be(1);
        }

        [Test]
        public void Remove()
        {
            _collection.AddRange(Enumerable.Range(1, 10));

            _collection.Remove(3);

            _results.Data.Count.Should().Be(9);
            _results.Data.Items.Contains(3).Should().BeFalse();
            _results.Data.Items.ShouldAllBeEquivalentTo(_target);
        }

        [Test]
        public void Duplicates()
        {
            _collection.Add(1);
            _collection.Add(1);

            _results.Data.Count.Should().Be(2);
        }

        [Test]
        public void Replace()
        {
            _collection.AddRange(Enumerable.Range(1, 10));
            _collection[8] = 20;

            _results.Data.Items.ShouldBeEquivalentTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 20, 10 });
        }

        [Test]
        public void ResetFiresClearsAndAdds()
        {
            _collection.AddRange(Enumerable.Range(1, 10));

            _collection.Reset();
            _results.Data.Items.ShouldAllBeEquivalentTo(_target);

            var resetNotification = _results.Messages.Last();
            resetNotification.Removes.Should().Be(10);
            resetNotification.Adds.Should().Be(10);
        }

        private class TestObservableCollection<T> : ObservableCollection<T>
        {
            public void Reset()
            {
                this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}