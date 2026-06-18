using DynamicData.Tests.Domain;

using FluentAssertions;

namespace DynamicData.Tests.Kernal;

public class KeyValueFixture
{
    [Fact]
    public void Create()
    {
        var person = new Person("Person", 10);
        var kv = new KeyValuePair<string, Person>("Person", person);

        kv.Key.Should().Be("Person");
        kv.Value.Should().Be(person);
    }
}
