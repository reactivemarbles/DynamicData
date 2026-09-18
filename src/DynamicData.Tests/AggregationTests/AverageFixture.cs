#if REACTIVE_TESTS
using DynamicData.Reactive.Aggregation;
#else
using DynamicData.Aggregation;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.AggregationTests;

public class AverageFixture : IDisposable
{
    private readonly SourceCache<Person, string> _source;

    public AverageFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Test]
    public async Task AddedItemsContributeToSum()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => p.Age).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumLong()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => Convert.ToInt64(p.Age)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumFloat()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => Convert.ToSingle(p.Age)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumDouble()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => Convert.ToDouble(p.Age)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumDecimal()
    {
        decimal avg = 0;

        var accumulator = _source.Connect().Avg(p => Convert.ToDecimal(p.Age)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumNullable()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => p.AgeNullable).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumNullableLong()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => (long?)(p.AgeNullable.HasValue ? Convert.ToInt64(p.AgeNullable) : default)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumNullableFloat()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => (float?)(p.AgeNullable.HasValue ? Convert.ToSingle(p.AgeNullable) : default)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumNullableDouble()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => (double?)(p.AgeNullable.HasValue ? Convert.ToDouble(p.AgeNullable) : default)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    [Test]
    public async Task AddedItemsContributeToSumNullableDecimal()
    {
        decimal avg = 0;

        var accumulator = _source.Connect().Avg(p => (decimal?)(p.AgeNullable.HasValue ? Convert.ToDecimal(p.AgeNullable) : default)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        await Assert.That(avg).IsEqualTo(20).Because("Average value should be 20");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Test]
    public async Task InlineChangeReEvaluatesTotals()
    {
        double avg = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Avg(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => avg = x);

        var personb = new Person("B", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(personb);
        _source.AddOrUpdate(new Person("C", 30));

        await Assert.That(avg).IsEqualTo(15).Because("Sum should be 15 after inline change");

        personb.Age = 20;

        await Assert.That(avg).IsEqualTo(20).Because("Sum should be 20 after inline change");
        accumulator.Dispose();
    }

    [Test]
    public async Task RemoveProduceCorrectResult()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => p.Age).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("A");
        await Assert.That(avg).IsEqualTo(25).Because("Average value should be 25 after remove");
        accumulator.Dispose();
    }
}
