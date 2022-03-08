using System;
using System.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class SwitchFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<int> _source;

    private readonly ISubject<ISourceList<int>> _switchable;

    public SwitchFixture()
    {
        _source = new SourceList<int>();
        _switchable = new BehaviorSubject<ISourceList<int>>(_source);
        _results = _switchable.Switch().AsAggregator();
    }

    [Fact]
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

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void PoulatesFirstSource()
    {
        var inital = Enumerable.Range(1, 100).ToArray();
        _source.AddRange(inital);

        _results.Data.Count.Should().Be(100);

        inital.Should().BeEquivalentTo(_source.Items);
    }
}
