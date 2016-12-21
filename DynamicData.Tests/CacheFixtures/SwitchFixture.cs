using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class SwitchFixture
    {
        private ISubject<ISourceCache<Person, string>> _switchable;
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<Person, string> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _switchable = new BehaviorSubject<ISourceCache<Person, string>>(_source);
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
            var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
            _source.AddOrUpdate(inital);

            Assert.AreEqual(100, _results.Data.Count);
        }

        [Test]
        public void ClearsForNewSource()
        {
            var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
            _source.AddOrUpdate(inital);

            Assert.AreEqual(100, _results.Data.Count);

            var newSource = new SourceCache<Person, string>(p => p.Name);
            _switchable.OnNext(newSource);

            Assert.AreEqual(0, _results.Data.Count);
            
            newSource.AddOrUpdate(inital);
            Assert.AreEqual(100, _results.Data.Count);

            var nextUpdates = Enumerable.Range(101, 100).Select(i => new Person("Person" + i, i)).ToArray();
            newSource.AddOrUpdate(nextUpdates);
            Assert.AreEqual(200, _results.Data.Count);

        }

    }
}
