#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests;

public class ObservableCollectionExFixture
{
    private readonly Person _person1 = new("One", 1);

    private readonly Person _person2 = new("Two", 2);

    private readonly Person _person3 = new("Three", 3);

    [Test]
    public async Task CanConvertToObservableChangeSetList()
    {
        var source = new ObservableCollection<Person> { _person1, _person2, _person3 };
        var changeSet = source.ToObservableChangeSet().AsObservableList();
        await Assert.That(changeSet.Items).IsEquivalentTo(source);
    }

    [Test]
    public async Task CanConvertToObservableChangeSetCache()
    {
        var source = new ObservableCollection<Person> { _person1, _person2, _person3 };
        var changeSet = source.ToObservableChangeSet(x => x.Name).AsObservableCache();
        await Assert.That(changeSet.Items).IsEquivalentTo(source);
        var one = changeSet.Lookup("One").Value;
        await Assert.That(one).IsEquivalentTo(_person1);
    }

    [Test]
    public async Task ReplacingAnItemWithSameProducesUpdate()
    {
        var source = new ObservableCollection<Person> { _person1, _person2, _person3 };
        var aggregator = source.ToObservableChangeSet(x => x.Name).AsAggregator();
        source[0] = new Person("One", 100);
        await Assert.That(aggregator.Summary.Latest.Updates).IsEqualTo(1);
        await Assert.That(aggregator.Summary.Latest.Adds).IsEqualTo(0);
        await Assert.That(aggregator.Summary.Latest.Removes).IsEqualTo(0);
    }
}
