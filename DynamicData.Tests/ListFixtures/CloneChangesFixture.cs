using System.Collections.Generic;
using System.Linq;
using DynamicData.Internal;
using DynamicData.Kernel;
using DynamicData.List.Internal;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    internal class CloneChangesFixture
    {
        private ChangeAwareList<int> _source;
        private List<int> _clone;

        [SetUp]
        public void Setup()
        {
            _source = new ChangeAwareList<int>();
            _clone = new List<int>();
        }

        [Test]
        public void Add()
        {
            _source.Add(1);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);

            //assert collection
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void AddSecond()
        {
            _source.Add(1);
            _source.Add(2);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void AddManyInSuccession()
        {
            Enumerable.Range(1, 10)
                      .ForEach(_source.Add);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void AddRange()
        {
            _source.AddRange(Enumerable.Range(1, 10));

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void AddSecondRange()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.AddRange(Enumerable.Range(11, 10));
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void InsertRangeInCentre()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.InsertRange(Enumerable.Range(11, 10), 5);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void Remove()
        {
            _source.Add(1);
            _source.Remove(1);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void RemoveRange()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.RemoveRange(5, 3);

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void RemoveSucession()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.ClearChanges();

            _source.ToArray().ForEach(i => _source.Remove(i));
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void RemoveSucessionReversed()
        {
            _source.AddRange(Enumerable.Range(1, 10));
            _source.ClearChanges();

            _source.OrderByDescending(i => i).ToArray().ForEach(i => _source.Remove(i));

            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void RemoveMany()
        {
            _source.AddRange(Enumerable.Range(1, 10));

            _source.RemoveMany(Enumerable.Range(1, 10));
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void RemoveInnerRange()
        {
            _source.AddRange(Enumerable.Range(1, 10));

            _source.RemoveRange(5, 3);
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }

        [Test]
        public void RemoveManyPartial()
        {
            _source.AddRange(Enumerable.Range(1, 10));

            _source.RemoveMany(Enumerable.Range(3, 5));
            var changes = _source.CaptureChanges();
            _clone.Clone(changes);
            CollectionAssert.AreEqual(_source, _clone);
        }
    }
}
