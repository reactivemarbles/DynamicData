using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

using FluentAssertions;

using Xunit;

using DynamicData.Tests.Utilities;

using Person = DynamicData.Tests.Domain.Person;

namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    [Fact]
    public void MergeChangeSets_ChildRefreshesItem_FiltersMergedItem()
    {
        // Arrange
        var threshold = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        using var first = CreatePeople(threshold);
        using var second = CreatePeople(threshold);
        var changed = _randomizer.ArrayElement(second.Items.ToArray());
        changed.Age = threshold;

        using var subscription = new[] { first.Connect().AutoRefresh(static person => person.Age), second.Connect().AutoRefresh(static person => person.Age) }
            .MergeChangeSets()
            .Filter(person => person.Age > threshold)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        changed.Age = threshold + 1;

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(first.Items.Concat(second.Items).Where(person => person.Age > threshold));
        results.RecordedItems.Should().Contain(changed);
    }

    [Fact]
    public void MergeManyChangeSets_ChildRefreshesItem_FiltersMergedItem()
    {
        // Arrange
        var threshold = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        using var first = CreatePeople(threshold);
        using var second = CreatePeople(threshold);
        using var parents = new SourceList<ISourceList<Person>>();
        parents.AddRange(new[] { first, second });
        var changed = _randomizer.ArrayElement(first.Items.ToArray());
        changed.Age = threshold;

        using var subscription = parents.Connect()
            .MergeManyChangeSets(static people => people.Connect().AutoRefresh(static person => person.Age))
            .Filter(person => person.Age > threshold)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        changed.Age = threshold + 1;

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(first.Items.Concat(second.Items).Where(person => person.Age > threshold));
        results.RecordedItems.Should().Contain(changed);
    }

    [Fact]
    public void CacheMergeManyChangeSets_ChildListRefreshesItem_FiltersMergedItem()
    {
        // Arrange
        var threshold = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        using var first = CreatePeople(threshold);
        using var second = CreatePeople(threshold);
        var ids = GenerateDistinctValues(2);
        using var parents = new SourceCache<(int Id, ISourceList<Person> People), int>(static parent => parent.Id);
        parents.AddOrUpdate(new[] { (ids[0], (ISourceList<Person>)first), (ids[1], second) });
        var changed = _randomizer.ArrayElement(second.Items.ToArray());
        changed.Age = threshold;

        using var subscription = parents.Connect()
            .MergeManyChangeSets(static parent => parent.People.Connect().AutoRefresh(static person => person.Age))
            .Filter(person => person.Age > threshold)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        changed.Age = threshold + 1;

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(first.Items.Concat(second.Items).Where(person => person.Age > threshold));
        results.RecordedItems.Should().Contain(changed);
    }

    [Fact]
    public void TransformManyOverObservableLists_ChildRefreshesItem_FiltersMergedItem()
    {
        // Arrange
        var threshold = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        using var first = CreatePeople(threshold);
        using var second = CreatePeople(threshold);
        using var firstRefreshing = first.Connect().AutoRefresh(static person => person.Age).AsObservableList();
        using var secondRefreshing = second.Connect().AutoRefresh(static person => person.Age).AsObservableList();
        using var parents = new SourceList<IObservableList<Person>>();
        parents.AddRange(new[] { firstRefreshing, secondRefreshing });
        var changed = _randomizer.ArrayElement(first.Items.ToArray());
        changed.Age = threshold;

        using var subscription = parents.Connect()
            .TransformMany(static people => people)
            .Filter(person => person.Age > threshold)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        changed.Age = threshold + 1;

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(first.Items.Concat(second.Items).Where(person => person.Age > threshold));
        results.RecordedItems.Should().Contain(changed);
    }

    private SourceList<Person> CreatePeople(int threshold)
    {
        var people = new SourceList<Person>();
        people.AddRange(Enumerable.Range(0, _randomizer.Int(MinimumEntryCount, MaximumEntryCount)).Select(_ => new Person(_randomizer.Hash(), _randomizer.Int(0, threshold * 2))).ToArray());

        return people;
    }
}
