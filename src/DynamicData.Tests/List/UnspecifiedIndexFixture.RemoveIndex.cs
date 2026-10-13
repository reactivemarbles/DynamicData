using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;

using FluentAssertions;

using Xunit;

using DynamicData.Tests.Utilities;

using Person = DynamicData.Tests.Domain.Person;

namespace DynamicData.Tests.List;

public sealed partial class UnspecifiedIndexFixture
{
    public enum IndexStrippingOperator
    {
        RemoveIndex,
        WhereReasonsAreRefresh,
        WhereReasonsAreNotMoved,
    }

    [Fact]
    public void AutoRefreshThenRemoveIndexThenFilter_PropertyChange_ReevaluatesItem()
    {
        // Arrange
        var threshold = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        using var people = CreatePeople(threshold);
        var changed = _randomizer.ArrayElement(people.Items.ToArray());
        changed.Age = threshold;

        using var subscription = people.Connect()
            .AutoRefresh(static person => person.Age)
            .RemoveIndex()
            .Filter(person => person.Age > threshold)
            .ValidateSynchronization()
            .ValidateChangeSets()
            .RecordListItems(out var results);

        // Act
        changed.Age = threshold + 1;

        // Assert
        results.Error.Should().BeNull();
        results.RecordedItems.Should().BeEquivalentTo(people.Items.Where(person => person.Age > threshold));
        results.RecordedItems.Should().Contain(changed);
    }

    [Fact]
    public void AutoRefreshThenWhereReasonsAreRefreshThenSuppressRefresh_PropertyChange_EmitsSelfReplace()
    {
        // Arrange
        var threshold = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        using var people = CreatePeople(threshold);
        var changed = _randomizer.ArrayElement(people.Items.ToArray());
        changed.Age = threshold;

        using var subscription = people.Connect()
            .AutoRefresh(static person => person.Age)
            .WhereReasonsAre(ListChangeReason.Refresh)
            .SuppressRefresh()
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act
        changed.Age = threshold + 1;

        // Assert
        results.Error.Should().BeNull();
        var change = results.RecordedValues.Should().ContainSingle().Which.Should().ContainSingle().Which;
        change.Reason.Should().Be(ListChangeReason.Replace);
        change.Item.Current.Should().BeSameAs(changed);
        change.Item.Previous.Value.Should().BeSameAs(changed);
        change.Item.CurrentIndex.Should().Be(-1);
        change.Item.PreviousIndex.Should().Be(-1);
    }

    [Fact]
    public void AutoRefreshThenWhereReasonsAreNotThenFilterThenBind_PropertyChange_ReevaluatesItem()
    {
        // Arrange
        var threshold = _randomizer.Int(MinimumEntryCount, MaximumEntryCount);
        using var people = CreatePeople(threshold);
        var changed = _randomizer.ArrayElement(people.Items.ToArray());
        changed.Age = threshold;

        using var subscription = people.Connect()
            .AutoRefresh(static person => person.Age)
            .WhereReasonsAreNot(ListChangeReason.Moved)
            .Filter(person => person.Age > threshold)
            .Bind(out ReadOnlyObservableCollection<Person> collection)
            .Subscribe();

        // Act
        changed.Age = threshold + 1;

        // Assert
        collection.Should().BeEquivalentTo(people.Items.Where(person => person.Age > threshold));
        collection.Should().Contain(changed);
    }

    [Theory]
    [InlineData(IndexStrippingOperator.RemoveIndex)]
    [InlineData(IndexStrippingOperator.WhereReasonsAreRefresh)]
    [InlineData(IndexStrippingOperator.WhereReasonsAreNotMoved)]
    public void IndexedRefresh_IndexStrippingOperator_EmitsUnindexedSelfReplace(IndexStrippingOperator stripping)
    {
        // Arrange
        var values = GenerateDistinctValues(2);
        var (other, refreshed) = (values[0], values[1]);

        using var source = new Subject<IChangeSet<int>>();
        var stripped = stripping switch
        {
            IndexStrippingOperator.RemoveIndex => source.RemoveIndex(),
            IndexStrippingOperator.WhereReasonsAreRefresh => source.WhereReasonsAre(ListChangeReason.Refresh),
            _ => source.WhereReasonsAreNot(ListChangeReason.Moved),
        };

        using var subscription = stripped
            .ValidateSynchronization()
            .RecordValues(out var results);

        // Act
        source.OnNext(new ChangeSet<int> { new Change<int>(ListChangeReason.Refresh, refreshed, 1), new Change<int>(other, 0, 1) });

        // Assert
        results.Error.Should().BeNull();
        var change = results.RecordedValues.Should().ContainSingle().Which.Should().ContainSingle().Which;
        (change.Reason, change.Item.Current, change.Item.Previous.Value, change.Item.CurrentIndex, change.Item.PreviousIndex).Should().Be((ListChangeReason.Replace, refreshed, refreshed, -1, -1));
    }
}
