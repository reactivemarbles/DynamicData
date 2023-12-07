using System;

using DynamicData.Aggregation;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.AggregationTests;

public class SumFixture : IDisposable
{
    private readonly SourceCache<Person, string> _source;

    public SumFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Fact]
    public void AddedItemsContributeToSum()
    {
        var sum = 0;
        double dev = 0;

        var accumulator = _source.Connect().Sum(p => p.Age).Subscribe(x => sum = x);
        var deviation = _source.Connect().StdDev(p => p.Age, (int)0).Subscribe(x => dev = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        sum.Should().Be(60, "Accumulated value should be 60");
        dev.Should().Be(7.0710678118654755, "");
        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumLong()
    {
        long sum = 0;
        double dev = 0;

        var accumulator = _source.Connect().Sum(p => Convert.ToInt64(p.Age)).Subscribe(x => sum = x);
        var deviation = _source.Connect().StdDev(p => p.Age, (long)0).Subscribe(x => dev = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        sum.Should().Be(60, "Accumulated value should be 60");
        dev.Should().Be(7.0710678118654755, "");
        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumFloat()
    {
        float sum = 0;
        double dev = 0;

        var accumulator = _source.Connect().Sum(p => Convert.ToSingle(p.Age)).Subscribe(x => sum = x);
        var deviation = _source.Connect().StdDev(p => p.Age, (float)0).Subscribe(x => dev = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        sum.Should().Be(60, "Accumulated value should be 60");
        dev.Should().Be(7.0710678118654755, "");
        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumDouble()
    {
        double sum = 0;
        double dev = 0;

        var accumulator = _source.Connect().Sum(p => Convert.ToDouble(p.Age)).Subscribe(x => sum = x);
        var deviation = _source.Connect().StdDev(p => p.Age, (double)0).Subscribe(x => dev = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        sum.Should().Be(60, "Accumulated value should be 60");
        dev.Should().Be(7.0710678118654755, "");
        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumDecimal()
    {
        decimal sum = 0;
        decimal dev = 0;

        var accumulator = _source.Connect().Sum(p => Convert.ToDecimal(p.Age)).Subscribe(x => sum = x);
        var deviation = _source.Connect().StdDev(p => p.Age, (decimal)0).Subscribe(x => dev = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        sum.Should().Be(60, "Accumulated value should be 60");
        dev.Should().Be(7.0710678118654752440084436210M, "");
        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumNullable()
    {
        var sum = 0;

        var accumulator = _source.Connect().Sum(p => p.AgeNullable).Subscribe(x => sum = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        sum.Should().Be(60, "Accumulated value should be 60");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumLongNullable()
    {
        long sum = 0;

        var accumulator = _source.Connect().Sum(p => (long?)(p.AgeNullable.HasValue ? Convert.ToInt64(p.AgeNullable) : default)).Subscribe(x => sum = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        sum.Should().Be(60, "Accumulated value should be 60");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumFloatNullable()
    {
        float sum = 0;

        var accumulator = _source.Connect().Sum(p => (float?)(p.AgeNullable.HasValue ? Convert.ToSingle(p.AgeNullable) : default)).Subscribe(x => sum = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        sum.Should().Be(60, "Accumulated value should be 60");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumDoubleNullable()
    {
        double sum = 0;

        var accumulator = _source.Connect().Sum(p => (double?)(p.AgeNullable.HasValue ? Convert.ToDouble(p.AgeNullable) : default)).Subscribe(x => sum = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        sum.Should().Be(60, "Accumulated value should be 60");

        accumulator.Dispose();
    }

    [Fact]
    public void AddedItemsContributeToSumDecimalNullable()
    {
        decimal sum = 0;

        var accumulator = _source.Connect().Sum(p => (decimal?)(p.AgeNullable.HasValue ? Convert.ToDecimal(p.AgeNullable) : default)).Subscribe(x => sum = x);

        _source.AddOrUpdate(new Person("A", new int?(10), "F", null));
        _source.AddOrUpdate(new Person("B", new int?(20), "F", null));
        _source.AddOrUpdate(new Person("C", new int?(30), "F", null));

        sum.Should().Be(60, "Accumulated value should be 60");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void InlineChangeReEvaluatesTotals()
    {
        var sum = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Sum(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => sum = x);

        var personb = new Person("B", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(personb);
        _source.AddOrUpdate(new Person("C", 30));

        sum.Should().Be(45, "Sum should be 45 after inline change");

        personb.Age = 20;

        sum.Should().Be(60, "Sum should be 60 after inline change");
        accumulator.Dispose();
    }

    [Fact]
    public void RemoveProduceCorrectResult()
    {
        var sum = 0;

        var accumulator = _source.Connect().Sum(p => p.Age).Subscribe(x => sum = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("A");
        sum.Should().Be(50, "Accumulated value should be 50 after remove");
        accumulator.Dispose();
    }
}
