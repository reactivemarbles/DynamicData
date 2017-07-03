using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.List
{
    
    public class DynamicOrFixture: IDisposable
    {
        private ISourceList<int> _source1;
        private ISourceList<int> _source2;
        private ISourceList<int> _source3;
        private ISourceList<IObservable<IChangeSet<int>>> _source;

        private ChangeSetAggregator<int> _results;

        [SetUp]
        public void Initialise()
        {
            _source1 = new SourceList<int>();
            _source2 = new SourceList<int>();
            _source3 = new SourceList<int>();
            _source = new SourceList<IObservable<IChangeSet<int>>>();
            _results = _source.Or().AsAggregator();
        }

        public void Dispose()
        {
            _source1.Dispose();
            _source2.Dispose();
            _source3.Dispose();
            _source.Dispose();
            _results.Dispose();
        }

        [Test]
        public void IncludedWhenItemIsInOneSource()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.Add(1);

            _results.Data.Count.Should().Be(1);
            _results.Data.Items.First().Should().Be(1);
        }

        [Test]
        public void IncludedWhenItemIsInTwoSources()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.Add(1);
            _source2.Add(1);
            _results.Data.Count.Should().Be(1);
            _results.Data.Items.First().Should().Be(1);
        }

        [Test]
        public void RemovedWhenNoLongerInEither()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.Add(1);
            _source1.Remove(1);
            _results.Data.Count.Should().Be(0);
        }

        [Test]
        public void CombineRange()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(6, 5));
            _results.Data.Count.Should().Be(10);
            _results.Data.Items.ShouldAllBeEquivalentTo(Enumerable.Range(1, 10));
        }

        [Test]
        public void ClearOnlyClearsOneSource()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(6, 5));
            _source1.Clear();
            _results.Data.Count.Should().Be(5);
            _results.Data.Items.ShouldAllBeEquivalentTo(Enumerable.Range(6, 5));
        }

        [Test]
        public void AddAndRemoveLists()
        {
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(6, 5));
            _source3.AddRange(Enumerable.Range(100, 5));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source.Add(_source3.Connect());

            var result = Enumerable.Range(1, 5).Union(Enumerable.Range(6, 5)).Union(Enumerable.Range(100, 5));

            _results.Data.Count.Should().Be(15);
            _results.Data.Items.ShouldAllBeEquivalentTo(result);

            _source.RemoveAt(1);
            _results.Data.Count.Should().Be(10);

            result = Enumerable.Range(1, 5).Union(Enumerable.Range(100, 5));
            _results.Data.Items.ShouldAllBeEquivalentTo(result);
        }

        [Test]
        public void RemoveAllLists()
        {
            _source1.AddRange(Enumerable.Range(1, 5));

            _source3.AddRange(Enumerable.Range(100, 5));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source.Add(_source3.Connect());

            _source2.AddRange(Enumerable.Range(6, 5));
            _source.Clear();

            _results.Data.Count.Should().Be(0);
        }
    }
}
