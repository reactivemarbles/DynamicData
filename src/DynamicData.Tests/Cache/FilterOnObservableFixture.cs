using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Test Fixture for the FilterOnObservable extension method.
/// </summary>
public class FilterOnObservableFixture : IDisposable
{
    private const int MagicNumber = 37;

    private readonly ChangeSetAggregator<Person, string> _sourceResults;

    private readonly ISourceCache<Person, string> _source;

    public FilterOnObservableFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);
        _sourceResults = _source.Connect().AsAggregator();
    }

    [Fact]
    public void FactoryIsInvoked()
    {
        // having
        var invoked = false;
        var val = -1;
        IObservable<bool> factory(Person p)
        {
            invoked = true;
            val = p.Age;
            return Observable.Return(true);
        }
        using var sub = _source.Connect().FilterOnObservable(factory).Subscribe();

        // when
        AddPerson(MagicNumber);

        // then
        _sourceResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
        val.Should().Be(MagicNumber, "Was value added to cache");
        Assert.Throws<ArgumentNullException>(() => _source.Connect().FilterOnObservable((Func<Person, IObservable<bool>>)null!));
    }

    [Fact]
    public void FactoryWithKeyIsInvoked()
    {
        // having
        var invoked = false;
        var val = -1;
        IObservable<bool> factory(Person p, string name)
        {
            invoked = true;
            val = p.Age;
            return Observable.Return(true);
        }
        using var sub = _source.Connect().FilterOnObservable(factory).Subscribe();

        // when
        AddPerson(MagicNumber);

        // then
        _sourceResults.Data.Count.Should().Be(1);
        invoked.Should().BeTrue();
        val.Should().Be(MagicNumber, "Was value added to cache");
        Assert.Throws<ArgumentNullException>(() => _source.Connect().FilterOnObservable((Func<Person, string, IObservable<bool>>)null!));
        Assert.Throws<ArgumentNullException>(() => ObservableCacheEx.FilterOnObservable(null!, (Func<Person, string, IObservable<bool>>)null!));
    }

    [Fact]
    public void FilteredOutIfNoObservableValue()
    {
        // having
        using var filterStats = _source.Connect().FilterOnObservable(p => Observable.Never<bool>()).AsAggregator();

        // when
        AddPeople(MagicNumber);

        // then
        _sourceResults.Data.Count.Should().Be(MagicNumber);
        _sourceResults.Messages[0].Adds.Should().Be(MagicNumber);
        _sourceResults.Messages.Count.Should().Be(1, "Should have all been added at once");
        filterStats.Messages.Count.Should().Be(0, "All items should be filtered out");
    }

    [Fact]
    public void ObservableFilterUsedToDetermineInclusion()
    {
        // having
        Predicate<Person> predicate = p => p.Age % 2 == 0;
        Func<Person, IObservable<bool>> filterFactory = p => Observable.Return(predicate(p));
        var passCount = 0;
        var failCount = 0;
        using var filterStats = _source.Connect().FilterOnObservable(filterFactory).AsAggregator();

        // when
        AddPeople(MagicNumber).ForEach(p => _ = predicate(p) ? passCount++ : failCount++);

        // then
        _sourceResults.Data.Count.Should().Be(passCount + failCount);
        filterStats.Data.Count.Should().Be(passCount);
    }

    [Fact]
    public void ObservableFilterTriggersAddAndRemove()
    {
        // having
        ISubject<bool> filterSubject = new Subject<bool>();

        using var filterStats = _source.Connect().FilterOnObservable(_ => filterSubject).AsAggregator();

        AddPeople(MagicNumber);

        // when
        filterSubject.OnNext(true);
        filterSubject.OnNext(false);

        // then
        _sourceResults.Data.Count.Should().Be(MagicNumber);
        _sourceResults.Messages.Count.Should().Be(1, "Should have all been added at once");
        filterStats.Data.Count.Should().Be(0);
        filterStats.Messages.Count.Should().Be(MagicNumber*2, "Each should be added and removed");
        filterStats.Summary.Overall.Adds.Should().Be(MagicNumber);
        filterStats.Summary.Overall.Removes.Should().Be(MagicNumber);
    }

    [Fact]
    public void ObservableFilterDuplicateValuesHaveNoEffect()
    {
        // having
        ISubject<bool> filterSubject = new Subject<bool>();

        using var filterStats = _source.Connect().FilterOnObservable(_ => filterSubject).AsAggregator();

        AddPeople(MagicNumber);

        // when
        filterSubject.OnNext(false);
        filterSubject.OnNext(false);
        filterSubject.OnNext(false);
        filterSubject.OnNext(true);
        filterSubject.OnNext(true);
        filterSubject.OnNext(true);

        // then
        _sourceResults.Data.Count.Should().Be(MagicNumber);
        _sourceResults.Messages.Count.Should().Be(1, "Should have all been added at once");
        filterStats.Data.Count.Should().Be(MagicNumber);
        filterStats.Messages.Count.Should().Be(MagicNumber, "Each should be added individually");
        filterStats.Summary.Overall.Adds.Should().Be(MagicNumber);
    }

    [Fact]
    public void ObservableFilterChangesCanBeBuffered()
    {
        // having
        TestScheduler? scheduler = new TestScheduler();
        ISubject<bool> filterSubject = new Subject<bool>();

        using var filterStats = _source.Connect().FilterOnObservable(_ => filterSubject, TimeSpan.FromSeconds(1), scheduler).AsAggregator();

        AddPeople(MagicNumber);

        // when
        filterSubject.OnNext(true);
        scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        // then
        _sourceResults.Data.Count.Should().Be(MagicNumber);
        _sourceResults.Messages.Count.Should().Be(1, "Should have all been added at once");
        filterStats.Data.Count.Should().Be(MagicNumber);
        filterStats.Messages.Count.Should().Be(1, "Should have all been added at once");
        filterStats.Summary.Overall.Adds.Should().Be(MagicNumber);
    }

    private static Person NewPerson(int n) => new("Name" + n, n);

    private IEnumerable<Person> AddPeople(int count)
    {
        var people = Enumerable.Range(0, count).Select(NewPerson).ToArray();
        _source.AddOrUpdate(people);
        return people;
    }

    private Person AddPerson(int n)
    {
        var p = NewPerson(n);
        _source.AddOrUpdate(p);
        return p;
    }

    public void Dispose()
    {
        _source.Dispose();
        _sourceResults.Dispose();
    }
}
