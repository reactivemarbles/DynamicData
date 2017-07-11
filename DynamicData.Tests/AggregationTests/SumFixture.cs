using System;
using System.Diagnostics;
using DynamicData.Aggregation;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.AggregationTests
{
    
    public class SumFixture: IDisposable
    {
        private readonly SourceCache<Person, string> _source;

        public SumFixture()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
        }

        public void Dispose()
        {
            _source.Dispose();
        }

        [Fact]
        public void AddedItemsContributeToSum()
        {
            int sum = 0;

            var accumulator = _source.Connect()
                                     .Sum(p => p.Age)
                                     .Subscribe(x => sum = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            sum.Should().Be(60, "Accumulated value should be 60");

            accumulator.Dispose();
        }

        [Fact]
        public void RemoveProduceCorrectResult()
        {
            int sum = 0;

            var accumulator = _source.Connect()
                .Sum(p => p.Age)
                .Subscribe(x => sum = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            _source.Remove("A");
            sum.Should().Be(50, "Accumulated value should be 50 after remove");
            accumulator.Dispose();
        }

        [Fact]
        public void InlineChangeReEvaluatesTotals()
        {
            int sum = 0;

            var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

            var accumulator = _source.Connect()
                .Sum(p => p.Age)
                .InvalidateWhen(somepropChanged)
                .Subscribe(x => sum = x);

            var personb = new Person("B", 5);
            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(personb);
            _source.AddOrUpdate(new Person("C", 30));

            sum.Should().Be(45, "Sum should be 45 after inline change");

            personb.Age = 20;

            sum.Should().Be(60, "Sum should be 60 after inline change");
            accumulator.Dispose();
        }

    }
}
