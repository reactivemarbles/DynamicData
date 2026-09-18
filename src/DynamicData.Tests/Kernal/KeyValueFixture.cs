using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Kernal;

public class KeyValueFixture
{
    [Test]
    public async Task Create()
    {
        var person = new Person("Person", 10);
        var kv = new KeyValuePair<string, Person>("Person", person);

        await Assert.That(kv.Key).IsEqualTo("Person");
        await Assert.That(kv.Value).IsEqualTo(person);
    }
}
