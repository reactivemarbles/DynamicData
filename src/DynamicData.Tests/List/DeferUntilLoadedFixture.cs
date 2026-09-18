using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class DeferAnsdSkipFixture
{
    [Test]
    public async Task DeferUntilLoadedDoesNothingUntilDataHasBeenReceived()
    {
        var updateReceived = false;
        IChangeSet<Person>? result = null;

        var cache = new SourceList<Person>();

        var deferStream = cache.Connect().DeferUntilLoaded().Subscribe(
            changes =>
            {
                updateReceived = true;
                result = changes;
            });

        var person = new Person("Test", 1);

        await Assert.That(updateReceived).IsFalse();
        cache.Add(person);

        await Assert.That(updateReceived).IsTrue();

        if (result is null)
        {
            throw new InvalidOperationException(nameof(result));
        }

        await Assert.That(result.Adds).IsEqualTo(1);
        await Assert.That(result.Unified().First().Current).IsEqualTo(person);
        deferStream.Dispose();
    }

    [Test]
    public async Task SkipInitialDoesNotReturnTheFirstBatchOfData()
    {
        var updateReceived = false;

        var cache = new SourceList<Person>();

        var deferStream = cache.Connect().SkipInitial().Subscribe(changes => updateReceived = true);

        await Assert.That(updateReceived).IsFalse();

        cache.Add(new Person("P1", 1));

        await Assert.That(updateReceived).IsFalse();

        cache.Add(new Person("P2", 2));
        await Assert.That(updateReceived).IsTrue();
        deferStream.Dispose();
    }
}
