using System;

using DynamicData.Aggregation;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.AggregationTests;

public class MinFixture : IDisposable
{
    private readonly SourceCache<Person, string> _source;

    public MinFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Fact]
    public void AddedItemsContributeToSum()
    {
        var result = 0;

        var accumulator = _source.Connect().Minimum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        result.Should().Be(10, "Min value should be 10");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void InlineChangeReEvaluatesTotals()
    {
        double min = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Minimum(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => min = x);

        var personc = new Person("C", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 11));
        _source.AddOrUpdate(personc);
        min.Should().Be(5);

        _source.AddOrUpdate(personc);

        personc.Age = 11;

        min.Should().Be(10);
        accumulator.Dispose();
    }

    [Fact]
    public void RemoveProduceCorrectResult()
    {
        var result = 0;

        var accumulator = _source.Connect().Minimum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("A");
        result.Should().Be(20, "Min value should be 20 after remove");
        accumulator.Dispose();
    }
}
