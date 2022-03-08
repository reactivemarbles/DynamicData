using System;
using System.Linq;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class SwitchFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<Person, string> _source;

    private readonly ISubject<ISourceCache<Person, string>> _switchable;

    public SwitchFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _switchable = new BehaviorSubject<ISourceCache<Person, string>>(_source);
        _results = _switchable.Switch().AsAggregator();
    }

    [Fact]
    public void ClearsForNewSource()
    {
        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        _source.AddOrUpdate(inital);

        _results.Data.Count.Should().Be(100);

        var newSource = new SourceCache<Person, string>(p => p.Name);
        _switchable.OnNext(newSource);

        _results.Data.Count.Should().Be(0);

        newSource.AddOrUpdate(inital);
        _results.Data.Count.Should().Be(100);

        var nextUpdates = Enumerable.Range(101, 100).Select(i => new Person("Person" + i, i)).ToArray();
        newSource.AddOrUpdate(nextUpdates);
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
        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        _source.AddOrUpdate(inital);

        _results.Data.Count.Should().Be(100);
    }
}
