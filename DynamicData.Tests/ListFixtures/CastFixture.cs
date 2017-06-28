using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class CastFixture
    {
        private ISourceList<int> _source;
        private ChangeSetAggregator<decimal> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceList<int>();
            _results = _source.Cast(i=>(decimal)i).AsAggregator();
        }

        [TearDown]
        public void Cleanup()
        {
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void CanCast()
        {
            _source.AddRange(Enumerable.Range(1,10));
            _results.Data.Count.Should().Be(10);

            _source.Clear();
            _results.Data.Count.Should().Be(0);
        }
    }
}
