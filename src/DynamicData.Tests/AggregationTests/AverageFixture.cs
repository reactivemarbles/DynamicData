using System;

using DynamicData.Aggregation;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.AggregationTests;

public class AverageFixture : IDisposable
{
    private readonly SourceCache<Person, string> _source;

    public AverageFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Fact]
    public void AddedItemsContributeToSum()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => p.Age).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumLong()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => Convert.ToInt64(p.Age)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumFloat()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => Convert.ToSingle(p.Age)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumDouble()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => Convert.ToDouble(p.Age)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumDecimal()
    {
        decimal avg = 0;

        var accumulator = _source.Connect().Avg(p => Convert.ToDecimal(p.Age)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumNullable()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => p.AgeNullable).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumNullableLong()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => (long?)(p.AgeNullable.HasValue ? Convert.ToInt64(p.AgeNullable) : default)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumNullableFloat()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => (float?)(p.AgeNullable.HasValue ? Convert.ToSingle(p.AgeNullable) : default)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumNullableDouble()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => (double?)(p.AgeNullable.HasValue ? Convert.ToDouble(p.AgeNullable) : default)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumNullableDecimal()
    {
        decimal avg = 0;

        var accumulator = _source.Connect().Avg(p => (decimal?)(p.AgeNullable.HasValue ? Convert.ToDecimal(p.AgeNullable) : default)).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        avg.Should().Be(20, "Average value should be 20");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void InlineChangeReEvaluatesTotals()
    {
        double avg = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Avg(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => avg = x);

        var personb = new Person("B", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(personb);
        _source.AddOrUpdate(new Person("C", 30));

        avg.Should().Be(15, "Sum should be 15 after inline change");

        personb.Age = 20;

        avg.Should().Be(20, "Sum should be 20 after inline change");
        accumulator.Dispose();
    }

    [Fact]
    public void RemoveProduceCorrectResult()
    {
        double avg = 0;

        var accumulator = _source.Connect().Avg(p => p.Age).Subscribe(x => avg = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("A");
        avg.Should().Be(25, "Average value should be 25 after remove");
        accumulator.Dispose();
    }
}
