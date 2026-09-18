using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformManyFixture : IDisposable
{
    private readonly ChangeSetAggregator<PersonWithRelations, string> _results;

    private readonly ISourceCache<PersonWithRelations, string> _source;

    public TransformManyFixture()
    {
        _source = new SourceCache<PersonWithRelations, string>(p => p.Key);

        _results = _source.Connect().TransformMany(p => p.Relations.RecursiveSelect(r => r.Relations), p => p.Name).IgnoreUpdateWhen((current, previous) => current.Name == previous.Name).AsAggregator();
    }

    [Test]
    public async Task ChildrenAreRemovedWhenParentIsRemoved()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
        //  var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

        _source.AddOrUpdate(mother);
        _source.Remove(mother);
        await Assert.That(_results.Data.Count).IsEqualTo(0).Because("Should be 4 in the cache");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Test]
    public async Task RecursiveChildrenCanBeAdded()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
        //  var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

        _source.AddOrUpdate(mother);

        await Assert.That(_results.Data.Count).IsEqualTo(4).Because("Should be 4 in the cache");
        await Assert.That(_results.Data.Lookup("Child1").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Child2").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Child3").HasValue).IsTrue();
        await Assert.That(_results.Data.Lookup("Friend1").HasValue).IsTrue();
    }
}
