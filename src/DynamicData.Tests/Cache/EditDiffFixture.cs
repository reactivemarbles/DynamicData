using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class EditDiffFixture : IDisposable
{
    private readonly SourceCache<Person, string> _cache;

    private readonly ChangeSetAggregator<Person, string> _result;

    public EditDiffFixture()
    {
        _cache = new SourceCache<Person, string>(p => p.Name);
        _result = _cache.Connect().AsAggregator();
        _cache.AddOrUpdate(Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray());
    }

    [Test]
    public async Task Amends()
    {
        var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
        _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

        await Assert.That(_cache.Count).IsEqualTo(3);

        var lastChange = _result.Messages.Last();
        await Assert.That(lastChange.Adds).IsEqualTo(0);
        await Assert.That(lastChange.Updates).IsEqualTo(3);
        await Assert.That(lastChange.Removes).IsEqualTo(7);

        await Assert.That(_cache.Items).IsEquivalentTo(newList, Person.NameAgeGenderComparer);
    }

    [Test]
    public async Task Amends_WithEqualityComparer()
    {
        var newList = Enumerable.Range(5, 3).Select(i => new Person("Name" + i, i + 10)).ToArray();
        _cache.EditDiff(newList, Person.AgeComparer);

        await Assert.That(_cache.Count).IsEqualTo(3);

        var lastChange = _result.Messages.Last();
        await Assert.That(lastChange.Adds).IsEqualTo(0);
        await Assert.That(lastChange.Updates).IsEqualTo(3);
        await Assert.That(lastChange.Removes).IsEqualTo(7);

        await Assert.That(_cache.Items).IsEquivalentTo(newList, Person.NameAgeGenderComparer);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _result.Dispose();
    }

    [Test]
    public async Task EditWithSameData()
    {
        var newPeople = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();

        _cache.EditDiff(newPeople, (current, previous) => Person.AgeComparer.Equals(current, previous));

        await Assert.That(_cache.Count).IsEqualTo(10);
        await Assert.That(_cache.Items).IsEquivalentTo(newPeople, Person.NameAgeGenderComparer);
        await Assert.That(_result.Messages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task EditWithSameData_WithEqualityComparer()
    {
        var newPeople = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();

        _cache.EditDiff(newPeople, Person.AgeComparer);

        await Assert.That(_cache.Count).IsEqualTo(10);
        await Assert.That(_cache.Items).IsEquivalentTo(newPeople, Person.NameAgeGenderComparer);
        var lastChange = _result.Messages.Last();
        await Assert.That(_result.Messages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task New()
    {
        var newPeople = Enumerable.Range(1, 15).Select(i => new Person("Name" + i, i)).ToArray();

        _cache.EditDiff(newPeople, (current, previous) => Person.AgeComparer.Equals(current, previous));

        await Assert.That(_cache.Count).IsEqualTo(15);
        await Assert.That(_cache.Items).IsEquivalentTo(newPeople, Person.NameAgeGenderComparer);
        var lastChange = _result.Messages.Last();
        await Assert.That(lastChange.Adds).IsEqualTo(5);
    }

    [Test]
    public async Task New_WithEqualityComparer()
    {
        var newPeople = Enumerable.Range(1, 15).Select(i => new Person("Name" + i, i)).ToArray();

        _cache.EditDiff(newPeople, Person.AgeComparer);

        await Assert.That(_cache.Count).IsEqualTo(15);
        await Assert.That(_cache.Items).IsEquivalentTo(newPeople, Person.NameAgeGenderComparer);
        var lastChange = _result.Messages.Last();
        await Assert.That(lastChange.Adds).IsEqualTo(5);
    }

    [Test]
    public async Task Removes()
    {
        var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
        _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

        await Assert.That(_cache.Count).IsEqualTo(7);

        var lastChange = _result.Messages.Last();
        await Assert.That(lastChange.Adds).IsEqualTo(0);
        await Assert.That(lastChange.Updates).IsEqualTo(0);
        await Assert.That(lastChange.Removes).IsEqualTo(3);

        await Assert.That(_cache.Items).IsEquivalentTo(newList, Person.NameAgeGenderComparer);
    }

    [Test]
    public async Task Removes_WithEqualityComparer()
    {
        var newList = Enumerable.Range(1, 7).Select(i => new Person("Name" + i, i)).ToArray();
        _cache.EditDiff(newList, Person.AgeComparer);

        await Assert.That(_cache.Count).IsEqualTo(7);

        var lastChange = _result.Messages.Last();
        await Assert.That(lastChange.Adds).IsEqualTo(0);
        await Assert.That(lastChange.Updates).IsEqualTo(0);
        await Assert.That(lastChange.Removes).IsEqualTo(3);

        await Assert.That(_cache.Items).IsEquivalentTo(newList, Person.NameAgeGenderComparer);
    }

    [Test]
    public async Task VariousChanges()
    {
        var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i + 10)).ToArray();

        _cache.EditDiff(newList, (current, previous) => Person.AgeComparer.Equals(current, previous));

        await Assert.That(_cache.Count).IsEqualTo(10);

        var lastChange = _result.Messages.Last();
        await Assert.That(lastChange.Adds).IsEqualTo(5);
        await Assert.That(lastChange.Updates).IsEqualTo(5);
        await Assert.That(lastChange.Removes).IsEqualTo(5);

        await Assert.That(_cache.Items).IsEquivalentTo(newList, Person.NameAgeGenderComparer);
    }

    [Test]
    public async Task VariousChanges_WithEqualityComparer()
    {
        var newList = Enumerable.Range(6, 10).Select(i => new Person("Name" + i, i + 10)).ToArray();

        _cache.EditDiff(newList, Person.AgeComparer);

        await Assert.That(_cache.Count).IsEqualTo(10);

        var lastChange = _result.Messages.Last();
        await Assert.That(lastChange.Adds).IsEqualTo(5);
        await Assert.That(lastChange.Updates).IsEqualTo(5);
        await Assert.That(lastChange.Removes).IsEqualTo(5);

        await Assert.That(_cache.Items).IsEquivalentTo(newList, Person.NameAgeGenderComparer);
    }
}
