using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformManySimpleFixture : IDisposable
{
    private readonly ChangeSetAggregator<Person, string> _results;

    private readonly ISourceCache<PersonWithChildren, string> _source;

    public TransformManySimpleFixture()
    {
        _source = new SourceCache<PersonWithChildren, string>(p => p.Key);

        _results = _source.Connect().TransformMany(p => p.Relations, p => p.Name).AsAggregator();
    }

    [Test]
    public async Task Adds()
    {
        var parent = new PersonWithChildren(
            "parent",
            50,
            new Person[]
            {
                new("Child1", 1),
                new("Child2", 2),
                new("Child3", 3)
            });
        _source.AddOrUpdate(parent);
        await Assert.That(_results.Data.Count).IsEqualTo(3).Because("Should be 4 in the cache");

        await Assert.That(_results.Data.Lookup("Child1").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Child2").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Child3").HasValue).IsTrue();
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task Remove()
    {
        var parent = new PersonWithChildren(
            "parent",
            50,
            new Person[]
            {
                new("Child1", 1), new("Child2", 2), new("Child3", 3)
            });
        _source.AddOrUpdate(parent);
        _source.Remove(parent);
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 4 in the cache");
    }

    [Test]
    public async Task RemovewithIncompleteChildren()
    {
        var parent1 = new PersonWithChildren(
            "parent",
            50,
            new Person[]
            {
                new("Child1", 1), new("Child2", 2), new("Child3", 3)
            });
        _source.AddOrUpdate(parent1);

        var parent2 = new PersonWithChildren(
            "parent",
            50,
            new Person[]
            {
                new("Child1", 1), new("Child3", 3)
            });
        _source.Remove(parent2);
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 0 in the cache");
    }

    [Test]
    public async Task UpdateWithLessChildren()
    {
        var parent1 = new PersonWithChildren(
            "parent",
            50,
            new Person[]
            {
                new("Child1", 1), new("Child2", 2), new("Child3", 3)
            });
        _source.AddOrUpdate(parent1);

        var parent2 = new PersonWithChildren(
            "parent",
            50,
            new Person[]
            {
                new("Child1", 1), new("Child3", 3),
            });
        _source.AddOrUpdate(parent2);
        await Assert.That(_results.Data.Count).IsEqualTo(2).Because("Should be 2 in the cache");
        await Assert.That(_results.Data.Lookup("Child1").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Child3").HasValue).IsTrue();
    }

    [Test]
    public async Task UpdateWithMultipleChanges()
    {
        var parent1 = new PersonWithChildren(
            "parent",
            50,
            new Person[]
            {
                new("Child1", 1), new("Child2", 2), new("Child3", 3)
            });
        _source.AddOrUpdate(parent1);

        var parent2 = new PersonWithChildren(
            "parent",
            50,
            new Person[]
            {
                new("Child1", 1), new("Child3", 3), new("Child5", 3),
            });
        _source.AddOrUpdate(parent2);
        await Assert.That(_results.Data.Count).IsEqualTo(3).Because("Should be 2 in the cache");
        await Assert.That(_results.Data.Lookup("Child1").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Child3").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Child5").HasValue).IsTrue();
    }
}
