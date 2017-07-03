using System.Linq;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.List
{
    
    internal class ReverseFixture
    {
        private ISourceList<int> _source;
        private ChangeSetAggregator<int> _results;

        [SetUp]
        public void SetUp()
        {
            _source = new SourceList<int>();
            _results = _source.Connect().Reverse().AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Test]
        public void AddInSucession()
        {
            _source.Add(1);
            _source.Add(2);
            _source.Add(3);
            _source.Add(4);
            _source.Add(5);

            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {5, 4, 3, 2, 1});
        }

        [Test]
        public void AddRange()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {5, 4, 3, 2, 1});
        }

        [Test]
        public void Removes()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.Remove(1);
            _source.Remove(4);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {5, 3, 2});
        }

        [Test]
        public void RemoveRange()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.RemoveRange(1, 3);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {5, 1});
        }

        [Test]
        public void RemoveRangeThenInsert()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.RemoveRange(1, 3);
            _source.Insert(1, 3);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {5, 3, 1});
        }

        [Test]
        public void Replace()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.ReplaceAt(2, 100);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {5, 4, 100, 2, 1});
        }

        [Test]
        public void Clear()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.Clear();
            _results.Data.Count.Should().Be(0);
        }

        [Test]
        public void Move()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.Move(4, 1);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {4, 3, 2, 5, 1});
        }

        [Test]
        public void Move2()
        {
            _source.AddRange(Enumerable.Range(1, 5));
            _source.Move(1, 4);
            _results.Data.Items.ShouldAllBeEquivalentTo(new[] {2, 5, 4, 3, 1});
        }
    }
}
