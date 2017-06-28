using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class ExceptFixture : ExceptFixtureBase
    {
        protected override IObservable<IChangeSet<int>> CreateObservable()
        {
            return _source1.Connect().Except(_source2.Connect());
        }
    }

    [TestFixture]
    public class ExceptCollectionFixture : ExceptFixtureBase
    {
        protected override IObservable<IChangeSet<int>> CreateObservable()
        {
            var l = new List<IObservable<IChangeSet<int>>> { _source1.Connect(), _source2.Connect() };
            return l.Except();
        }
    }

    [TestFixture]
    public abstract class ExceptFixtureBase
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
        }

        [Test]
        public void NothingFromOther()
        {
            _source2.Add(1);
            _results.Data.Count.Should().Be(0);
        }

        [Test]
        public void ExcludedWhenItemIsInTwoSources()
        {
            _source1.Add(1);
            _source2.Add(1);
            _results.Data.Count.Should().Be(0);
        }

        [Test]
        public void AddedWhenNoLongerInSecond()
        {
            _source1.Add(1);
            _source2.Add(1);
            _source2.Remove(1);
            _results.Data.Count.Should().Be(1);
        }

        [Test]
        public void CombineRange()
        {
            _source1.AddRange(Enumerable.Range(1, 10));
            _source2.AddRange(Enumerable.Range(6, 10));
            _results.Data.Count.Should().Be(5);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 5), _results.Data.Items);
        }

        [Test]
        public void ClearFirstClearsResult()
        {
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(1, 5));
            _source1.Clear();
            _results.Data.Count.Should().Be(0);
        }

        [Test]
        public void ClearSecondEnsuresFirstIsIncluded()
        {
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(1, 5));
            _results.Data.Count.Should().Be(0);
            _source2.Clear();
            _results.Data.Count.Should().Be(5);
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 5), _results.Data.Items);
        }
    }
}
