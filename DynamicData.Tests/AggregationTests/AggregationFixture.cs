using System;
using System.Reactive.Linq;
using DynamicData.Aggregation;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using Xunit;


namespace DynamicData.Tests.AggregationTests
{
    
    public class AggregationFixture: IDisposable
    {
        private readonly SourceCache<Person, string> _source;
        private readonly IObservable<int> _accumulator;

        /// <summary>
        /// Initialises this instance.
        /// </summary>
        public AggregationFixture()
        {
            _source = new SourceCache<Person, string>(p => p.Name);

            _accumulator = _source.Connect()
                                  .ForAggregation()
                                  .Scan(0, (current, items) =>
                                  {
                                      items.ForEach(x =>
                                      {
                                          if (x.Type == AggregateType.Add)
                                              current = current + x.Item.Age;
                                          else
                                              current = current - x.Item.Age;
                                      });
                                      return current;
                                  });
        }

        public void Dispose()
        {
            _source.Dispose();
        }

        [Fact]
        public void CanAccumulate()
        {
            int latest = 0;
            int counter = 0;

            var accumulator = _accumulator.Subscribe(value =>
            {
                latest = value;
                counter++;
            });

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("B", 20));
            _source.AddOrUpdate(new Person("C", 30));

            counter.Should().Be(3, "Should be 3 updates");
            latest.Should().Be(60, "Accumulated value should be 60");
            _source.AddOrUpdate(new Person("A", 5));

            accumulator.Dispose();
        }

        [Fact]
        public void CanHandleUpdatedItem()
        {
            int latest = 0;
            int counter = 0;

            var accumulator = _accumulator.Subscribe(value =>
            {
                latest = value;
                counter++;
            });

            _source.AddOrUpdate(new Person("A", 10));
            _source.AddOrUpdate(new Person("A", 15));

            counter.Should().Be(2, "Should be 2 updates");
            latest.Should().Be(15, "Accumulated value should be 60");
            accumulator.Dispose();
        }
    }
}
