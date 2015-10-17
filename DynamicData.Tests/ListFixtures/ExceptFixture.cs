using System.Linq;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class ExceptFixture
    {
        private ISourceList<int> _source1;
        private ISourceList<int> _source2;
        private ChangeSetAggregator<int> _results;

        [SetUp]
        public void Initialise()
        {
            _source1 = new SourceList<int>();
            _source2 = new SourceList<int>();
            _results = _source1.Connect().Except(_source2.Connect()).AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source1.Dispose();
            _source2.Dispose();
            _results.Dispose();
        }

        [Test]
        public void IncludedWhenItemIsInOneSource()
        {
            _source1.Add(1);
            Assert.AreEqual(1, _results.Data.Count);
        }

        [Test]
        public void ExcludedWhenItemIsInTwoSources()
        {
            _source1.Add(1);
            _source2.Add(1);
            Assert.AreEqual(0, _results.Data.Count);
        }

        [Test]
        public void AddedWhenNoLongerInSecond()
        {
            _source1.Add(1);
            _source2.Add(1);
            _source2.Remove(1);
            Assert.AreEqual(1, _results.Data.Count);
        }


        [Test]
        public void CombineRange()
        {
            _source1.AddRange(Enumerable.Range(1, 10));
            _source2.AddRange(Enumerable.Range(6, 10));
            Assert.AreEqual(5, _results.Data.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 5), _results.Data.Items);
        }

        [Test]
        public void ClearFirstClearsResult()
        {
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(1, 5));
            _source1.Clear();
            Assert.AreEqual(0, _results.Data.Count);
        }

        [Test]
        public void ClearSecondEnsuresFirstIsIncluded()
        {
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(1, 5));
            Assert.AreEqual(0, _results.Data.Count);
            _source2.Clear();
            Assert.AreEqual(5, _results.Data.Count);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 5), _results.Data.Items);
        }
    }
}