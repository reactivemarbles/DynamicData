using Xunit;

namespace DynamicData.Tests;

[Collection(CollectionName)]
[CollectionDefinition(CollectionName, DisableParallelization = true)]
public class IntegrationTestFixtureBase
{
    public const string CollectionName
        = "IntegrationTests";
}
