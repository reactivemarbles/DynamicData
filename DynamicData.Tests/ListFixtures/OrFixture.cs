using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class OrFixture : OrFixtureBase
    {
        protected override IObservable<IChangeSet<int>> CreateObservable()
        {
            return _source1.Connect().Or(_source2.Connect());
        }
    }

    [TestFixture]
    public class OrCollectionFixture : OrFixtureBase
    {
        protected override IObservable<IChangeSet<int>> CreateObservable()
        {
            var list = new List<IObservable<IChangeSet<int>>> { _source1.Connect(), _source2.Connect() };
            return list.Or();
        }
    }

    [TestFixture]
    public abstract class OrFixtureBase
    {
        protected ISourceList<int> _source1;
        protected ISourceList<int> _source2;
        private ChangeSetAggregator<int> _results;

        [SetUp]
        public void Initialise()
        {
            _source1 = new SourceList<int>();
            _source2 = new SourceList<int>();
            _results = CreateObservable().AsAggregator();
        }

        protected abstract IObservable<IChangeSet<int>> CreateObservable();

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

            _results.Data.Count.Should().Be(1);
            _results.Data.Items.First().Should().Be(1);
        }

        [Test]
        public void IncludedWhenItemIsInTwoSources()
        {
            _source1.Add(1);
            _source2.Add(1);
            _results.Data.Count.Should().Be(1);
            _results.Data.Items.First().Should().Be(1);
        }

        [Test]
        public void RemovedWhenNoLongerInEither()
        {
            _source1.Add(1);
            _source1.Remove(1);
            _results.Data.Count.Should().Be(0);
        }

        [Test]
        public void CombineRange()
        {
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(6, 5));
            _results.Data.Count.Should().Be(10);
            _results.Data.Items.ShouldAllBeEquivalentTo(Enumerable.Range(1, 10));
        }

        [Test]
        public void ClearOnlyClearsOneSource()
        {
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(6, 5));
            _source1.Clear();
            _results.Data.Count.Should().Be(5);
            _results.Data.Items.ShouldAllBeEquivalentTo(Enumerable.Range(6, 5));
        }
    }
}
