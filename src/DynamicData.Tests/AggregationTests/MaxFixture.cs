using System;

using DynamicData.Aggregation;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.AggregationTests;

public class MaxFixture : IDisposable
{
    private readonly SourceCache<Person, string> _source;

    public MaxFixture() => _source = new SourceCache<Person, string>(p => p.Name);

    [Fact]
    public void AddItems()
    {
        var result = 0;

        var accumulator = _source.Connect().Maximum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        result.Should().Be(30, "Max value should be 30");

        accumulator.Dispose();
    }

    public void Dispose() => _source.Dispose();

    [Fact]
    public void InlineChangeReEvaluatesTotals()
    {
        double max = 0;

        var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

        var accumulator = _source.Connect().Maximum(p => p.Age).InvalidateWhen(somepropChanged).Subscribe(x => max = x);

        var personc = new Person("C", 5);
        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 11));
        _source.AddOrUpdate(personc);

        max.Should().Be(11, "Max should be 11");

        personc.Age = 100;

        max.Should().Be(100, "Max should be 100 after inline change");
        accumulator.Dispose();
    }

    [Fact]
    public void RemoveItems()
    {
        var result = 0;

        var accumulator = _source.Connect().Maximum(p => p.Age).Subscribe(x => result = x);

        _source.AddOrUpdate(new Person("A", 10));
        _source.AddOrUpdate(new Person("B", 20));
        _source.AddOrUpdate(new Person("C", 30));

        _source.Remove("C");
        result.Should().Be(20, "Max value should be 20 after remove");
        accumulator.Dispose();
    }
}
