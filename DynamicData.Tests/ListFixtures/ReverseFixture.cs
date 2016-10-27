using System.Linq;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    internal class ReverseFixture
    {
        private ISourceList<int> _source;
        private ChangeSetAggregator<int> _results;

        [SetUp]
        public void SetUp()
        {
            _source = new SourceList<int>();
            _results = _source.Connect().Reverse().AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Test]
        public void AddInSucession()
        {
            _source.Add(1);
            _source.Add(2);
            _source.Add(3);
            _source.Add(4);
            _source.Add(5);

            CollectionAssert.AreEquivalent(new[] { 5, 4, 3, 2, 1 }, _results.Data.Items);
        }

        [Test]
        public void AddRange()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            CollectionAssert.AreEquivalent(new[] { 5, 4, 3, 2, 1 }, _results.Data.Items);
        }

        [Test]
        public void Removes()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.Remove(1);
            _source.Remove(4);
            CollectionAssert.AreEquivalent(new[] { 5, 3, 2 }, _results.Data.Items);
        }

        [Test]
        public void RemoveRange()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.RemoveRange(1, 3);
            CollectionAssert.AreEquivalent(new[] { 5, 1 }, _results.Data.Items);
        }

        [Test]
        public void RemoveRangeThenInsert()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.RemoveRange(1, 3);
            _source.Insert(1, 3);
            CollectionAssert.AreEquivalent(new[] { 5, 3, 1 }, _results.Data.Items);
        }

        [Test]
        public void Replace()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.ReplaceAt(2, 100);
            CollectionAssert.AreEquivalent(new[] { 5, 4, 100, 2, 1 }, _results.Data.Items);
        }

        [Test]
        public void Clear()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.Clear();
            Assert.AreEqual(0, _results.Data.Count);
        }

        [Test]
        public void Move()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.Move(4, 1);
            CollectionAssert.AreEquivalent(new[] { 4, 3, 2, 5, 1 }, _results.Data.Items);
        }

        [Test]
        public void Move2()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.Move(1, 4);
            CollectionAssert.AreEquivalent(new[] { 2, 5, 4, 3, 1 }, _results.Data.Items);
        }
    }
}
