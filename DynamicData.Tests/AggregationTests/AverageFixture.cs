using System;
using System.Diagnostics;
using DynamicData.Aggregation;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.AggregationTests
{
    
    public class AverageFixture: IDisposable
    {
        private readonly SourceCache<Person, string> _source;

        public AverageFixture()
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
            double avg = 0;

            var accumulator = _source.Connect()
                                     .Avg(p => p.Age)
                                     .Subscribe(x => avg = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            avg.Should().Be(20, "Average value should be 20");

            accumulator.Dispose();
        }

        [Fact]
        public void RemoveProduceCorrectResult()
        {
            double avg = 0;

            var accumulator = _source.Connect()
                .Avg(p => p.Age)
                .Subscribe(x => avg = x);

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            _source.Remove("A");
            avg.Should().Be(25, "Average value should be 25 after remove");
            accumulator.Dispose();
        }

        [Fact]
        public void InlineChangeReEvaluatesTotals()
        {
            double avg = 0;

            var somepropChanged = _source.Connect().WhenValueChanged(p => p.Age);

            var accumulator = _source.Connect()
                .Avg(p => p.Age)
                .InvalidateWhen(somepropChanged)
                .Subscribe(x => avg = x);

            var personb = new Person("B", 5);
            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(personb);
            _source.AddOrUpdate(new Person("C", 30));

            avg.Should().Be(15, "Sum should be 15 after inline change");

            personb.Age = 20;

            avg.Should().Be(20, "Sum should be 20 after inline change");
            accumulator.Dispose();
        }

    }
}
