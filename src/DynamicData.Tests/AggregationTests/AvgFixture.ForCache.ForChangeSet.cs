using DynamicData.Aggregation;

namespace DynamicData.Tests.AggregationTests;

public partial class AvgFixture
{
    public partial class ForCache
    {
        // ForAggregateChangeSet covers the arithmetic both overloads share; these tests cover refresh-aware state.
        public class ForChangeSet
        {
            [Fact]
            public void ItemsAreCleared_EmptyValueIsEmitted()
            {
                using var source = CreatePopulatedSource();

                using var subscription = source.Connect()
                    .Avg(person => person.Age, emptyValue: -1)
                    .RecordValues(out var results);

                source.Edit(updater => updater.Clear());

                results.RecordedValues.Should().Equal(20.0, -1.0);
            }

            [Fact]
            public void ItemIsRefreshed_AverageReevaluatesMutatedValue()
            {
                var randomizer = new Randomizer(0x4176_6743);
                var first = randomizer.Int(1, 100);
                var second = randomizer.Int(1, 100);
                var mutated = randomizer.Int(1, 100);
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                var person = new Person("A", first);
                source.AddOrUpdate(new[] { person, new Person("B", second) });
                using var subscription = source.Connect().Avg(item => item.Age).ValidateSynchronization().RecordValues(out var results);

                person.Age = mutated;
                source.Refresh(person);

                results.Error.Should().BeNull();
                results.RecordedValues.Should().Equal(new[] { (first + second) / 2.0, (mutated + second) / 2.0 }, "a refresh re-evaluates the selector");
            }

            [Fact]
            public void ItemMutatedWithoutRefreshIsRemoved_StoredValueIsExcluded()
            {
                var randomizer = new Randomizer(0x4176_6752);
                var first = randomizer.Int(1, 100);
                var second = randomizer.Int(1, 100);
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                var person = new Person("A", first);
                source.AddOrUpdate(new[] { person, new Person("B", second) });
                using var subscription = source.Connect().Avg(item => item.Age).ValidateSynchronization().RecordValues(out var results);

                person.Age = first + randomizer.Int(1, 100);
                source.Remove(person);

                results.Error.Should().BeNull();
                results.RecordedValues.Should().Equal(new[] { (first + second) / 2.0, second }, "the removal excludes the value that was included, not the mutated one");
            }

            [Fact]
            public void NullableSelectorItemIsRefreshed_AverageReevaluatesMutatedValue()
            {
                var randomizer = new Randomizer(0x4176_674E);
                var value = randomizer.Int(1, 100);
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                var person = new Person("A", (int?)null);
                source.AddOrUpdate(new[] { person, new Person("B", (int?)value) });
                using var subscription = source.Connect().Avg(item => item.AgeNullable).ValidateSynchronization().RecordValues(out var results);

                person.AgeNullable = value;
                source.Refresh(person);

                results.Error.Should().BeNull();
                results.RecordedValues.Should().Equal(new[] { value / 2.0, value }, "a null value counts as zero until a refresh supplies a value");
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
