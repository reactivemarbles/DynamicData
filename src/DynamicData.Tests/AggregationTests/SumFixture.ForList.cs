using System;
using System.Linq;

using DynamicData.Aggregation;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.AggregationTests;

public partial class SumFixture
{
    public class ForList
    {
        [Theory]
        [InlineData(1, 10)]
        [InlineData(3, 60)]
        public void ItemsAreAdded_SumReflectsAllItems(int itemCount, int expectedSum)
        {
            var items = new[] { 10, 20, 30 };
            using var source = new TestSourceList<int>();

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().BeEmpty("no items have been added to the source");

            // UUT Action
            source.AddRange(items.Take(itemCount));

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().ContainSingle("an AddRange produces a single changeset")
                .Which.Should().Be(expectedSum, $"the sum of the first {itemCount} items should be {expectedSum}");
        }

        [Theory]
        [InlineData(0, 50)]
        [InlineData(1, 40)]
        [InlineData(2, 30)]
        public void ItemIsRemoved_SumReflectsRemoval(int removalIndex, int expectedSum)
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items")
                .Which.Should().Be(60, "the sum of items 10 + 20 + 30 is 60");

            // UUT Action
            source.RemoveAt(removalIndex);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().HaveCount(2, "one additional sum value should have been emitted after the removal");
            results.RecordedValues[^1].Should().Be(expectedSum, $"removing item at index {removalIndex} should leave a sum of {expectedSum}");
        }

        [Fact]
        public void ItemIsReplaced_SumReflectsReplacement()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items")
                .Which.Should().Be(60, "the sum of items 10 + 20 + 30 is 60");

            // UUT Action: replace item at index 1 (value 20) with 50
            source.ReplaceAt(1, 50);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().HaveCount(2, "one additional sum value should have been emitted after the replacement");
            results.RecordedValues[^1].Should().Be(90, "replacing 20 with 50 should change the sum from 60 to 90");
        }

        [Fact]
        public void ItemsAreCleared_SumReturnsToZero()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items")
                .Which.Should().Be(60, "the sum of items 10 + 20 + 30 is 60");

            // UUT Action
            source.Clear();

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().HaveCount(2, "one additional sum value should have been emitted after clearing");
            results.RecordedValues[^1].Should().Be(0, "all items were removed so the sum should return to zero");
        }

        [Fact]
        public void SourceIsEmpty_NoSumEmitted()
        {
            using var source = new TestSourceList<int>();

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().BeEmpty("no items were added so no sum values should have been emitted");
        }

        [Fact]
        public void SourceCompletesAfterEmitting_CompletionPropagates()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeFalse("the source can still publish notifications");
            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items");

            // UUT Action
            source.Complete();

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeTrue("the source has completed");
        }

        [Fact]
        public void SourceCompletesWithoutEmitting_CompletionPropagates()
        {
            using var source = new TestSourceList<int>();

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
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
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });
            source.Complete();

            // UUT Construction: source is already completed, with pre-existing items.
            // Subscription should produce both an initial sum and a completion, synchronously.
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeTrue("the source was already completed at the time of subscription");
            results.RecordedValues.Should().ContainSingle("an initial sum value should still be emitted, even when the source completes immediately upon subscription")
                .Which.Should().Be(60, "the sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void SourceCompletesImmediatelyWithoutEmitting_CompletionPropagates()
        {
            using var source = new TestSourceList<int>();

            source.Complete();

            // UUT Construction: source is already completed, with no items.
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.HasCompleted.Should().BeTrue("the source was already completed at the time of subscription");
            results.RecordedValues.Should().BeEmpty("no items were added so no sum values should have been emitted");
        }

        [Fact]
        public void SourceErrorsAfterEmitting_ErrorPropagates()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeNull("no errors should have occurred");
            results.RecordedValues.Should().ContainSingle("one changeset was published containing all pre-existing items");

            // UUT Action
            var error = new Exception("Test error");
            source.SetError(error);

            results.Error.Should().BeSameAs(error, "the error from the source should propagate to the subscriber");
            results.HasCompleted.Should().BeFalse("an error is not a completion");
        }

        [Fact]
        public void SourceFailsImmediately_ErrorPropagates()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });
            var error = new Exception("Test error");
            source.SetError(error);

            // UUT Construction: source is already in error state.
            // The error should propagate synchronously upon subscription.
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            results.Error.Should().BeSameAs(error, "the error from the source should propagate to the subscriber immediately upon subscription");
            results.HasCompleted.Should().BeFalse("an error is not a completion");
        }

        [Theory]
        [InlineData(new[] { 10, 20, 30 }, 60)]
        [InlineData(new[] { int.MaxValue }, int.MaxValue)]
        [InlineData(new[] { int.MinValue }, int.MinValue)]
        [InlineData(new[] { int.MaxValue, -1 }, int.MaxValue - 1)]
        [InlineData(new[] { int.MinValue, 1 }, int.MinValue + 1)]
        public void ItemsAreAdded_SumIsCorrect_ForInt(int[] values, int expectedSum)
        {
            using var source = new TestSourceList<int>();

            source.AddRange(values);

            using var subscription = source.Connect()
                .Sum(x => x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(expectedSum, $"the int sum of [{string.Join(", ", values)}] is {expectedSum}");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableInt()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (int?)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60, "the nullable int sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForLong()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (long)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60L, "the long sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableLong()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (long?)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60L, "the nullable long sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForDouble()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (double)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60.0, "the double sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableDouble()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (double?)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60.0, "the nullable double sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForDecimal()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (decimal)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60M, "the decimal sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableDecimal()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (decimal?)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60M, "the nullable decimal sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForFloat()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (float)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60F, "the float sum of items 10 + 20 + 30 is 60");
        }

        [Fact]
        public void ItemsAreAdded_SumIsCorrect_ForNullableFloat()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (float?)x)
                .RecordValues(out var results);

            results.RecordedValues[^1].Should().Be(60F, "the nullable float sum of items 10 + 20 + 30 is 60");
        }
    }
}
