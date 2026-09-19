#if REACTIVE_TESTS
using DynamicData.Reactive.Aggregation;
#else
using DynamicData.Aggregation;
#endif

namespace DynamicData.Tests.AggregationTests;

public partial class SumFixture
{
    public class ForList
    {
        [Test]
        [Arguments(1, 10)]
        [Arguments(3, 60)]
        public async Task ItemsAreAdded_SumReflectsAllItems(int itemCount, int expectedSum)
        {
            var items = new[] { 10, 20, 30 };
            using var source = new TestSourceList<int>();

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).IsEmpty();

            // UUT Action
            source.AddRange(items.Take(itemCount));

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(expectedSum).Because($"the sum of the first {itemCount} items should be {expectedSum}");
        }

        [Test]
        [Arguments(0, 50)]
        [Arguments(1, 40)]
        [Arguments(2, 30)]
        public async Task ItemIsRemoved_SumReflectsRemoval(int removalIndex, int expectedSum)
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(60).Because("the sum of items 10 + 20 + 30 is 60");

            // UUT Action
            source.RemoveAt(removalIndex);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).HasCount(2).Because("one additional sum value should have been emitted after the removal");
            await Assert.That(results.RecordedValues[^1]).IsEqualTo(expectedSum).Because($"removing item at index {removalIndex} should leave a sum of {expectedSum}");
        }

        [Test]
        public async Task ItemIsReplaced_SumReflectsReplacement()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(60).Because("the sum of items 10 + 20 + 30 is 60");

            // UUT Action: replace item at index 1 (value 20) with 50
            source.ReplaceAt(1, 50);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).HasCount(2).Because("one additional sum value should have been emitted after the replacement");
            await Assert.That(results.RecordedValues[^1]).IsEqualTo(90).Because("replacing 20 with 50 should change the sum from 60 to 90");
        }

        [Test]
        public async Task ItemsAreCleared_SumReturnsToZero()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(60).Because("the sum of items 10 + 20 + 30 is 60");

            // UUT Action
            source.Clear();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).HasCount(2).Because("one additional sum value should have been emitted after clearing");
            await Assert.That(results.RecordedValues[^1]).IsEqualTo(0).Because("all items were removed so the sum should return to zero");
        }

        [Test]
        public async Task SourceIsEmpty_NoSumEmitted()
        {
            using var source = new TestSourceList<int>();

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).IsEmpty();
        }

        [Test]
        public async Task SourceCompletesAfterEmitting_CompletionPropagates()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).HasSingleItem();

            // UUT Action
            source.Complete();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsTrue();
        }

        [Test]
        public async Task SourceCompletesWithoutEmitting_CompletionPropagates()
        {
            using var source = new TestSourceList<int>();

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues).IsEmpty();

            // UUT Action
            source.Complete();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsTrue();
            await Assert.That(results.RecordedValues).IsEmpty();
        }

        [Test]
        public async Task SourceCompletesImmediately_InitialSumAndCompletionPropagate()
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsTrue();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(60).Because("the sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task SourceCompletesImmediatelyWithoutEmitting_CompletionPropagates()
        {
            using var source = new TestSourceList<int>();

            source.Complete();

            // UUT Construction: source is already completed, with no items.
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsTrue();
            await Assert.That(results.RecordedValues).IsEmpty();
        }

        [Test]
        public async Task SourceErrorsAfterEmitting_ErrorPropagates()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(x => x)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.RecordedValues).HasSingleItem();

            // UUT Action
            var error = new Exception("Test error");
            source.SetError(error);

            await Assert.That(results.Error).IsSameReferenceAs(error).Because("the error from the source should propagate to the subscriber");
            await Assert.That(results.HasCompleted).IsFalse();
        }

        [Test]
        public async Task SourceFailsImmediately_ErrorPropagates()
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

            await Assert.That(results.Error).IsSameReferenceAs(error).Because("the error from the source should propagate to the subscriber immediately upon subscription");
            await Assert.That(results.HasCompleted).IsFalse();
        }

        [Test]
        [Arguments(new[] { 10, 20, 30 }, 60)]
        [Arguments(new[] { int.MaxValue }, int.MaxValue)]
        [Arguments(new[] { int.MinValue }, int.MinValue)]
        [Arguments(new[] { int.MaxValue, -1 }, int.MaxValue - 1)]
        [Arguments(new[] { int.MinValue, 1 }, int.MinValue + 1)]
        public async Task ItemsAreAdded_SumIsCorrect_ForInt(int[] values, int expectedSum)
        {
            using var source = new TestSourceList<int>();

            source.AddRange(values);

            using var subscription = source.Connect()
                .Sum(x => x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(expectedSum).Because($"the int sum of [{string.Join(", ", values)}] is {expectedSum}");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableInt()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (int?)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60).Because("the nullable int sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForLong()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (long)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60L).Because("the long sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableLong()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (long?)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60L).Because("the nullable long sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForDouble()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (double)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60.0).Because("the double sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableDouble()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (double?)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60.0).Because("the nullable double sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForDecimal()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (decimal)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60M).Because("the decimal sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableDecimal()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (decimal?)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60M).Because("the nullable decimal sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForFloat()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (float)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60F).Because("the float sum of items 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableFloat()
        {
            using var source = new TestSourceList<int>();

            source.AddRange(new[] { 10, 20, 30 });

            using var subscription = source.Connect()
                .Sum(x => (float?)x)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60F).Because("the nullable float sum of items 10 + 20 + 30 is 60");
        }
    }
}
