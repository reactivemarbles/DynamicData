using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List
{
    
    public class DynamicExceptFixture: IDisposable
    {
        private readonly ISourceList<int> _source1;
        private readonly ISourceList<int> _source2;
        private readonly ISourceList<int> _source3;
        private readonly ISourceList<IObservable<IChangeSet<int>>> _source;

        private readonly ChangeSetAggregator<int> _results;

        public  DynamicExceptFixture()
        {
            _source1 = new SourceList<int>();
            _source2 = new SourceList<int>();
            _source3 = new SourceList<int>();
            _source = new SourceList<IObservable<IChangeSet<int>>>();
            _results = _source.Except().AsAggregator();
        }

        public void Dispose()
        {
            _source1.Dispose();
            _source2.Dispose();
            _source3.Dispose();
            _source.Dispose();
            _results.Dispose();
        }

        [Fact]
        public void IncludedWhenItemIsInOneSource()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.Add(1);
            _results.Data.Count.Should().Be(1);
        }

        [Fact]
        public void NothingFromOther()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source2.Add(1);
            _results.Data.Count.Should().Be(0);
        }

        [Fact]
        public void ExcludedWhenItemIsInTwoSources()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.Add(1);
            _source2.Add(1);
            _results.Data.Count.Should().Be(0);
        }

        [Fact]
        public void AddedWhenNoLongerInSecond()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.Add(1);
            _source2.Add(1);
            _source2.Remove(1);
            _results.Data.Count.Should().Be(1);
        }

        [Fact]
        public void CombineRange()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.AddRange(Enumerable.Range(1, 10));
            _source2.AddRange(Enumerable.Range(6, 10));
            _results.Data.Count.Should().Be(5);
            _results.Data.Items.ShouldAllBeEquivalentTo(Enumerable.Range(1, 5));
        }

        [Fact]
        public void ClearFirstClearsResult()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(1, 5));
            _source1.Clear();
            _results.Data.Count.Should().Be(0);
        }

        [Fact]
        public void ClearSecondEnsuresFirstIsIncluded()
        {
            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());

            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(1, 5));
            _results.Data.Count.Should().Be(0);
            _source2.Clear();
            _results.Data.Count.Should().Be(5);
            _results.Data.Items.ShouldAllBeEquivalentTo(Enumerable.Range(1, 5));
        }

        [Fact]
        public void AddAndRemoveLists()
        {
            _source1.AddRange(Enumerable.Range(1, 5));
            _source2.AddRange(Enumerable.Range(6, 5));
            _source3.AddRange(Enumerable.Range(100, 5));

            _source.Add(_source1.Connect());
            _source.Add(_source2.Connect());
            _source.Add(_source3.Connect());

            var result = Enumerable.Range(1, 5);
            _results.Data.Count.Should().Be(5);
            _results.Data.Items.ShouldAllBeEquivalentTo(result);

            _source2.Edit(innerList =>
            {
                innerList.Clear();
                innerList.AddRange(Enumerable.Range(3, 5));
            });

            result = Enumerable.Range(1, 2);
            _results.Data.Count.Should().Be(2);
            _results.Data.Items.ShouldAllBeEquivalentTo(result);

            _source.RemoveAt(1);
            result = Enumerable.Range(1, 5);
            _results.Data.Count.Should().Be(5);
            _results.Data.Items.ShouldAllBeEquivalentTo(result);

            _source.Add(_source2.Connect());
            result = Enumerable.Range(1, 2);
            _results.Data.Count.Should().Be(2);
            _results.Data.Items.ShouldAllBeEquivalentTo(result);

            //remove root except
            _source.RemoveAt(0);
            result = Enumerable.Range(100, 5);
            _results.Data.Count.Should().Be(5);
            _results.Data.Items.ShouldAllBeEquivalentTo(result);
        }
    }
}
