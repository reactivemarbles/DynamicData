using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class AsObservableChangeSetFixture
{
    private const int MaxItems = 1097;

    [Fact]
    public void ItemsFromEnumerableAreAddedToChangeSet()
    {
        // having
        var enumerable = CreatePeople(0, MaxItems, "Name");
        var enumObservable = Observable.Return(enumerable);

        // when
        var observableChangeSet = enumObservable.AsObservableChangeSet(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(MaxItems);
        results.Messages.Count.Should().Be(1);
    }

    [Fact]
    public void ItemsRemovedFromEnumerableAreRemovedFromChangeSet()
    {
        // having
        var enumerable = CreatePeople(0, MaxItems, "Name");
        var enumObservable = new[] {enumerable, Enumerable.Empty<Person>()}.ToObservable();

        // when
        var observableChangeSet = enumObservable.AsObservableChangeSet(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(0);
        results.Messages.Count.Should().Be(2);
        results.Messages[0].Adds.Should().Be(MaxItems);
        results.Messages[1].Removes.Should().Be(MaxItems);
        results.Messages[1].Updates.Should().Be(0);
    }

    [Fact]
    public void ItemsUpdatedAreUpdatedInChangeSet()
    {
        // having
        var enumerable1 = CreatePeople(0, MaxItems * 2, "Name");
        var enumerable2 = CreatePeople(MaxItems, MaxItems, "Update");
        var enumObservable = new[] { enumerable1, enumerable2 }.ToObservable();

        // when
        var observableChangeSet = enumObservable.AsObservableChangeSet(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(MaxItems);
        results.Messages.Count.Should().Be(2);
        results.Messages[0].Adds.Should().Be(MaxItems * 2);
        results.Messages[1].Updates.Should().Be(MaxItems);
        results.Messages[1].Removes.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResultCompletesIfAndOnlyIfSourceCompletes(bool completeSource)
    {
        // having
        var enumerable = CreatePeople(0, MaxItems, "Name");
        var enumObservable = Observable.Return(enumerable);
        if (!completeSource)
        {
            enumObservable = enumObservable.Concat(Observable.Never<IEnumerable<Person>>());
        }
        bool completed = false;

        // when
        using var results = enumObservable.Subscribe(_ => { }, () => completed = true);

        // then
        completed.Should().Be(completeSource);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResultFailsIfAndOnlyIfSourceFails (bool failSource)
    {
        // having
        var enumerable = CreatePeople(0, MaxItems, "Name");
        var enumObservable = Observable.Return(enumerable);
        var testException = new Exception("Test");
        if (failSource)
        {
            enumObservable = enumObservable.Concat(Observable.Throw<IEnumerable<Person>>(testException));
        }
        var receivedError = default(Exception);

        // when
        using var results = enumObservable.Subscribe(_ => { }, err => receivedError = err);

        // then
        receivedError.Should().Be(failSource ? testException : default);
    }

    [Trait("Performance", "Manual run only")]
    [Theory]
    [InlineData(7, 3, 5)]
    [InlineData(2521, 1187, MaxItems)]
    [InlineData(2521, 0, MaxItems)]
    [InlineData(2521, 2521, MaxItems)]
    [InlineData(5081, 2683, MaxItems)]
    [InlineData(5081, 0, MaxItems)]
    [InlineData(5081, 5081, MaxItems)]
    public void Perf(int blockSize, int overlapSize, int maxItems)
    {
        Debug.Assert(overlapSize <= blockSize);

        // having
        var enumerables = Enumerable.Range(1, maxItems - 1)
            .Select(n => new { Start = (n * blockSize) - (n * overlapSize), Overlap = n * blockSize, })
            .Select(info => CreatePeople(info.Start, overlapSize, "Overlap")
                                                            .Concat(CreatePeople(info.Start + overlapSize, blockSize - overlapSize, "Name")))
            .Prepend(CreatePeople(0, blockSize, "Name"));
        var enumObservable = enumerables.ToObservable();

        // when
        var observableChangeSet = enumObservable.AsObservableChangeSet(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(blockSize);
        results.Messages.Count.Should().Be(maxItems);
        results.Summary.Overall.Adds.Should().Be((blockSize * maxItems) - (overlapSize * (maxItems - 1)));
        results.Summary.Overall.Removes.Should().Be((blockSize - overlapSize) * (maxItems - 1));
        results.Summary.Overall.Updates.Should().Be(overlapSize * (maxItems - 1));
    }

    private static Person CreatePerson(int id, string name) => new(id, name);

    private static IEnumerable<Person> CreatePeople(int baseId, int count, string baseName) =>
        Enumerable.Range(baseId, count).Select(i => CreatePerson(i, baseName + i));

    private class Person
    {
        public Person(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }
    }
}
