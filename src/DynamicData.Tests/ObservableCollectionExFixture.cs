
using System.Collections.ObjectModel;
using DynamicData.Binding;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests;

public class ObservableCollectionExFixture
{
    private readonly Person _person1 = new("One", 1);

    private readonly Person _person2 = new("Two", 2);

    private readonly Person _person3 = new("Three", 3);

    [Fact]
    public void CanConvertToObservableChangeSetList()
    {
        var source = new ObservableCollection<Person> { _person1, _person2, _person3 };
        var changeSet = source.ToObservableChangeSet().AsObservableList();
        changeSet.Items.Should().BeEquivalentTo(source);
    }

    [Fact]
    public void CanConvertToObservableChangeSetCache()
    {
        var source = new ObservableCollection<Person> { _person1, _person2, _person3 };
        var changeSet = source.ToObservableChangeSet(x => x.Name).AsObservableCache();
        changeSet.Items.Should().BeEquivalentTo(source);
        var one = changeSet.Lookup("One").Value;
        one.Should().BeEquivalentTo(_person1);
    }


    [Fact]
    public void ReplacingAnItemWithSameProducesUpdate()
    {
        var source = new ObservableCollection<Person> { _person1, _person2, _person3 };
        var aggregator = source.ToObservableChangeSet(x => x.Name).AsAggregator();
        source[0] = new Person("One", 100);
        aggregator.Summary.Latest.Updates.Should().Be(1);
        aggregator.Summary.Latest.Adds.Should().Be(0);
        aggregator.Summary.Latest.Removes.Should().Be(0);
    }
}
