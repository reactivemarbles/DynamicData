using DynamicData.Aggregation;

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
        public void ItemsAreClearedAfterMoves_FloatingPointSumsResetExactly()
        {
            using var source = new TestSourceList<double>();
            using var doubleSubscription = source.Connect()
                .Sum(value => value)
                .RecordValues(out var doubleResults);
            using var floatSubscription = source.Connect()
                .Sum(value => (float)value)
                .RecordValues(out var floatResults);

            source.AddRange(new[] { 0.1, 0.2, 0.3 });
            source.Move(0, 2);
            source.Clear();

            doubleResults.Error.Should().BeNull();
            floatResults.Error.Should().BeNull();
            doubleResults.RecordedValues[^1].Should().Be(0D);
            floatResults.RecordedValues[^1].Should().Be(0F);

            source.Add(7);

            doubleResults.RecordedValues[^1].Should().Be(7D);
            floatResults.RecordedValues[^1].Should().Be(7F);
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

        [Fact]
        public void ItemIsRefreshed_SumReflectsMutatedValue()
        {
            using var source = new TestSourceList<Person>();
            var person = new Person("A", 10);

            source.Add(person);

            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .RecordValues(out var results);

            person.Age = 40;
            source.Refresh(0);

            results.RecordedValues.Should().Equal(10, 40);

            source.RemoveAt(0);

            results.RecordedValues[^1].Should().Be(0, "removal should subtract the value captured by the refresh");
        }

        [Fact]
        public void ItemIsMoved_RefreshUsesItsNewIndex()
        {
            using var source = new TestSourceList<Person>();
            var first = new Person("A", 10);
            var second = new Person("B", 20);

            source.AddRange(new[] { first, second });

            using var subscription = source.Connect()
                .Sum(p => p.Age)
                .RecordValues(out var results);

            source.Move(1, 0);
            second.Age = 50;
            source.Refresh(0);

            results.RecordedValues.Should().Equal(30, 30, 60);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ItemIsMutatedWithoutRefresh_ReplaceOrRemovalSubtractsStoredValue(bool replace)
        {
            using var source = new TestSourceList<Person>();
            using var subscription = source.Connect()
                .Sum(person => person.Age)
                .RecordValues(out var results);
            var person = new Person("A", 10);
            source.Add(person);

            person.Age = 100;
            if (replace)
            {
                source.ReplaceAt(0, new Person("A", 25));
                results.RecordedValues[^1].Should().Be(25);
            }

            source.RemoveAt(0);

            results.Error.Should().BeNull();
            results.RecordedValues.Should().Equal(replace ? new[] { 10, 25, 0 } : new[] { 10, 0 });
        }

        [Fact]
        public void ItemsAreInsertedAndRemovedInTheMiddle_RefreshUsesShiftedPositions()
        {
            using var source = new TestSourceList<Person>();
            using var subscription = source.Connect()
                .Sum(person => person.Age)
                .RecordValues(out var results);
            var first = new Person("A", 10);
            var last = new Person("E", 40);
            source.AddRange(new[] { first, last });

            source.Edit(items => items.Insert(1, new Person("B", 20)));
            results.RecordedValues[^1].Should().Be(70);

            source.Edit(items => items.InsertRange(new[] { new Person("C", 25), new Person("D", 35) }, 2));
            results.RecordedValues[^1].Should().Be(130);

            last.Age = 50;
            source.Refresh(4);
            results.RecordedValues[^1].Should().Be(140);

            source.Edit(items => items.RemoveRange(1, 3));
            results.RecordedValues[^1].Should().Be(60);

            last.Age = 70;
            source.Refresh(1);
            results.Error.Should().BeNull();
            results.RecordedValues[^1].Should().Be(80);
        }

        [Fact]
        public void ReplacementChangesIndex_SubsequentRefreshAndRemovalUseNewPositions()
        {
            using var source = new Subject<IChangeSet<Person>>();
            using var subscription = source
                .Sum(person => person.Age)
                .RecordValues(out var results);
            var first = new Person("A", 10);
            var previous = new Person("B", 20);
            var last = new Person("C", 30);
            source.OnNext(new ChangeSet<Person>
            {
                new(ListChangeReason.AddRange, new[] { first, previous, last }, 0)
            });

            previous.Age = 100;
            var replacement = new Person("D", 5);
            source.OnNext(new ChangeSet<Person>
            {
                new(ListChangeReason.Replace, replacement, previous, currentIndex: 0, previousIndex: 1)
            });
            results.RecordedValues[^1].Should().Be(45);

            last.Age = 60;
            source.OnNext(new ChangeSet<Person> { new(ListChangeReason.Refresh, last, 2) });
            results.RecordedValues[^1].Should().Be(75);

            source.OnNext(new ChangeSet<Person> { new(ListChangeReason.Remove, first, 1) });
            results.Error.Should().BeNull();
            results.RecordedValues[^1].Should().Be(65);
        }

        [Theory]
        [InlineData(ListChangeReason.Remove, false, 40, 45)]
        [InlineData(ListChangeReason.Remove, true, 50, 55)]
        [InlineData(ListChangeReason.RemoveRange, false, 10, 15)]
        [InlineData(ListChangeReason.RemoveRange, true, 20, 25)]
        public void IndexlessRemoval_PrefersIdentityThenEqualityAndSubtractsStoredValues(
            ListChangeReason reason, bool equalInstance, int expectedAfterRemoval, int expectedAfterRefresh)
        {
            using var source = new Subject<IChangeSet<Person>>();
            using var subscription = source
                .Sum(person => person.Age)
                .RecordValues(out var results);
            var first = new Person("A", 10);
            var second = new Person("A", 20);
            var last = new Person("C", 30);
            source.OnNext(new ChangeSet<Person>
            {
                new(ListChangeReason.AddRange, new[] { first, second, last })
            });
            second.Age = 200;
            var removed = equalInstance ? new Person("A", 999) : second;

            source.OnNext(new ChangeSet<Person>
            {
                reason == ListChangeReason.Remove
                    ? new Change<Person>(reason, removed)
                    : new Change<Person>(reason, new[] { removed, last })
            });
            results.RecordedValues[^1].Should().Be(expectedAfterRemoval);

            var remaining = equalInstance ? second : first;
            remaining.Age = equalInstance ? 25 : 15;
            source.OnNext(new ChangeSet<Person> { new(ListChangeReason.Refresh, remaining, 0) });

            results.Error.Should().BeNull();
            results.RecordedValues[^1].Should().Be(expectedAfterRefresh);
        }

        public class IntegrationTests
        {
            [Fact]
            public void MixedEdits_SumMatchesLinqAfterEveryChangeset()
            {
                var randomizer = new Randomizer(1167);
                var editCount = randomizer.Int(200, 400);
                var expected = new List<Person>();
                using var source = new TestSourceList<Person>();
                using var subscription = source.Connect()
                    .Sum(person => person.Age)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                for (var edit = 0; edit < editCount; ++edit)
                {
                    // Each edit is delivered separately. Do not batch an Insert with a pending AddRange:
                    // ChangeAwareList's range coalescing for that case is a separate, known issue.
                    var operation = expected.Count == 0 ? 0 : randomizer.Int(0, 7);
                    var index = randomizer.Int(0, expected.Count);
                    switch (operation)
                    {
                        case 0:
                            var item = new Person(edit.ToString(), randomizer.Int(1, 100));
                            expected.Insert(index, item);
                            source.Edit(items => items.Insert(index, item));
                            break;
                        case 1:
                            var added = Enumerable.Range(0, randomizer.Int(2, 5))
                                .Select(offset => new Person($"{edit}:{offset}", randomizer.Int(1, 100)))
                                .ToArray();
                            expected.InsertRange(index, added);
                            source.Edit(items => items.InsertRange(added, index));
                            break;
                        case 2:
                            index = randomizer.Int(0, expected.Count - 1);
                            var replacement = new Person(edit.ToString(), randomizer.Int(1, 100));
                            expected[index] = replacement;
                            source.ReplaceAt(index, replacement);
                            break;
                        case 3:
                            index = randomizer.Int(0, expected.Count - 1);
                            expected.RemoveAt(index);
                            source.RemoveAt(index);
                            break;
                        case 4:
                            index = randomizer.Int(0, expected.Count - 1);
                            var destination = randomizer.Int(0, expected.Count - 1);
                            var moved = expected[index];
                            expected.RemoveAt(index);
                            expected.Insert(destination, moved);
                            source.Move(index, destination);
                            break;
                        case 5:
                            index = randomizer.Int(0, expected.Count - 1);
                            expected[index].Age = randomizer.Int(1, 100);
                            source.Refresh(index);
                            break;
                        case 6:
                            index = randomizer.Int(0, expected.Count - 1);
                            var count = randomizer.Int(1, expected.Count - index);
                            expected.RemoveRange(index, count);
                            source.Edit(items => items.RemoveRange(index, count));
                            break;
                        case 7:
                            expected.Clear();
                            source.Clear();
                            break;
                    }

                    results.Error.Should().BeNull($"edit {edit} must preserve a valid stream");
                    results.RecordedValues[^1].Should().Be(expected.Sum(person => person.Age), $"after edit {edit}");
                }

                source.Complete();
                results.HasCompleted.Should().BeTrue();
            }
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
