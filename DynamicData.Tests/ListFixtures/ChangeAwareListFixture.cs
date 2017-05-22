using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.List.Internal;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    internal class ChangeAwareListFixture
    {
        private ChangeAwareList<int> _list;

        [SetUp]
        public void Setup()
        {
            _list = new ChangeAwareList<int>();
        }

        [Test]
        public void Add()
        {
            _list.Add(1);

            //assert changes
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(1, changes.Adds);
            Assert.AreEqual(1, changes.First().Item.Current);

            //assert collection
            CollectionAssert.AreEqual(Enumerable.Range(1, 1), _list);
        }

        [Test]
        public void AddSecond()
        {
            _list.Add(1);
            _list.ClearChanges();

            _list.Add(2);

            //assert changes
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(1, changes.Adds);
            Assert.AreEqual(2, changes.First().Item.Current);
            //assert collection
            CollectionAssert.AreEqual(Enumerable.Range(1, 2), _list);
        }

        [Test]
        public void AddManyInSuccession()
        {
            Enumerable.Range(1, 10)
                      .ForEach(_list.Add);

            //assert changes
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(10, changes.Adds);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), changes.First().Range);
            //assert collection
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), _list);
        }

        [Test]
        public void AddRange()
        {
            _list.AddRange(Enumerable.Range(1, 10));

            //assert changes
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(10, changes.Adds);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), changes.First().Range);

            //assert collection
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), _list);
        }

        [Test]
        public void AddSecondRange()
        {
            _list.AddRange(Enumerable.Range(1, 10));
            _list.AddRange(Enumerable.Range(11, 10));
            var changes = _list.CaptureChanges();

            //assert changes
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(20, changes.Adds);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), changes.First().Range);
            CollectionAssert.AreEqual(Enumerable.Range(11, 10), changes.Skip(1).First().Range);

            //assert collection
            CollectionAssert.AreEqual(Enumerable.Range(1, 20), _list);
        }

        [Test]
        public void InsertRangeInCentre()
        {
            _list.AddRange(Enumerable.Range(1, 10));
            _list.InsertRange(Enumerable.Range(11, 10), 5);
            var changes = _list.CaptureChanges();

            //assert changes
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(20, changes.Adds);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), changes.First().Range);
            CollectionAssert.AreEqual(Enumerable.Range(11, 10), changes.Skip(1).First().Range);

            var shouldBe = Enumerable.Range(1, 5)
                                     .Union(Enumerable.Range(11, 10))
                                     .Union(Enumerable.Range(6, 5));
            //assert collection
            CollectionAssert.AreEqual(shouldBe, _list);
        }

        [Test]
        public void Remove()
        {
            _list.Add(1);
            _list.ClearChanges();

            _list.Remove(1);

            //assert changes
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(1, changes.Removes);
            Assert.AreEqual(1, changes.First().Item.Current);
            //assert collection
            Assert.AreEqual(0, _list.Count);
        }

        [Test]
        public void RemoveRange()
        {
            _list.AddRange(Enumerable.Range(1, 10));
            _list.ClearChanges();

            _list.RemoveRange(5, 3);

            //assert changes
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(3, changes.Removes);
            CollectionAssert.AreEqual(Enumerable.Range(6, 3), changes.First().Range);

            //assert collection
            var shouldBe = Enumerable.Range(1, 5)
                                     .Union(Enumerable.Range(9, 2));
            //assert collection
            CollectionAssert.AreEqual(shouldBe, _list);
        }

        [Test]
        public void RemoveSucession()
        {
            _list.AddRange(Enumerable.Range(1, 10));
            _list.ClearChanges();

            _list.ToArray().ForEach(i => _list.Remove(i));

            //assert changes (should batch)s
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(10, changes.Removes);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), changes.First().Range);

            //assert collection
            Assert.AreEqual(0, _list.Count);
        }

        [Test]
        public void RemoveSucessionReversed()
        {
            _list.AddRange(Enumerable.Range(1, 10));
            _list.ClearChanges();

            _list.OrderByDescending(i => i).ToArray().ForEach(i => _list.Remove(i));

            //assert changes (should batch)
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(10, changes.Removes);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), changes.First().Range);
            //assert collection
            Assert.AreEqual(0, _list.Count);
        }

        [Test]
        public void RemoveMany()
        {
            _list.AddRange(Enumerable.Range(1, 10));
            _list.ClearChanges();

            _list.RemoveMany(Enumerable.Range(1, 10));

            //assert changes (should batch)s
            var changes = _list.CaptureChanges();
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(10, changes.Removes);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10), changes.First().Range);

            //assert collection
            Assert.AreEqual(0, _list.Count);
        }


        [Test]
        public void RefreshAt()
        {
            _list.AddRange(Enumerable.Range(0, 9));
            _list.ClearChanges();
            _list.RefreshAt(1);

            //assert changes (should batch)
            var changes = _list.CaptureChanges();

            changes.Count.Should().Be(1);
            changes.Refreshes.Should().Be(1);
            changes.First().Reason.Should().Be(ListChangeReason.Refresh);
            changes.First().Item.Current.Should().Be(1);


            Assert.Throws<ArgumentException>(() => _list.RefreshAt(-1));
            Assert.Throws<ArgumentException>(() => _list.RefreshAt(1000));
        }

        [Test]
        public void Refresh()
        {
            _list.AddRange(Enumerable.Range(0, 9));
            _list.ClearChanges();
            _list.Refresh(1);

            //assert changes (should batch)
            var changes = _list.CaptureChanges();

            changes.Count.Should().Be(1);
            changes.Refreshes.Should().Be(1);
            changes.First().Reason.Should().Be(ListChangeReason.Refresh);
            changes.First().Item.Current.Should().Be(1);

            _list.Refresh(5).Should().Be(true);
            _list.Refresh(-1).Should().Be(false);
            _list.Refresh(1000).Should().Be(false);
        }



        [Test]
        public void ThrowWhenRemovingItemOutsideOfBoundaries()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _list.RemoveAt(0));
        }

        [Test]
        public void ThrowWhenRemovingRangeThatBeginsOutsideOfBoundaries()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _list.RemoveRange(0, 1));
        }

        [Test]
        public void ThrowWhenRemovingRangeThatFinishesOutsideOfBoundaries()
        {
            _list.Add(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => _list.RemoveRange(0, 2));
        }
    }
}
