using System;
using System.Linq;

using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class MergeManyChangeSetsParentUpdateFixture
{
    [Fact]
    public void ParentUpdateDoesNotEmitDuplicateAdds()
    {
        using var owners = new SourceCache<AnimalOwner, Guid>(o => o.Id);

        var owner = new AnimalOwner("Owner");
        owner.Animals.AddRange([
            new Animal("A1", "Type", AnimalFamily.Mammal),
            new Animal("A2", "Type", AnimalFamily.Mammal),
        ]);
        owners.AddOrUpdate(owner);

        using var subscription = owners.Connect()
            .MergeManyChangeSets(o => o.Animals.Connect())
            .Transform(a => new Person(a.Name, a.Name.Length))
            .AddKey(static person => person.Name)
            .ValidateChangeSets(static person => person.Name)
            .RecordCacheItems(out var results);

        results.Error.Should().BeNull("the initial subscription should be valid");

        // Re-adding the same instance is an Update on the parent, which swaps the child subscription.
        owners.AddOrUpdate(owner);

        results.Error.Should().BeNull("a parent update must not re-add children that are already present");
        results.RecordedItemsByKey.Should().HaveCount(2, "the owner still has exactly two animals");
    }
}

