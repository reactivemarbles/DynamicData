using System;

using DynamicData.Aggregation;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.AggregationTests;

public partial class SumFixture
{
    public class ForCache
    {
        [Theory]
        [InlineData(1, 10)]
        [InlineData(3, 60)]
        public void ItemsAreAdded_SumReflectsAllItems(int itemCount, int expectedSum)
        {
            var ages = new[] { 10, 20, 30 };
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().BeEmpty("no items have been added to the source");

            // UUT Action
            for (var i = 0; i < itemCount; i++)
            {
                source.AddOrUpdate(new Person(((char)('A' + i)).ToString(), ages[i]));
            }

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().HaveCount(itemCount, "each AddOrUpdate should produce a new sum emission");
            results.RecordedValues[^1].Should().Be(expectedSum, $"the sum of the first {itemCount} ages should be {expectedSum}");
        }

        [Theory]
        [InlineData("A", 50)]
        [InlineData("B", 40)]
        [InlineData("C", 30)]
        public void ItemIsRemoved_SumReflectsRemoval(string keyToRemove, int expectedSum)
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items")
                .Which.Should().Be(60, "the sum of ages 10 + 20 + 30 is 60");

            // UUT Action
            source.Remove(keyToRemove);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().HaveCount(2, "one additional sum value should have been emitted after the removal");
            results.RecordedValues[^1].Should().Be(expectedSum, $"removing '{keyToRemove}' should leave a sum of {expectedSum}");
        }

        [Fact]
        public void ItemIsUpdated_SumReflectsNewValue()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items")
                .Which.Should().Be(30, "the sum of ages 10 + 20 is 30");

            // UUT Action: update "B" from age 20 to age 50 (same key, new value)
            source.AddOrUpdate(new Person("B", 50));

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().HaveCount(2, "one additional sum value should have been emitted after the update");
            results.RecordedValues[^1].Should().Be(60, "updating 'B' from 20 to 50 should change the sum from 30 to 60");
        }

        [Fact]
        public void MultipleChangesInBatch_SingleSumEmitted()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.RecordedValues.Should().BeEmpty("no items have been added to the source");

            // UUT Action: add 3 items in a single batch
            source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("A", 10));
                updater.AddOrUpdate(new Person("B", 20));
                updater.AddOrUpdate(new Person("C", 30));
            });

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().ContainSingle("a batched edit should produce exactly one sum emission")
                .Which.Should().Be(60, "the sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void SourceIsEmpty_NoSumEmitted()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().BeEmpty("no items were added so no sum values should have been emitted");
        }

        [Fact]
        public void AllItemsRemoved_SumReturnsToZero()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items")
                .Which.Should().Be(60, "the sum of ages 10 + 20 + 30 is 60");

            // UUT Action: remove all items in a single batch
            source.Edit(updater => updater.Clear());

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().HaveCount(2, "one additional sum value should have been emitted after clearing");
            results.RecordedValues[^1].Should().Be(0, "all items were removed so the sum should return to zero");
        }

        [Fact]
        public void SourceCompletesAfterEmitting_CompletionPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().ContainSingle("one changeset was published containing the pre-existing item")
                .Which.Should().Be(10, "the sum of a single age of 10 is 10");

            // UUT Action
            source.Complete();

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeTrue("the source has completed");
        }

        [Fact]
        public void SourceCompletesWithoutEmitting_CompletionPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.RecordedValues.Should().BeEmpty("no items were added to the source");

            // UUT Action
            source.Complete();

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeTrue("the source has completed");
            results.RecordedValues.Should().BeEmpty("no items were added so no sum values should have been emitted");
        }

        [Fact]
        public void SourceCompletesImmediately_InitialSumAndCompletionPropagate()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));
            source.Complete();

            // UUT Construction: source is already completed, with pre-existing items.
            // Subscription should produce both an initial sum and a completion, synchronously.
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeTrue("the source was already completed at the time of subscription");
            results.RecordedValues.Should().ContainSingle("an initial sum value should still be emitted, even when the source completes immediately upon subscription")
                .Which.Should().Be(60, "the sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void SourceCompletesImmediatelyWithoutEmitting_CompletionPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.Complete();

            // UUT Construction: source is already completed, with no items.
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeTrue("the source was already completed at the time of subscription");
            results.RecordedValues.Should().BeEmpty("no items were added so no sum values should have been emitted");
        }

        [Fact]
        public void SourceErrorsAfterEmitting_ErrorPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.RecordedValues.Should().ContainSingle("one changeset was published containing the pre-existing item");

            // UUT Action
            var error = new Exception("Test error");
            source.SetError(error);

            results.Error.Should().BeSameAs(error, "the error from the source should propagate to the subscriber");
            results.HasCompleted.Should().BeFalse("an error is not a completion");
        }

        [Fact]
        public void SourceErrorsWithoutEmitting_ErrorPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.RecordedValues.Should().BeEmpty("no items were added to the source");

            // UUT Action
            var error = new Exception("Test error");
            source.SetError(error);

            results.Error.Should().BeSameAs(error, "the error from the source should propagate to the subscriber");
            results.HasCompleted.Should().BeFalse("an error is not a completion");
            results.RecordedValues.Should().BeEmpty("no items were added so no sum values should have been emitted");
        }

        [Fact]
        public void SourceFailsImmediately_ErrorPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            var error = new Exception("Test error");
            source.SetError(error);

            // UUT Construction: source is already in error state.
            // The error should propagate synchronously upon subscription.
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeSameAs(error, "the error from the source should propagate to the subscriber immediately upon subscription");
            results.HasCompleted.Should().BeFalse("an error is not a completion");
        }

        [Fact]
        public void NullableValuesAreTreatedAsZero()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", new int?(10), "F", null));
            source.AddOrUpdate(new Person("B", null, "F", null));
            source.AddOrUpdate(new Person("C", new int?(30), "F", null));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.AgeNullable)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items")
                .Which.Should().Be(40, "null values should be treated as zero, so the sum should be 10 + 0 + 30 = 40");
        }

        [Theory]
        [InlineData(new[] { 10, 20, 30 }, 60)]
        [InlineData(new[] { int.MaxValue }, int.MaxValue)]
        [InlineData(new[] { int.MinValue }, int.MinValue)]
        [InlineData(new[] { int.MaxValue, -1 }, int.MaxValue - 1)]
        [InlineData(new[] { int.MinValue, 1 }, int.MinValue + 1)]
        public void ItemsAreAdded_SumIsCorrect_ForInt(int[] ages, int expectedSum)
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            for (var i = 0; i < ages.Length; i++)
            {
                source.AddOrUpdate(new Person(((char)('A' + i)).ToString(), ages[i]));
            }

            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(expectedSum, $"the int sum of [{string.Join(", ", ages)}] is {expectedSum}");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableInt()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", new int?(10), "F", null));
            source.AddOrUpdate(new Person("B", new int?(20), "F", null));
            source.AddOrUpdate(new Person("C", new int?(30), "F", null));

            using var subscription = source.Connect()
                .Sum(p => p.AgeNullable)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60, "the nullable int sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForLong()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (long)p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60L, "the long sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableLong()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (long?)p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60L, "the nullable long sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForDouble()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (double)p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60.0, "the double sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableDouble()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (double?)p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60.0, "the nullable double sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForDecimal()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (decimal)p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60M, "the decimal sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableDecimal()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (decimal?)p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60M, "the nullable decimal sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForFloat()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (float)p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60F, "the float sum of ages 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableFloat()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (float?)p.Age)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60F, "the nullable float sum of ages 10 + 20 + 30 is 60");
        }
    }
}
