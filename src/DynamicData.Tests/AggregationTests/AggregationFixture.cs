#if REACTIVE_TESTS
using DynamicData.Reactive.Aggregation;
#else
using DynamicData.Aggregation;
#endif
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.AggregationTests;

public class AggregationFixture : IDisposable
{
    private readonly IObservable<int> _accumulator;

    private readonly SourceCache<Person, string> _source;

    /// <summary>
    /// Initialises this instance.
    /// </summary>
    public AggregationFixture()
    {
        _source = new SourceCache<Person, string>(p => p.Name);

        _accumulator = _source.Connect().ForAggregation().Scan(
            0,
            (current, items) =>
            {
                items.ForEach(
                    x =>
                    {
                        if (x.Type == AggregateType.Add)
                        {
                            current += x.Item.Age;
                        }
                        else
                        {
                            current -= x.Item.Age;
                        }
                    });
                return current;
            });
    }

    [Test]
    public async Task CanAccumulate()
    {
        var latest = 0;
        var counter = 0;

        var accumulator = _accumulator.Subscribe(
            value =>
            {
                latest = value;
                counter++;
            });

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(counter).IsEqualTo(3).Because("Should be 3 updates");
        await Assert.That(latest).IsEqualTo(60).Because("Accumulated value should be 60");
        _source.AddOrUpdate(new Person("A", 5));

        accumulator.Dispose();
    }

    [Test]
    public async Task CanHandleUpdatedItem()
    {
        var latest = 0;
        var counter = 0;

        var accumulator = _accumulator.Subscribe(
            value =>
            {
                latest = value;
                counter++;
            });

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("A", 15));

        await Assert.That(counter).IsEqualTo(2).Because("Should be 2 updates");
        await Assert.That(latest).IsEqualTo(15).Because("Accumulated value should be 60");
        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();
}
