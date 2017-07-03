
using System.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.List
{
    
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

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void PoulatesFirstSource()
        {
            var inital = Enumerable.Range(1,100).ToArray();
            _source.AddRange(inital);

            _results.Data.Count.Should().Be(100);

            inital.ShouldAllBeEquivalentTo(_source.Items);
        }

        [Test]
        public void ClearsForNewSource()
        {
            var inital = Enumerable.Range(1, 100).ToArray();
            _source.AddRange(inital);

            _results.Data.Count.Should().Be(100);

            var newSource = new SourceList<int>();
            _switchable.OnNext(newSource);

            _results.Data.Count.Should().Be(0);

            newSource.AddRange(inital);
            _results.Data.Count.Should().Be(100);

            var nextUpdates = Enumerable.Range(100, 100).ToArray();
            newSource.AddRange(nextUpdates);
            _results.Data.Count.Should().Be(200);

        }

    }
}
