#if REACTIVE_TESTS
using DynamicData.Reactive.Aggregation;
#else
using DynamicData.Aggregation;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.AggregationTests;

public partial class SumFixture
{
    public class ForCache
    {
        [Test]
        [Arguments(1, 10)]
        [Arguments(3, 60)]
        public async Task ItemsAreAdded_SumReflectsAllItems(int itemCount, int expectedSum)
        {
            var ages = new[] { 10, 20, 30 };
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).IsEmpty();

            // UUT Action
            for (var i = 0; i < itemCount; i++)
            {
                source.AddOrUpdate(new Person(((char)('A' + i)).ToString(), ages[i]));
            }

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).HasCount(itemCount).Because("each AddOrUpdate should produce a new sum emission");
            await Assert.That(results.RecordedValues[^1]).IsEqualTo(expectedSum).Because($"the sum of the first {itemCount} ages should be {expectedSum}");
        }

        [Test]
        [Arguments("A", 50)]
        [Arguments("B", 40)]
        [Arguments("C", 30)]
        public async Task ItemIsRemoved_SumReflectsRemoval(string keyToRemove, int expectedSum)
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(60).Because("the sum of ages 10 + 20 + 30 is 60");

            // UUT Action
            source.Remove(keyToRemove);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).HasCount(2).Because("one additional sum value should have been emitted after the removal");
            await Assert.That(results.RecordedValues[^1]).IsEqualTo(expectedSum).Because($"removing '{keyToRemove}' should leave a sum of {expectedSum}");
        }

        [Test]
        public async Task ItemIsUpdated_SumReflectsNewValue()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(30).Because("the sum of ages 10 + 20 is 30");

            // UUT Action: update "B" from age 20 to age 50 (same key, new value)
            source.AddOrUpdate(new Person("B", 50));

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).HasCount(2).Because("one additional sum value should have been emitted after the update");
            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60).Because("updating 'B' from 20 to 50 should change the sum from 30 to 60");
        }

        [Test]
        public async Task MultipleChangesInBatch_SingleSumEmitted()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues).IsEmpty();

            // UUT Action: add 3 items in a single batch
            source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("A", 10));
                updater.AddOrUpdate(new Person("B", 20));
                updater.AddOrUpdate(new Person("C", 30));
            });

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(60).Because("the sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task SourceIsEmpty_NoSumEmitted()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).IsEmpty();
        }

        [Test]
        public async Task AllItemsRemoved_SumReturnsToZero()
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

            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(60).Because("the sum of ages 10 + 20 + 30 is 60");

            // UUT Action: remove all items in a single batch
            source.Edit(updater => updater.Clear());

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).HasCount(2).Because("one additional sum value should have been emitted after clearing");
            await Assert.That(results.RecordedValues[^1]).IsEqualTo(0).Because("all items were removed so the sum should return to zero");
        }

        [Test]
        public async Task SourceCompletesAfterEmitting_CompletionPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(10).Because("the sum of a single age of 10 is 10");

            // UUT Action
            source.Complete();

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsTrue();
        }

        [Test]
        public async Task SourceCompletesWithoutEmitting_CompletionPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsTrue();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(60).Because("the sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task SourceCompletesImmediatelyWithoutEmitting_CompletionPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.Complete();

            // UUT Construction: source is already completed, with no items.
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsTrue();
            await Assert.That(results.RecordedValues).IsEmpty();
        }

        [Test]
        public async Task SourceErrorsAfterEmitting_ErrorPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
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
        public async Task SourceErrorsWithoutEmitting_ErrorPropagates()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            // UUT Construction
            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .ValidateSynchronization()
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues).IsEmpty();

            // UUT Action
            var error = new Exception("Test error");
            source.SetError(error);

            await Assert.That(results.Error).IsSameReferenceAs(error).Because("the error from the source should propagate to the subscriber");
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(results.RecordedValues).IsEmpty();
        }

        [Test]
        public async Task SourceFailsImmediately_ErrorPropagates()
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

            await Assert.That(results.Error).IsSameReferenceAs(error).Because("the error from the source should propagate to the subscriber immediately upon subscription");
            await Assert.That(results.HasCompleted).IsFalse();
        }

        [Test]
        public async Task NullableValuesAreTreatedAsZero()
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

            await Assert.That(results.Error).IsNull();
            await Assert.That(results.HasCompleted).IsFalse();
            await Assert.That(await Assert.That(results.RecordedValues).HasSingleItem()).IsEqualTo(40).Because("null values should be treated as zero, so the sum should be 10 + 0 + 30 = 40");
        }

        [Test]
        [Arguments(new[] { 10, 20, 30 }, 60)]
        [Arguments(new[] { int.MaxValue }, int.MaxValue)]
        [Arguments(new[] { int.MinValue }, int.MinValue)]
        [Arguments(new[] { int.MaxValue, -1 }, int.MaxValue - 1)]
        [Arguments(new[] { int.MinValue, 1 }, int.MinValue + 1)]
        public async Task ItemsAreAdded_SumIsCorrect_ForInt(int[] ages, int expectedSum)
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            for (var i = 0; i < ages.Length; i++)
            {
                source.AddOrUpdate(new Person(((char)('A' + i)).ToString(), ages[i]));
            }

            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(expectedSum).Because($"the int sum of [{string.Join(", ", ages)}] is {expectedSum}");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableInt()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", new int?(10), "F", null));
            source.AddOrUpdate(new Person("B", new int?(20), "F", null));
            source.AddOrUpdate(new Person("C", new int?(30), "F", null));

            using var subscription = source.Connect()
                .Sum(p => p.AgeNullable)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60).Because("the nullable int sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForLong()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (long)p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60L).Because("the long sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableLong()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (long?)p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60L).Because("the nullable long sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForDouble()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (double)p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60.0).Because("the double sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableDouble()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (double?)p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60.0).Because("the nullable double sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForDecimal()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (decimal)p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60M).Because("the decimal sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableDecimal()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (decimal?)p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60M).Because("the nullable decimal sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForFloat()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (float)p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60F).Because("the float sum of ages 10 + 20 + 30 is 60");
        }

        [Test]
        public async Task ItemsAreAdded_SumIsCorrect_ForNullableFloat()
        {
            using var source = new TestSourceCache<Person, string>(p => p.Name);

            source.AddOrUpdate(new Person("A", 10));
            source.AddOrUpdate(new Person("B", 20));
            source.AddOrUpdate(new Person("C", 30));

            using var subscription = source.Connect()
                .Sum(p => (float?)p.Age)
                .RecordValues(out var results);

            await Assert.That(results.RecordedValues[^1]).IsEqualTo(60F).Because("the nullable float sum of ages 10 + 20 + 30 is 60");
        }
    }
}
