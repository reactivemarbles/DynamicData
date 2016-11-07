using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using DynamicData.Tests.Domain;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class SwitchFixture
    {
        private readonly RandomPersonGenerator _generator = new RandomPersonGenerator();

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
            var inital = _generator.Take(100).ToArray();
            _source.AddOrUpdate(inital);

            Assert.AreEqual(100, _results.Data.Count);
        }

        [Test]
        public void ClearsForNewSource()
        {
            var inital = _generator.Take(100).ToArray();
            _source.AddOrUpdate(inital);

            Assert.AreEqual(100, _results.Data.Count);

            var newSource = new SourceCache<Person, string>(p => p.Name);
            _switchable.OnNext(newSource);

            Assert.AreEqual(0, _results.Data.Count);
            
            newSource.AddOrUpdate(inital);
            Assert.AreEqual(100, _results.Data.Count);

            newSource.AddOrUpdate(_generator.Take(200).Skip(100).ToArray());
            Assert.AreEqual(200, _results.Data.Count);
        }

    }
}
