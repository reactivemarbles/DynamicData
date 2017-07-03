using System.Linq;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.Cache
{
    
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

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }
        
        [Test]
        public void PoulatesFirstSource()
        {
            var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
            _source.AddOrUpdate(inital);

            _results.Data.Count.Should().Be(100);
        }

        [Test]
        public void ClearsForNewSource()
        {
            var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
            _source.AddOrUpdate(inital);

            _results.Data.Count.Should().Be(100);

            var newSource = new SourceCache<Person, string>(p => p.Name);
            _switchable.OnNext(newSource);

            _results.Data.Count.Should().Be(0);
            
            newSource.AddOrUpdate(inital);
            _results.Data.Count.Should().Be(100);

            var nextUpdates = Enumerable.Range(101, 100).Select(i => new Person("Person" + i, i)).ToArray();
            newSource.AddOrUpdate(nextUpdates);
            _results.Data.Count.Should().Be(200);

        }

    }
}
