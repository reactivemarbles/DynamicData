
using System.Linq;
using System.Reactive.Subjects;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class SwitchFixture
    {
        private ISubject<ISourceList<int>> _switchable;
        private ISourceList<int> _source;
        private ChangeSetAggregator<int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<int>();
            _switchable = new BehaviorSubject<ISourceList<int>>(_source);
            _results = _switchable.Switch().AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void PoulatesFirstSource()
        {
            var inital = Enumerable.Range(1,100).ToArray();
            _source.AddRange(inital);

            Assert.AreEqual(100, _results.Data.Count);

            CollectionAssert.AreEqual(_source.Items, inital);
        }

        [Test]
        public void ClearsForNewSource()
        {
            var inital = Enumerable.Range(1, 100).ToArray();
            _source.AddRange(inital);

            Assert.AreEqual(100, _results.Data.Count);

            var newSource = new SourceList<int>();
            _switchable.OnNext(newSource);

            Assert.AreEqual(0, _results.Data.Count);

            newSource.AddRange(inital);
            Assert.AreEqual(100, _results.Data.Count);

            var nextUpdates = Enumerable.Range(100, 100).ToArray();
            newSource.AddRange(nextUpdates);
            Assert.AreEqual(200, _results.Data.Count);

        }

    }
}
