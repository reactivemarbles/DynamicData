using System;
using System.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class CastFixture : IDisposable
{
    private readonly ChangeSetAggregator<decimal> _results;

    private readonly ISourceList<int> _source;

    public CastFixture()
    {
        _source = new SourceList<int>();
        _results = _source.Cast(i => (decimal)i).AsAggregator();
    }

    [Fact]
    public void CanCast()
    {
        _source.AddRange(Enumerable.Range(1, 10));
        _results.Data.Count.Should().Be(10);

        _source.Clear();
        _results.Data.Count.Should().Be(0);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
