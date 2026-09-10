using System;

using DynamicData.Aggregation;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.AggregationTests;

public partial class AvgFixture
{
    public class ForCache
    {
        // Behavioral coverage lives on the inner aggregate overload; ForChangeSet checks forwarding.
        public class ForAggregateChangeSet
        {
            [Theory]
            [InlineData(1, 10.0)]
            [InlineData(2, 15.0)]
            [InlineData(3, 20.0)]
            public void ItemsAreAdded_AverageReflectsAllItems(int itemCount, double expectedAverage)
            {
                var ages = new[] { 10, 20, 30 };
                using var source = new TestSourceCache<Person, string>(person => person.Name);

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                results.Error.Should().BeNull();
                results.HasCompleted.Should().BeFalse();
                // Initial-empty behavior is covered separately by the known-defect test.
                var initialValueCount = results.RecordedValues.Count;

                for (var i = 0; i < itemCount; ++i)
                {
                    source.AddOrUpdate(new Person(((char)('A' + i)).ToString(), ages[i]));
                }

                results.Error.Should().BeNull();
                results.HasCompleted.Should().BeFalse();
                results.RecordedValues.Should().HaveCount(initialValueCount + itemCount, "each edit should produce one average");
                results.RecordedValues[^1].Should().Be(expectedAverage);
            }

            [Theory]
            [InlineData("A", 25.0)]
            [InlineData("B", 20.0)]
            [InlineData("C", 15.0)]
            public void ItemIsRemoved_AverageReflectsRemoval(string key, double expectedAverage)
            {
                using var source = CreatePopulatedSource();

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                results.RecordedValues.Should().ContainSingle().Which.Should().Be(20.0);

                source.Remove(key);

                results.Error.Should().BeNull();
                results.HasCompleted.Should().BeFalse();
                results.RecordedValues.Should().Equal(20.0, expectedAverage);
            }

            [Fact]
            public void ItemIsUpdated_AverageReflectsReplacement()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                source.AddOrUpdate(new Person("A", 10));
                source.AddOrUpdate(new Person("B", 20));

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                source.AddOrUpdate(new Person("B", 50));

                results.Error.Should().BeNull();
                results.RecordedValues.Should().Equal(15.0, 30.0);
            }

            [Fact]
            public void MultipleChangesInBatch_SingleAverageIsEmitted()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                var initialValueCount = results.RecordedValues.Count;
                source.Edit(updater =>
                {
                    updater.AddOrUpdate(new Person("A", 10));
                    updater.AddOrUpdate(new Person("B", 20));
                    updater.AddOrUpdate(new Person("C", 30));
                });

                results.Error.Should().BeNull();
                results.RecordedValues.Should().HaveCount(initialValueCount + 1, "one change set should produce one average");
                results.RecordedValues[^1].Should().Be(20.0);
            }

