using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Kernal;

public class UpdateFixture
{
    [Test]
    public async Task Add()
    {
        var person = new Person("Person", 10);
        var update = new Change<Person, string>(ChangeReason.Add, "Person", person);

        await Assert.That(update.Key).IsEqualTo("Person");
        await Assert.That(update.Reason).IsEqualTo(ChangeReason.Add);
        await Assert.That(update.Current).IsEqualTo(person);
        await Assert.That(update.Previous).IsEqualTo(ReactiveUI.Primitives.Optional<Person>.None);
    }

    [Test]
    public async Task Remove()
    {
        var person = new Person("Person", 10);
        var update = new Change<Person, string>(ChangeReason.Remove, "Person", person);

        await Assert.That(update.Key).IsEqualTo("Person");
        await Assert.That(update.Reason).IsEqualTo(ChangeReason.Remove);
        await Assert.That(update.Current).IsEqualTo(person);
        await Assert.That(update.Previous).IsEqualTo(ReactiveUI.Primitives.Optional<Person>.None);
    }

    [Test]
    public async Task Update()
    {
        var current = new Person("Person", 10);
        var previous = new Person("Person", 9);
        var update = new Change<Person, string>(ChangeReason.Update, "Person", current, previous);

        await Assert.That(update.Key).IsEqualTo("Person");
        await Assert.That(update.Reason).IsEqualTo(ChangeReason.Update);
        await Assert.That(update.Current).IsEqualTo(current);
        await Assert.That(update.Previous.HasValue).IsTrue();
        await Assert.That(update.Previous.Value).IsEqualTo(previous);
    }

    [Test]
    public async Task UpdateWillThrowIfNoPreviousValueIsSupplied()
    {
        var current = new Person("Person", 10);
        await Assert.That(() => new Change<Person, string>(ChangeReason.Update, "Person", current)).Throws<ArgumentException>();
    }
}
