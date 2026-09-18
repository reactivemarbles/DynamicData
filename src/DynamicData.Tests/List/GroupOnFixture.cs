using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class GroupOnFixture : IDisposable
{
    private readonly ChangeSetAggregator<IGroup<Person, int>> _results;

    private readonly ISourceList<Person> _source;

    public GroupOnFixture()
    {
        _source = new SourceList<Person>();
        _results = _source.Connect().GroupOn(p => p.Age).AsAggregator();
    }

    [Test]
    public async Task Add()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");

        var firstGroup = _results.Data.Items[0].List.Items.ToArray();
        await Assert.That(firstGroup[0]).IsEqualTo(person).Because("Should be same person");
    }

    [Test]
    public async Task BigList()
    {
        var generator = new RandomPersonGenerator();
        var people = generator.Take(10000).ToArray();
        _source.AddRange(people);

        Console.WriteLine();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task Remove()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);
        _source.Remove(person);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 1 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be no groups");
    }

    [Test]
    public async Task UpdateWillChangeTheGroup()
    {
        var person = new Person("Adult1", 50);
        var amended = new Person("Adult1", 60);
        _source.Add(person);
        _source.ReplaceAt(0, amended);

        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(_results.Data.Count).IsEqualTo(1).Because("Should be 1 item in the cache");

        var firstGroup = _results.Data.Items[0].List.Items.ToArray();
        await Assert.That(firstGroup[0]).IsEqualTo(amended).Because("Should be same person");
    }
}