            [Fact(Skip = "Existing defect: Avg does not emit emptyValue when the source is initially empty.")]
            public void SourceIsEmpty_ConfiguredEmptyValueIsEmitted()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age, emptyValue: -1)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                results.Error.Should().BeNull();
                results.HasCompleted.Should().BeFalse();
                results.RecordedValues.Should().ContainSingle().Which.Should().Be(-1.0);
            }

            [Fact]
            public void AllItemsAreRemoved_ConfiguredEmptyValueIsEmitted()
            {
                using var source = CreatePopulatedSource();

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age, emptyValue: -1)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                source.Edit(updater => updater.Clear());

                results.Error.Should().BeNull();
                results.RecordedValues.Should().Equal(20.0, -1.0);
            }

            // Legacy behavior: nullable projections are coalesced to zero and remain in the denominator.
            [Fact]
            public void LegacyNullableValuesAreCountedAsZero()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                source.AddOrUpdate(new Person("A", new int?(10)));
                source.AddOrUpdate(new Person("B", null));
                source.AddOrUpdate(new Person("C", new int?(20)));

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.AgeNullable, emptyValue: -1)
                    .RecordValues(out var results);

                results.RecordedValues.Should().ContainSingle().Which.Should().Be(10.0,
                    "null contributes zero while the item remains in the denominator");
            }

            // Legacy behavior: null coalescing is currently part of the public operator behavior.
            [Fact]
            public void LegacyAllValuesAreNull_ZeroIsEmittedInsteadOfEmptyValue()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                source.AddOrUpdate(new Person("A", null));
                source.AddOrUpdate(new Person("B", null));

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.AgeNullable, emptyValue: -1)
                    .RecordValues(out var results);

                results.RecordedValues.Should().ContainSingle().Which.Should().Be(0.0,
                    "a non-empty collection of null projections is not empty");
            }

            [Fact]
            public void NumericOverloadsProduceExpectedAverages()
            {
                using var source = CreatePopulatedSource();
                var changes = source.Connect().ForAggregation();

                using var intSubscription = changes.Avg(person => person.Age, emptyValue: -1).RecordValues(out var ints);
                using var longSubscription = changes.Avg(person => (long)person.Age, emptyValue: -2L).RecordValues(out var longs);
                using var doubleSubscription = changes.Avg(person => (double)person.Age, emptyValue: -3.0).RecordValues(out var doubles);
                using var decimalSubscription = changes.Avg(person => (decimal)person.Age, emptyValue: -4M).RecordValues(out var decimals);
                using var floatSubscription = changes.Avg(person => (float)person.Age, emptyValue: -5F).RecordValues(out var floats);

                source.Edit(updater => updater.Clear());

                ints.RecordedValues.Should().Equal(20.0, -1.0);
                longs.RecordedValues.Should().Equal(20.0, -2.0);
                doubles.RecordedValues.Should().Equal(20.0, -3.0);
                decimals.RecordedValues.Should().Equal(20M, -4M);
                floats.RecordedValues.Should().Equal(20F, -5F);
            }

            [Fact]
            public void LegacyNullableNumericOverloadsProduceExpectedAverages()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                source.AddOrUpdate(new Person("A", new int?(10)));
                source.AddOrUpdate(new Person("B", null));
                var changes = source.Connect().ForAggregation();

                using var intSubscription = changes.Avg(person => person.AgeNullable, emptyValue: -1).RecordValues(out var ints);
                using var longSubscription = changes.Avg(person => (long?)person.AgeNullable, emptyValue: -2L).RecordValues(out var longs);
                using var doubleSubscription = changes.Avg(person => (double?)person.AgeNullable, emptyValue: -3.0).RecordValues(out var doubles);
                using var decimalSubscription = changes.Avg(person => (decimal?)person.AgeNullable, emptyValue: -4M).RecordValues(out var decimals);
                using var floatSubscription = changes.Avg(person => (float?)person.AgeNullable, emptyValue: -5F).RecordValues(out var floats);

                source.Edit(updater => updater.Clear());

                ints.RecordedValues.Should().Equal(5.0, -1.0);
                longs.RecordedValues.Should().Equal(5.0, -2.0);
                doubles.RecordedValues.Should().Equal(5.0, -3.0);
                decimals.RecordedValues.Should().Equal(5M, -4M);
                floats.RecordedValues.Should().Equal(5F, -5F);
            }

            [Fact]
            public void IntegerAverageCanBeFractional()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                source.AddOrUpdate(new Person("A", 10));
                source.AddOrUpdate(new Person("B", 11));

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age)
                    .RecordValues(out var results);

                results.RecordedValues.Should().ContainSingle().Which.Should().Be(10.5);
            }

            [Fact(Skip = "Existing defect: the legacy aggregate adapter discards Refresh details.")]
            public void ItemIsRefreshed_AverageReevaluatesMutatedValue()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                var person = new Person("A", 10);
                source.AddOrUpdate(person);

                using var subscription = source.Connect().ForAggregation()
                    .Avg(item => item.Age)
                    .RecordValues(out var results);

                person.Age = 40;
                source.Refresh(person);

                results.RecordedValues.Should().Equal(10.0, 40.0);
            }

            [Theory]
            [InlineData(StreamCompletionStrategy.Asynchronous)]
            [InlineData(StreamCompletionStrategy.Immediate)]
            public void SourceCompletes_CompletionPropagates(StreamCompletionStrategy completionStrategy)
            {
                using var source = CreatePopulatedSource();

                if (completionStrategy is StreamCompletionStrategy.Immediate)
                    source.Complete();

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                if (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    source.Complete();

                results.Error.Should().BeNull();
                results.HasCompleted.Should().BeTrue();
                results.RecordedValues.Should().ContainSingle().Which.Should().Be(20.0);
            }

            [Theory(Skip = "Existing defect: Avg does not emit emptyValue when the source is initially empty.")]
            [InlineData(StreamCompletionStrategy.Asynchronous)]
            [InlineData(StreamCompletionStrategy.Immediate)]
            public void EmptySourceCompletes_ConfiguredEmptyValueAndCompletionPropagate(StreamCompletionStrategy completionStrategy)
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);

                if (completionStrategy is StreamCompletionStrategy.Immediate)
                    source.Complete();

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age, emptyValue: -1)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                if (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    source.Complete();

                results.Error.Should().BeNull();
                results.HasCompleted.Should().BeTrue();
                results.RecordedValues.Should().ContainSingle().Which.Should().Be(-1.0);
            }

            [Theory]
            [InlineData(StreamCompletionStrategy.Asynchronous)]
            [InlineData(StreamCompletionStrategy.Immediate)]
            public void SourceFails_ErrorPropagates(StreamCompletionStrategy completionStrategy)
            {
                using var source = CreatePopulatedSource();
                var error = new Exception("Test error");

                if (completionStrategy is StreamCompletionStrategy.Immediate)
                    source.SetError(error);

                using var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                if (completionStrategy is StreamCompletionStrategy.Asynchronous)
                    source.SetError(error);

                results.Error.Should().BeSameAs(error);
                results.HasCompleted.Should().BeFalse();
                if (completionStrategy is StreamCompletionStrategy.Immediate)
                    results.RecordedValues.Should().BeEmpty();
                else
                    results.RecordedValues.Should().ContainSingle().Which.Should().Be(20.0);
            }

            [Fact]
            public void DisposedSubscriptionReceivesNoFurtherValues()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                var subscription = source.Connect().ForAggregation()
                    .Avg(person => person.Age)
                    .RecordValues(out var results);

                source.AddOrUpdate(new Person("A", 10));
                var valueCountAtDisposal = results.RecordedValues.Count;
                subscription.Dispose();
                source.AddOrUpdate(new Person("B", 20));

                results.RecordedValues.Should().HaveCount(valueCountAtDisposal);
                results.RecordedValues[^1].Should().Be(10.0);
            }

            [Fact]
            public void MultipleSubscriptionsMaintainIndependentState()
            {
                using var source = new TestSourceCache<Person, string>(person => person.Name);
                source.AddOrUpdate(new Person("A", 10));
                var averages = source.Connect().ForAggregation().Avg(person => person.Age);

                using var firstSubscription = averages.RecordValues(out var first);
                using var secondSubscription = averages.RecordValues(out var second);
                source.AddOrUpdate(new Person("B", 20));

                first.RecordedValues.Should().Equal(10.0, 15.0);
                second.RecordedValues.Should().Equal(10.0, 15.0);
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

        // The aggregate fixture above contains the shared behavioral coverage.
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
