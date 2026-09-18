using DynamicData.Tests.Domain;

namespace DynamicData.Tests;

public class EnumerableExFixtures
{
    private readonly Person _person1 = new("One", 1);

    private readonly Person _person2 = new("Two", 2);

    private readonly Person _person3 = new("Three", 3);

    [Test]
    public async Task CanConvertToObservableChangeSetCache()
    {
        var source = new[] { _person1, _person2, _person3 };
        var changeSet = source.AsObservableChangeSet().AsObservableList();
        await Assert.That(changeSet.Items).IsEquivalentTo(source);
    }

    [Test]
    public async Task CanConvertToObservableChangeSetList()
    {
        var source = new[] { _person1, _person2, _person3 };
        var changeSet = source.AsObservableChangeSet(x => x.Age).AsObservableCache();
        await Assert.That(changeSet.Items).IsEquivalentTo(source);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RespectsCompleteConfigurationForCache(bool shouldComplete)
    {
        var completed = false;
        var source = new[] { _person1, _person2, _person3 };
        using (source.AsObservableChangeSet(x => x.Age, shouldComplete).Subscribe(_ => { }, () => completed = true))
        {
            await Assert.That(completed).IsEqualTo(shouldComplete);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RespectsCompleteConfigurationForList(bool shouldComplete)
    {
        var completed = false;
        var source = new[] { _person1, _person2, _person3 };
        using (source.AsObservableChangeSet(shouldComplete).Subscribe(_ => { }, () => completed = true))
        {
            await Assert.That(completed).IsEqualTo(shouldComplete);
        }
    }
}
