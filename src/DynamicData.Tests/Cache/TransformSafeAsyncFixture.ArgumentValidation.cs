// Copyright (c) 2011-2026 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Person = DynamicData.Tests.Domain.Person;

namespace DynamicData.Tests.Cache;

public partial class TransformSafeAsyncFixture
{
    /// <summary>Verifies that the options overload rejects a null factory without subscribing to the source.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OptionsOverload_NullFactory_ThrowsBeforeSubscription(bool populateSource)
    {
        // Arrange
        using var source = new SourceCache<Person, string>(static person => person.Key);

        if (populateSource)
        {
            source.AddOrUpdate(new Person("Person", 20));
        }

        Func<Person, Optional<Person>, string, Task<Person>>? transformFactory = null;

        // Act
        Action action = () => source.Connect().TransformSafeAsync(transformFactory!, static _ => { }, TransformAsyncOptions.Default);

        // Assert
        action.Should().Throw<ArgumentNullException>(because: "invalid factories must be rejected before processing any items")
            .WithParameterName(nameof(transformFactory));
    }
}
