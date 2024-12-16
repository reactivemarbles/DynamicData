using System;
using System.Linq;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class SwitchFixture
{
    [Fact]
    public void ClearsForNewSource()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();


        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        results.Data.Count.Should().Be(100);

        var newSource = new SourceCache<Person, string>(p => p.Name);
        switchable.OnNext(newSource);

        results.Data.Count.Should().Be(0);

        newSource.AddOrUpdate(inital);
        results.Data.Count.Should().Be(100);

        var nextUpdates = Enumerable.Range(101, 100).Select(i => new Person("Person" + i, i)).ToArray();
        newSource.AddOrUpdate(nextUpdates);
        results.Data.Count.Should().Be(200);
    }

    [Fact]
    public void PoulatesFirstSource()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();


        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        results.Data.Count.Should().Be(100);
    }

    [Fact]
    public void PropagatesOuterErrors()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();


        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        var error = new Exception("Test");
        switchable.OnError(error);

        results.Error.Should().Be(error);
    }

    [Fact]
    public void PropagatesInnerErrors()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<Person, string>>>(source.Connect());
        var results = switchable.Switch().AsAggregator();


        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        using var source2 = new BehaviorSubject<IChangeSet<Person, string>>(ChangeSet<Person, string>.Empty);

        switchable.OnNext(source2);

        var error = new Exception("Test");
        source2.OnError(error);

        results.Error.Should().Be(error);
    }
}
