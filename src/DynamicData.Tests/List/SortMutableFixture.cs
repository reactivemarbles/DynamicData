using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;

using DynamicData.Binding;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class SortMutableFixture : IDisposable
{
    private readonly ISubject<IComparer<Person>> _changeComparer;

    private readonly IComparer<Person> _comparer = SortExpressionComparer<Person>.Ascending(p => p.Age).ThenByAscending(p => p.Name);

    private readonly RandomPersonGenerator _generator = new();

    private readonly ISubject<Unit> _resort;

    private readonly ChangeSetAggregator<Person> _results;

    private readonly ISourceList<Person> _source;

    public SortMutableFixture()
    {
        _source = new SourceList<Person>();
        _changeComparer = new BehaviorSubject<IComparer<Person>>(_comparer);
        _resort = new Subject<Unit>();

        _results = _source.Connect().Sort(_changeComparer, resetThreshold: 25, resort: _resort).AsAggregator();
    }

    [Fact]
    public void ChangeComparer()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var newComparer = SortExpressionComparer<Person>.Ascending(p => p.Name).ThenByAscending(p => p.Age);

        _changeComparer.OnNext(newComparer);

        _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, newComparer);
        var actualResult = _results.Data.Items;

        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    public void Dispose()
    {
        _results.Dispose();
        _source.Dispose();
    }

    [Fact]
    public void Insert()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var shouldbeLast = new Person("__A", 10000);
        _source.Add(shouldbeLast);

        _results.Data.Count.Should().Be(101);

        _results.Data.Items[
        ^1].Should().Be(shouldbeLast);
    }

    [Fact]
    public void Remove()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        var toRemove = people.ElementAt(20);
        people.RemoveAt(20);
        _source.RemoveAt(20);

        _results.Data.Count.Should().Be(99, "Should be 99 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");
        _results.Messages[1].First().Item.Current.Should().Be(toRemove, "Incorrect item removed");

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void RemoveManyOdds()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        var odd = people.Select((p, idx) => new { p, idx }).Where(x => x.idx % 2 == 1).Select(x => x.p).ToArray();

        _source.RemoveMany(odd);

        _results.Data.Count.Should().Be(50, "Should be 99 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

        var expectedResult = people.Except(odd).OrderByDescending(p => p, _comparer).ToArray();
        var actualResult = _results.Data.Items;
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void RemoveManyOrdered()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _source.RemoveMany(people.OrderBy(p => p, _comparer).Skip(10).Take(90));

        _results.Data.Count.Should().Be(10, "Should be 99 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

        var expectedResult = people.OrderBy(p => p, _comparer).Take(10);
        var actualResult = _results.Data.Items;
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void RemoveManyReverseOrdered()
    {
        var people = _generator.Take(100).ToList();
        _source.AddRange(people);

        _source.RemoveMany(people.OrderByDescending(p => p, _comparer).Skip(10).Take(90));

        _results.Data.Count.Should().Be(10, "Should be 99 people in the cache");
        _results.Messages.Count.Should().Be(2, "Should be 2 update messages");

        var expectedResult = people.OrderByDescending(p => p, _comparer).Take(10);
        var actualResult = _results.Data.Items;
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void Replace()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        var shouldbeLast = new Person("__A", 999);
        _source.ReplaceAt(10, shouldbeLast);

        _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

        _results.Data.Items[
        ^1].Should().Be(shouldbeLast);
    }

    [Fact]
    public void Resort()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        people.OrderBy(_ => Guid.NewGuid()).ForEach((person, index) => { person.Age = index; });

        _resort.OnNext(Unit.Default);

        _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;

        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void ResortOnInlineChanges()
    {
        var people = _generator.Take(10).ToList();
        _source.AddRange(people);

        people[0].Age = -1;
        people[1].Age = -10;
        people[2].Age = -12;
        people[3].Age = -5;
        people[4].Age = -7;
        people[5].Age = -6;

        var comparer = SortExpressionComparer<Person>.Descending(p => p.Age).ThenByAscending(p => p.Name);

        _changeComparer.OnNext(comparer);

        var expectedResult = people.OrderBy(p => p, comparer).ToArray();
        var actualResult = _results.Data.Items.ToArray();

        //actualResult.(expectedResult);
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void SortInitialBatch()
    {
        var people = _generator.Take(100).ToArray();
        _source.AddRange(people);

        _results.Data.Count.Should().Be(100);

        var expectedResult = people.OrderBy(p => p, _comparer);
        var actualResult = _results.Data.Items;

        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public void UpdateMoreThanThreshold()
    {
        var allPeople = _generator.Take(1100).ToList();
        var people = allPeople.Take(100).ToArray();
        _source.AddRange(people);

        _results.Data.Count.Should().Be(100, "Should be 100 people in the cache");

        var morePeople = allPeople.Skip(100).ToArray();
        _source.AddRange(morePeople);

        _results.Data.Count.Should().Be(1100, "Should be 1100 people in the cache");
        var expectedResult = people.Union(morePeople).OrderBy(p => p, _comparer).ToArray();
        var actualResult = _results.Data.Items;

        actualResult.Should().BeEquivalentTo(expectedResult);

        _results.Messages.Count.Should().Be(2, "Should be 2 messages");

        var lastMessage = _results.Messages.Last();
        lastMessage.First().Range.Count.Should().Be(100, "Should be 100 in the range");
        lastMessage.First().Reason.Should().Be(ListChangeReason.Clear);

        lastMessage.Last().Range.Count.Should().Be(1100, "Should be 1100 in the range");
        lastMessage.Last().Reason.Should().Be(ListChangeReason.AddRange);
    }
}
