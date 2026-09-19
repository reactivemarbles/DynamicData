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

public class GroupImmutableFixture : IDisposable
{
    private readonly ReactiveUI.Primitives.Signals.ISignal<Unit> _regrouper;

    private readonly ChangeSetAggregator<DataList.IGrouping<Person, int>> _results;

    private readonly ISourceList<Person> _source;

    public GroupImmutableFixture()
    {
        _source = new SourceList<Person>();
        _regrouper = new ReactiveUI.Primitives.Signals.Signal<Unit>();
        _results = _source.Connect().GroupWithImmutableState(p => p.Age, _regrouper).AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        _source.Add(new Person("Person1", 20));
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 add");
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1);
    }

    [Test]
    public async Task ChanegMultipleGroups()
    {
        var initialPeople = Enumerable.Range(1, 10000).Select(i => new Person("Person" + i, i % 10)).ToArray();

        _source.AddRange(initialPeople);

        foreach (var group in initialPeople.GroupBy(p => p.Age))
        {
            var grp = _results.Data.Items.First(g => g.Key.Equals(group.Key));
            await Assert.That(grp.Items).IsEquivalentTo(group.ToArray());
        }

        _source.RemoveMany(initialPeople.Take(15));

        foreach (var group in initialPeople.Skip(15).GroupBy(p => p.Age))
        {
            var list = _results.Data.Items.First(p => p.Key == group.Key);
            await Assert.That(list.Items).IsEquivalentTo(group);
        }

        await Assert.That(_results.Messages.Count).IsEqualTo(2);
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(10);
        await Assert.That(_results.Messages.Skip(1).First().Replaced).IsEqualTo(10);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
        _regrouper.Dispose();
    }

    [Test]
    public async Task FiresManyValueForBatchOfDifferentAdds()
    {
        _source.Edit(
            updater =>
            {
                updater.Add(new Person("Person1", 20));
                updater.Add(new Person("Person2", 21));
                updater.Add(new Person("Person3", 22));
                updater.Add(new Person("Person4", 23));
            });

        await Assert.That(_results.Data.Count).IsEqualTo(4);
        await Assert.That(_results.Messages.Count).IsEqualTo(1);
        await Assert.That(_results.Messages.First().Count).IsEqualTo(1);
        foreach (var update in _results.Messages.First())
        {
            await Assert.That(update.Reason).IsEqualTo(ListChangeReason.AddRange);
        }
    }

    [Test]
    public async Task FiresOnlyOnceForABatchOfUniqueValues()
    {
        _source.Edit(
            updater =>
            {
                updater.Add(new Person("Person1", 20));
                updater.Add(new Person("Person2", 20));
                updater.Add(new Person("Person3", 20));
                updater.Add(new Person("Person4", 20));
            });

        await Assert.That(_results.Messages.Count).IsEqualTo(1);
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(_results.Data.Items[0].Count).IsEqualTo(4);
    }

    [Test]
    public async Task Reevaluate()
    {
        var initialPeople = Enumerable.Range(1, 10).Select(i => new Person("Person" + i, i % 2)).ToArray();

        _source.AddRange(initialPeople);
        await Assert.That(_results.Messages.Count).IsEqualTo(1);

        //do an inline update
        foreach (var person in initialPeople)
        {
            person.Age += 1;
        }

        //signal operators to evaluate again
        _regrouper.OnNext();

        foreach (var groupContainer in initialPeople.GroupBy(p => p.Age))
        {
            var grouping = _results.Data.Items.First(g => g.Key == groupContainer.Key);
            await Assert.That(grouping.Items).IsEquivalentTo(groupContainer);
        }

        await Assert.That(_results.Data.Count).IsEqualTo(2);
        await Assert.That(_results.Messages.Count).IsEqualTo(2);

        var secondMessage = _results.Messages.Skip(1).First();
        await Assert.That(secondMessage.Removes).IsEqualTo(1);
        await Assert.That(secondMessage.Replaced).IsEqualTo(1);
        await Assert.That(secondMessage.Adds).IsEqualTo(1);
    }

    [Test]
    public async Task Remove()
    {
        var person = new Person("Person1", 20);
        _source.Add(person);
        _source.Remove(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(2);
        await Assert.That(_results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task UpdateAnItemWillChangedThegroup()
    {
        var person1 = new Person("Person1", 20);
        _source.Add(person1);
        _source.Replace(person1, new Person("Person1", 21));

        await Assert.That(_results.Data.Count).IsEqualTo(1);
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(_results.Messages.Skip(1).First().Adds).IsEqualTo(1);
        await Assert.That(_results.Messages.Skip(1).First().Removes).IsEqualTo(1);
        var group = _results.Data.Items[0];
        await Assert.That(group.Count).IsEqualTo(1);

        await Assert.That(group.Key).IsEqualTo(21);
    }

    [Test]
    public async Task UpdatesArePermissible()
    {
        _source.Add(new Person("Person1", 20));
        _source.Add(new Person("Person2", 20));

        await Assert.That(_results.Data.Count).IsEqualTo(1); //1 group
        await Assert.That(_results.Messages.First().Adds).IsEqualTo(1);
        await Assert.That(_results.Messages.Skip(1).First().Replaced).IsEqualTo(1);

        var group = _results.Data.Items[0];
        await Assert.That(group.Count).IsEqualTo(2);
    }
}
