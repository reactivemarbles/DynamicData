using System;

using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

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

    [Fact]
    public void ChildrenAreRemovedWhenParentIsRemoved()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
        //  var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

        _source.Add(mother);
        _source.Remove(mother);
        _results.Data.Count.Should().Be(0, "Should be 4 in the cache");
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void RecursiveChildrenCanBeAdded()
    {
        var frientofchild1 = new PersonWithRelations("Friend1", 10);
        var child1 = new PersonWithRelations("Child1", 10, new[] { frientofchild1 });
        var child2 = new PersonWithRelations("Child2", 8);
        var child3 = new PersonWithRelations("Child3", 8);
        var mother = new PersonWithRelations("Mother", 35, new[] { child1, child2, child3 });
        //  var father = new PersonWithRelations("Father", 35, new[] {child1, child2, child3, mother});

        _source.Add(mother);

        _results.Data.Count.Should().Be(4, "Should be 4 in the cache");
        _results.Data.Items.IndexOfOptional(child1).HasValue.Should().BeTrue();
        _results.Data.Items.IndexOfOptional(child2).HasValue.Should().BeTrue();
        _results.Data.Items.IndexOfOptional(child3).HasValue.Should().BeTrue();
        _results.Data.Items.IndexOfOptional(frientofchild1).HasValue.Should().BeTrue();
    }
}
