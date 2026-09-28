using Person = DynamicData.Tests.Domain.Person;

namespace DynamicData.Tests.AggregationTests;

public partial class AvgFixture
{
    public partial class ForCache
    {
        // ForAggregateChangeSet contains the shared behavioral coverage.
        public class ForChangeSet
        {
            [Fact]
            public void ChangeSetOverload_ForwardsToAggregateOverload()
            {
                using var source = CreatePopulatedSource();

                using var subscription = source.Connect()
                    .Avg(person => person.Age, emptyValue: -1)
                    .RecordValues(out var results);

                source.Edit(updater => updater.Clear());

                results.RecordedValues.Should().Equal(20.0, -1.0);
            }

            private static TestSourceCache<Person, string> CreatePopulatedSource()
            {
                var source = new TestSourceCache<Person, string>(person => person.Name);
                source.Edit(updater =>
                {
                    updater.AddOrUpdate(new Person("A", 10));
                    updater.AddOrUpdate(new Person("B", 20));
                    updater.AddOrUpdate(new Person("C", 30));
                });
                return source;
            }
        }
    }
}
