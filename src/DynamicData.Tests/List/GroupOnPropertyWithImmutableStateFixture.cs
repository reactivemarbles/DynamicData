#if REACTIVE_TESTS
using DataList = DynamicData.Reactive.List;
#else
using DataList = DynamicData.List;
#endif
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class GroupOnPropertyWithImmutableStateFixture : IDisposable
{
    private readonly ChangeSetAggregator<DataList.IGrouping<Person, int>> _results;

    private readonly ISourceList<Person> _source;

    public GroupOnPropertyWithImmutableStateFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect().GroupOnPropertyWithImmutableState(p => p.Age).AsAggregator();
    }

    [Test]
    public async Task CanGroupOnAdds()
    {
        _source.Add(new Person("A", 10));

        await Assert.That(_results.Data.Count).IsEqualTo(1);

        var firstGroup = _results.Data.Items[0];

        await Assert.That(firstGroup.Count).IsEqualTo(1);
        await Assert.That(firstGroup.Key).IsEqualTo(10);
    }

    [Test]
    public async Task CanHandleAddBatch()
    {
        var generator = new RandomPersonGenerator();
        var people = generator.Take(1000).ToArray();

        _source.AddRange(people);

        var expectedGroupCount = people.Select(p => p.Age).Distinct().Count();
        await Assert.That(_results.Data.Count).IsEqualTo(expectedGroupCount);
    }

    [Test]
    public async Task CanHandleChangedItemsBatch()
    {
        var generator = new RandomPersonGenerator();
        var people = generator.Take(100).ToArray();

        _source.AddRange(people);

        var initialCount = people.Select(p => p.Age).Distinct().Count();
        await Assert.That(_results.Data.Count).IsEqualTo(initialCount);

        people.Take(25).ForEach(p => p.Age = 200);

        var changedCount = people.Select(p => p.Age).Distinct().Count();
        await Assert.That(_results.Data.Count).IsEqualTo(changedCount);

        //check that each item is only in one cache
        var peopleInCache = _results.Data.Items.SelectMany(g => g.Items).ToArray();

        await Assert.That(peopleInCache.Length).IsEqualTo(100);
    }

    [Test]
    public async Task CanRemoveFromGroup()
    {
        var person = new Person("A", 10);
        _source.Add(person);
        _source.Remove(person);

        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task Regroup()
    {
        var person = new Person("A", 10);
        _source.Add(person);
        person.Age = 20;

        await Assert.That(_results.Data.Count).IsEqualTo(1);
        var firstGroup = _results.Data.Items[0];

        await Assert.That(firstGroup.Count).IsEqualTo(1);
        await Assert.That(firstGroup.Key).IsEqualTo(20);
    }
}
