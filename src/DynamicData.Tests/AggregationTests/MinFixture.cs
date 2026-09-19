#if REACTIVE_TESTS
using DynamicData.Reactive.Aggregation;
#else
using DynamicData.Aggregation;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.AggregationTests;

public class MinFixture : IDisposable
{
    private readonly SourceCache<Person, string> _source;

    public MinFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Test]
    public async Task AddedItemsContributeToSum()
    {
        var result = 0;

        var accumulator = _source.Connect().Minimum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(result).IsEqualTo(10).Because("Min value should be 10");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task InlineChangeReEvaluatesTotals()
    {
        double min = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Minimum(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => min = x);

        var personc = new Person("C", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 11));
        _source.AddOrUpdate(personc);
        await Assert.That(min).IsEqualTo(5);

        _source.AddOrUpdate(personc);

        personc.Age = 11;

        await Assert.That(min).IsEqualTo(10);
        accumulator.Dispose();
    }

    [Test]
    public async Task RemoveProduceCorrectResult()
    {
        var result = 0;

        var accumulator = _source.Connect().Minimum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("A");
        await Assert.That(result).IsEqualTo(20).Because("Min value should be 20 after remove");
        accumulator.Dispose();
    }
}
