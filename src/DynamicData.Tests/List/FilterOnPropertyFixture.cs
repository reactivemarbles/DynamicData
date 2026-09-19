using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class FilterOnPropertyFixture
{
    [Test]
    public async Task ChangeAValueSoItIsStillInTheFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);

        people[50].Age = 100;
        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2);
        await Assert.That(stub.Results.Data.Count).IsEqualTo(82);
    }

    [Test]
    public async Task ChangeAValueToMatchFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);

        people[20].Age = 10;

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2);
        await Assert.That(stub.Results.Data.Count).IsEqualTo(81);
    }

    [Test]
    public async Task ChangeAValueToNoLongerMatchFilter()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);

        people[10].Age = 20;

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(2);
        await Assert.That(stub.Results.Data.Count).IsEqualTo(83);
    }

    [Test]
    public async Task Clear()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);
        stub.Source.Clear();

        await Assert.That(stub.Results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InitialValues()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);

        await Assert.That(stub.Results.Messages.Count).IsEqualTo(1);
        await Assert.That(stub.Results.Data.Count).IsEqualTo(82);

        await Assert.That(stub.Results.Data.Items).IsEquivalentTo(people.Skip(18));
    }

    [Test]
    public async Task RemoveRange()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new FilterPropertyStub();
        stub.Source.AddRange(people);
        stub.Source.RemoveRange(89, 10);

        await Assert.That(stub.Results.Data.Count).IsEqualTo(72);
    }

    private class FilterPropertyStub : IDisposable
    {
        public FilterPropertyStub() => Results = new ChangeSetAggregator<Person>(Source.Connect().FilterOnProperty(p => p.Age, p => p.Age > 18));

        public ChangeSetAggregator<Person> Results { get; }

        public ISourceList<Person> Source { get; } = new SourceList<Person>();

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }
    }
}
