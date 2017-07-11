using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Binding;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.List
{
    public class SortPrimitiveFixture : IDisposable
    {
        private readonly ISourceList<int> _source;
        private readonly ChangeSetAggregator<int> _results;

        private readonly IComparer<int> _comparer = SortExpressionComparer<int>.Ascending(i => i);

        public SortPrimitiveFixture()
        {
            _source = new SourceList<int>();
            _results = _source.Connect().Sort(_comparer).AsAggregator();
        }

        public void Dispose()
        {
            _results.Dispose();
            _source.Dispose();
        }

        [Fact]
        public void RemoveRandomSorts()
        {
            //seems an odd test but belive me it catches and exception when sorting on primitives
            var items = Enumerable.Range(1,100).OrderBy(_=>Guid.NewGuid()).ToArray();
            _source.AddRange(items);

            _results.Data.Count.Should().Be(100);

            var expectedResult = items.OrderBy(p => p, _comparer);
            var actualResult = _results.Data.Items;

            actualResult.ShouldAllBeEquivalentTo(expectedResult);

            for (int i = 0; i < 50; i++)
            {
                _source.Remove(i);
            }

  
        }


    }
}