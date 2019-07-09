using System.Collections.Generic;
using System.Linq;
using DynamicData.Kernel;
using Xunit;
using System.Collections.ObjectModel;
using FluentAssertions;

namespace DynamicData.Tests.List
{
    
    public class CloneChangesFixture
    {
        private readonly ChangeAwareList<int> _source;
        private readonly List<int> _clone;

        public  CloneChangesFixture()
        {
            _source = new ChangeAwareList<int>();
            _clone = new List<int>();
        }

        [Fact]
        public void Add()
        {
            _source.Add(1);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);

            //assert collection
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void AddSecond()
        {
            _source.Add(1);
            _source.Add(2);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void AddManyInSuccession()
        {
            Enumerable.Range(1, 10)
                      .ForEach(_source.Add);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void AddRange()
        {
            _source.AddRange(Enumerable.Range(1, 10));

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void AddSecondRange()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.AddRange(Enumerable.Range(11, 10));
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void InsertRangeInCentre()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.InsertRange(Enumerable.Range(11, 10), 5);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void Remove()
        {
            _source.Add(1);
            _source.Remove(1);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void RemoveRange()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.RemoveRange(5, 3);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void RemoveSucession()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.ClearChanges();

            _source.ToArray().ForEach(i => _source.Remove(i));
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void RemoveSucessionReversed()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.ClearChanges();

            _source.OrderByDescending(i => i).ToArray().ForEach(i => _source.Remove(i));

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void RemoveMany()
        {
            _source.AddRange(Enumerable.Range(1, 10));

            _source.RemoveMany(Enumerable.Range(1, 10));
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void RemoveInnerRange()
        {
            _source.AddRange(Enumerable.Range(1, 10));

            _source.RemoveRange(5, 3);
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void RemoveManyPartial()
        {
            _source.AddRange(Enumerable.Range(1, 10));

            _source.RemoveMany(Enumerable.Range(3, 5));
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            _clone.ShouldAllBeEquivalentTo(_source);
        }

        [Fact]
        public void MovedItemInObservableCollectionIsMoved()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.Move(1, 2);

            var clone = new ObservableCollection<int>();
            var changes = _source.CaptureChanges();
            var itemMoved = false;

            clone.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Move)
                    itemMoved = true;
            };

            clone.Clone(changes);

            itemMoved.Should().BeTrue();
        }
    }
}
