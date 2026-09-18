using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class RecursiveTransformManyFixture : IDisposable
{
    private readonly ChangeSetAggregator<PersonWithRelations> _results;

    private readonly ISourceList<PersonWithRelations> _source;

    public RecursiveTransformManyFixture()
    {
        _source = new SourceList<PersonWithRelations>();

        _results = _source.Connect().TransformMany(p => p.Relations.RecursiveSelect(r => r.Relations)).AsAggregator();
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

        _source.Add(mother);
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

        _source.Add(mother);

        await Assert.That(_results.Data.Count).IsEqualTo(4).Because("Should be 4 in the cache");
        await Assert.That(_results.Data.Items.IndexOfOptional(child1).HasValue).IsTrue();
        await Assert.That(_results.Data.Items.IndexOfOptional(child2).HasValue).IsTrue();
        await Assert.That(_results.Data.Items.IndexOfOptional(child3).HasValue).IsTrue();
        await Assert.That(_results.Data.Items.IndexOfOptional(frientofchild1).HasValue).IsTrue();
    }
}
