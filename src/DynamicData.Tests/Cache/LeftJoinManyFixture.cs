#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class LeftJoinManyFixture : IDisposable
{
    private readonly SourceCache<Person, string> _people;

    private readonly ChangeSetAggregator<ParentAndChildren, string> _result;

    public LeftJoinManyFixture()
    {
        _people = new SourceCache<Person, string>(p => p.Name);

        _result = _people.Connect().LeftJoinMany(_people.Connect(), pac => pac.ParentName, (person, grouping) => new ParentAndChildren(person, grouping.Items.Select(p => p).ToArray())).AsAggregator();
    }

    [Test]
    public async Task AddChild()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        var person11 = new Person("Person11", 100, parentName: "Person3");
        _people.AddOrUpdate(person11);

        var updatedPeople = people.Union(new[] { person11 }).ToArray();

        await AssertDataIsCorrectlyFormed(updatedPeople);
    }

    [Test]
    public async Task AddLeftOnly()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Person" + i, i)).ToArray();

        _people.AddOrUpdate(people);

        await Assert.That(_result.Data.Count).IsEqualTo(10);
        await Assert.That(_result.Data.Items.Select(pac => pac.Parent)).IsEquivalentTo(people.Cast<Person?>());

        foreach (var pac in _result.Data.Items) { await Assert.That(pac.Count).IsEqualTo(0); }
    }

    [Test]
    public async Task AddPeopleWithParents()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        await AssertDataIsCorrectlyFormed(people);
    }

    public void Dispose()
    {
        _people.Dispose();
        _result.Dispose();
    }

    [Test]
    public async Task RefreshRightKey()
    {
        _people.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Person() { Name = "Person #1", ParentName = string.Empty });
                innerCache.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #1" });
                innerCache.AddOrUpdate(new Person() { Name = "Person #3", ParentName = "Person #2" });
            });

        var refreshPerson = _people.Lookup("Person #2").Value;

        // Change pairing
        refreshPerson.ParentName = "Person #3";
        _people.Refresh(refreshPerson);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name)))).DoesNotContain(("Person #1", "Person #2"));
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name)))).Contains(("Person #3", "Person #2"));

        // Remove pairing
        refreshPerson.ParentName = "Person #4";
        _people.Refresh(refreshPerson);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (parentName: family.Parent.Name, chilldName: child.Name)))).DoesNotContain(pair => pair.chilldName == "Person #2");

        // Restore pairing
        refreshPerson.ParentName = "Person #1";
        _people.Refresh(refreshPerson);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name)))).Contains(("Person #1", "Person #2"));

        // No change
        _people.Refresh(refreshPerson);

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name)))).Contains(("Person #1", "Person #2"));
    }

    [Test]
    public async Task RemoveChild()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        var last = people.Last();
        _people.Remove(last);

        var updatedPeople = people.Where(p => p.Name != last.Name).ToArray();

        await AssertDataIsCorrectlyFormed(updatedPeople, last.Name);
    }

    [Test]
    public async Task UpdateChild()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        var current6 = people[5];
        var person6 = new Person("Person6", 100, parentName: current6.ParentName);
        _people.AddOrUpdate(person6);

        var updatedPeople = people.Where(p => p.Name != "Person6").Union(new[] { person6 }).ToArray();

        await AssertDataIsCorrectlyFormed(updatedPeople);
    }

    [Test]
    public async Task UpdateParent()
    {
        var people = Enumerable.Range(1, 10).Select(
            i =>
            {
                var parent = "Person" + CalculateParent(i, 10);
                return new Person("Person" + i, i, parentName: parent);
            }).ToArray();

        _people.AddOrUpdate(people);

        var current10 = people.Last();
        var person10 = new Person("Person10", 100, parentName: current10.ParentName);
        _people.AddOrUpdate(person10);

        var updatedPeople = people.Take(9).Union(new[] { person10 }).ToArray();

        await AssertDataIsCorrectlyFormed(updatedPeople);
    }

    [Test]
    public async Task UpdateRightKey()
    {
        _people.Edit(
            innerCache =>
            {
                innerCache.AddOrUpdate(new Person() { Name = "Person #1", ParentName = string.Empty });
                innerCache.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #1" });
                innerCache.AddOrUpdate(new Person() { Name = "Person #3", ParentName = "Person #2" });
            });

        // Change pairing
        _people.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #3" });

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name)))).DoesNotContain(("Person #1", "Person #2"));
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name)))).Contains(("Person #3", "Person #2"));

        // Remove pairing
        _people.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #4" });

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (parentName: family.Parent.Name, chilldName: child.Name)))).DoesNotContain(pair => pair.chilldName == "Person #2");

        // Restore pairing
        _people.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #1" });

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name)))).Contains(("Person #1", "Person #2"));

        // No change
        _people.AddOrUpdate(new Person() { Name = "Person #2", ParentName = "Person #1" });

        await Assert.That(_result.Data.Count).IsEqualTo(3);
        await Assert.That(_result.Data.Items.SelectMany(family => family.Children.Select(child => (family.Parent.Name, child.Name)))).Contains(("Person #1", "Person #2"));
    }

    private async Task AssertDataIsCorrectlyFormed(Person[] expected, params string[] missingParents)
    {
        await Assert.That(_result.Data.Count).IsEqualTo(expected.Length);
        await Assert.That(_result.Data.Items.Select(pac => pac.Parent)).IsEquivalentTo(expected.Cast<Person?>());

        foreach (var grouping in expected.GroupBy(p => p.ParentName))
        {
            if (missingParents.Length > 0 && missingParents.Contains(grouping.Key))
            {
                continue;
            }

            var result = _result.Data.Lookup(grouping.Key).ValueOrThrow(() => new Exception("Missing result for " + grouping.Key));

            var children = result.Children;
            await Assert.That(children).IsEquivalentTo(grouping);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Accetable for test.")]
    private int CalculateParent(int index, int totalPeople)
    {
        if (index < 5)
        {
            return 10;
        }

        if (index == totalPeople - 1)
        {
            return 1;
        }

        if (index == totalPeople)
        {
            return 1;
        }

        return index + 1;
    }
}
