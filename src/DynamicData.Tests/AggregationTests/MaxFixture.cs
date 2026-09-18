#if REACTIVE_TESTS
using DynamicData.Reactive.Aggregation;
#else
using DynamicData.Aggregation;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.AggregationTests;

public class MaxFixture : IDisposable
{
    private readonly SourceCache<Person, string> _source;

    public MaxFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Test]
    public async Task AddItems()
    {
        var result = 0;

        var accumulator = _source.Connect().Maximum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(result).IsEqualTo(30).Because("Max value should be 30");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task InlineChangeReEvaluatesTotals()
    {
        double max = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Maximum(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => max = x);

        var personc = new Person("C", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 11));
        _source.AddOrUpdate(personc);

        await Assert.That(max).IsEqualTo(11).Because("Max should be 11");

        personc.Age = 100;

        await Assert.That(max).IsEqualTo(100).Because("Max should be 100 after inline change");
        accumulator.Dispose();
    }

    [Test]
    public async Task RemoveItems()
    {
        var result = 0;

        var accumulator = _source.Connect().Maximum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("C");
        await Assert.That(result).IsEqualTo(20).Because("Max value should be 20 after remove");
        accumulator.Dispose();
    }
}
