using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

public class BufferInitialFixture
{
    private static readonly ICollection<Person> People = Enumerable.Range(1, 10_000).Select(i => new Person(i.ToString(), i)).ToList();

    [Test]
    public async Task BufferInitial()
    {
        var scheduler = new TestScheduler();

        using var cache = new SourceList<Person>();
        using var aggregator = cache.Connect().BufferInitial(TimeSpan.FromSeconds(1), scheduler).AsAggregator();
        foreach (var item in People)
        {
            cache.Add(item);
        }

        await Assert.That(aggregator.Data.Count).IsEqualTo(0);
        await Assert.That(aggregator.Messages.Count).IsEqualTo(0);

        scheduler.Start();

        await Assert.That(aggregator.Data.Count).IsEqualTo(10_000);
        await Assert.That(aggregator.Messages.Count).IsEqualTo(1);

        cache.Add(new Person("_New", 1));

        await Assert.That(aggregator.Data.Count).IsEqualTo(10_001);
        await Assert.That(aggregator.Messages.Count).IsEqualTo(2);
    }
}
