using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public static partial class MergeManyChangeSetsFixture
{
    public static partial class ForListChangesets
    {
        #if DEBUG
        private const int InitialOwnerCount = 7;
        private const int AddRangeSize = 5;
        private const int RemoveRangeSize = 3;
        #else
        private const int InitialOwnerCount = 103;
        private const int AddRangeSize = 53;
        private const int RemoveRangeSize = 37;
        #endif

        private static void CheckResultContents(
            IReadOnlyList<AnimalOwner> owners,
            ChangeSetAggregator<AnimalOwner, Guid> ownerResults,
            ChangeSetAggregator<Animal> animalResults)
        {
            var expectedOwners = owners.ToList();

            // These should be subsets of each other
            expectedOwners.Should().BeSubsetOf(ownerResults.Data.Items);
            ownerResults.Data.Items.Count.Should().Be(expectedOwners.Count);

            // All owner animals should be in the results
            foreach (var owner in owners)
            {
                owner.Animals.Items.Should().BeSubsetOf(animalResults.Data.Items);
            }

            // Results should not have more than the total number of animals
            animalResults.Data.Count.Should().Be(owners.Sum(owner => owner.Animals.Count));
        }
    }
}
