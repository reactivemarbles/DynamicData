using DynamicData.Aggregation;

namespace DynamicData.Tests.AggregationTests;

public partial class AvgFixture
{
    public partial class ForList
    {
        // ForAggregateChangeSet covers the arithmetic both overloads share; these tests cover refresh-aware state.
        public class ForChangeSet
        {
            [Fact]
            public void ItemsAreCleared_EmptyValueIsEmitted()
            {
                using var source = CreatePopulatedSource();

                using var subscription = source.Connect()
                    .Avg(value => value, emptyValue: -1)
                    .RecordValues(out var results);

                source.Clear();

                results.RecordedValues.Should().Equal(20.0, -1.0);
            }

            [Fact]
            public void MovedItemIsRefreshed_AverageReevaluatesMutatedValue()
            {
                var randomizer = new Randomizer(0x4176_674C);
                var first = randomizer.Int(1, 100);
                var second = randomizer.Int(1, 100);
                var mutated = randomizer.Int(1, 100);
                using var source = new TestSourceList<Person>();
                var person = new Person("A", first);
                source.AddRange(new[] { person, new Person("B", second) });
                using var subscription = source.Connect().Avg(item => item.Age).ValidateSynchronization().RecordValues(out var results);

                source.Move(0, 1);
                person.Age = mutated;
                source.Refresh(1);

                results.Error.Should().BeNull();
                results.RecordedValues[^1].Should().Be((mutated + second) / 2.0, "a refresh re-evaluates the selector at the item's moved index");
            }

            [Fact]
            public void ItemMutatedWithoutRefreshIsReplaced_StoredValueIsExcluded()
            {
                var randomizer = new Randomizer(0x4176_6752);
                var first = randomizer.Int(1, 100);
                var second = randomizer.Int(1, 100);
                var replacement = randomizer.Int(1, 100);
                using var source = new TestSourceList<Person>();
                var person = new Person("A", first);
                source.AddRange(new[] { person, new Person("B", second) });
                using var subscription = source.Connect().Avg(item => item.Age).ValidateSynchronization().RecordValues(out var results);

                person.Age = first + randomizer.Int(1, 100);
                source.ReplaceAt(0, new Person("C", replacement));

                results.Error.Should().BeNull();
                results.RecordedValues.Should().Equal(new[] { (first + second) / 2.0, (replacement + second) / 2.0 }, "the replacement excludes the value that was included, not the mutated one");
            }

            private static TestSourceList<int> CreatePopulatedSource()
            {
                var source = new TestSourceList<int>();
                source.AddRange(new[] { 10, 20, 30 });
                return source;
            }
        }
    }
}
