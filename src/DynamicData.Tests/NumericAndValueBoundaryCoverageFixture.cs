using System.Collections;

#if REACTIVE_TESTS
using DynamicData.Reactive.Aggregation;
using DynamicData.Reactive.Diagnostics;
using DynamicData.Reactive.Kernel;
using RxUnit = System.Reactive.Unit;
#else
using DynamicData.Aggregation;
using DynamicData.Diagnostics;
using DynamicData.Kernel;
using RxUnit = ReactiveUI.Primitives.RxVoid;
#endif

namespace DynamicData.Tests;

public sealed class NumericAndValueBoundaryCoverageFixture
{
    [Test]
    public async Task ListNumericAggregatesEmitExactBoundaryValues()
    {
        using var source = new SourceList<NumericSample>();

        var averageInt = new List<double>();
        var averageNullableInt = new List<double>();
        var averageLong = new List<double>();
        var averageNullableLong = new List<double>();
        var averageDouble = new List<double>();
        var averageNullableDouble = new List<double>();
        var averageDecimal = new List<decimal>();
        var averageNullableDecimal = new List<decimal>();
        var averageFloat = new List<float>();
        var averageNullableFloat = new List<float>();
        var counts = new List<int>();
        var empty = new List<bool>();
        var notEmpty = new List<bool>();
        var accumulated = new List<int>();

        using var subscriptions = new CompositeDisposable(
            source.Connect().Avg(item => item.IntValue, -1).Subscribe(averageInt.Add),
            source.Connect().Avg(item => item.NullableIntValue, -2).Subscribe(averageNullableInt.Add),
            source.Connect().Avg(item => item.LongValue, -3L).Subscribe(averageLong.Add),
            source.Connect().Avg(item => item.NullableLongValue, -4L).Subscribe(averageNullableLong.Add),
            source.Connect().Avg(item => item.DoubleValue, -5D).Subscribe(averageDouble.Add),
            source.Connect().Avg(item => item.NullableDoubleValue, -6D).Subscribe(averageNullableDouble.Add),
            source.Connect().Avg(item => item.DecimalValue, -7M).Subscribe(averageDecimal.Add),
            source.Connect().Avg(item => item.NullableDecimalValue, -8M).Subscribe(averageNullableDecimal.Add),
            source.Connect().Avg(item => item.FloatValue, -9F).Subscribe(averageFloat.Add),
            source.Connect().Avg(item => item.NullableFloatValue, -10F).Subscribe(averageNullableFloat.Add),
            CountEx.Count(source.Connect()).Subscribe(counts.Add),
            CountEx.IsEmpty(source.Connect()).Subscribe(empty.Add),
            CountEx.IsNotEmpty(source.Connect()).Subscribe(notEmpty.Add),
            source.Connect().Accumulate(0, item => item.IntValue, (current, value) => current + value, (current, value) => current - value).Subscribe(accumulated.Add));

        source.AddRange(
            new[]
            {
                new NumericSample("A", 2, 4L, 6D, 8M, 10F, 1),
                new NumericSample("B", 4, 8L, 10D, 12M, 14F, 2, UseNullNullableValues: true)
            });
        source.Clear();

        await Assert.That(averageInt).IsEquivalentTo(new[] { 3D, -1D }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageNullableInt).IsEquivalentTo(new[] { 1D, -2D }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageLong).IsEquivalentTo(new[] { 6D, -3D }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageNullableLong).IsEquivalentTo(new[] { 2D, -4D }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageDouble).IsEquivalentTo(new[] { 8D, -5D }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageNullableDouble).IsEquivalentTo(new[] { 3D, -6D }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageDecimal).IsEquivalentTo(new[] { 10M, -7M }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageNullableDecimal).IsEquivalentTo(new[] { 4M, -8M }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageFloat).IsEquivalentTo(new[] { 12F, -9F }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(averageNullableFloat).IsEquivalentTo(new[] { 5F, -10F }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(counts).IsEquivalentTo(new[] { 2, 0 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(empty).IsEquivalentTo(new[] { true, false, true }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(notEmpty).IsEquivalentTo(new[] { false, true, false }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(accumulated).IsEquivalentTo(new[] { 6, 0 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ListMaximumAndMinimumResetWhenCurrentBoundaryIsRemoved()
    {
        using var source = new SourceList<NumericSample>();

        var maximum = new List<int>();
        var minimum = new List<int>();

        using var subscriptions = new CompositeDisposable(
            source.Connect().Maximum(item => item.IntValue, 99).Subscribe(maximum.Add),
            source.Connect().Minimum(item => item.IntValue, -99).Subscribe(minimum.Add));

        var two = new NumericSample("Two", 2, 2, 2, 2, 2, 1);
        var five = new NumericSample("Five", 5, 5, 5, 5, 5, 1);
        var three = new NumericSample("Three", 3, 3, 3, 3, 3, 1);

        source.Add(two);
        source.Add(five);
        source.Add(three);
        source.Remove(five);
        source.Clear();

        await Assert.That(maximum).IsEquivalentTo(new[] { 2, 5, 3, 99 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(minimum).IsEquivalentTo(new[] { 2, -99 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task CacheCountDistinctCountAndAggregationReflectUpdatesAndRemoves()
    {
        using var source = new SourceCache<NumericSample, string>(item => item.Key);

        var counts = new List<int>();
        var empty = new List<bool>();
        var notEmpty = new List<bool>();
        var distinctCounts = new List<int>();
        var accumulated = new List<int>();
        var aggregateEvents = new List<AggregateItem<NumericSample>>();

        using var subscriptions = new CompositeDisposable(
            CountEx.Count(source.Connect()).Subscribe(counts.Add),
            CountEx.IsEmpty(source.Connect()).Subscribe(empty.Add),
            CountEx.IsNotEmpty(source.Connect()).Subscribe(notEmpty.Add),
            CountEx.Count(source.Connect().DistinctValues(item => item.Group)).Subscribe(distinctCounts.Add),
            source.Connect().Accumulate(0, item => item.IntValue, (current, value) => current + value, (current, value) => current - value).Subscribe(accumulated.Add),
            source.Connect().ForAggregation().Subscribe(changes => aggregateEvents.AddRange(changes)));

        var first = new NumericSample("A", 10, 10, 10, 10, 10, 1);
        var second = new NumericSample("B", 20, 20, 20, 20, 20, 1);
        var updatedFirst = new NumericSample("A", 11, 11, 11, 11, 11, 2);

        source.AddOrUpdate(first);
        source.AddOrUpdate(second);
        source.AddOrUpdate(updatedFirst);
        source.Remove("B");

        await Assert.That(counts).IsEquivalentTo(new[] { 1, 2, 2, 1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(empty).IsEquivalentTo(new[] { true, false, false, false, false }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(notEmpty).IsEquivalentTo(new[] { false, true, true, true, true }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(distinctCounts).IsEquivalentTo(new[] { 1, 2, 1 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(accumulated).IsEquivalentTo(new[] { 10, 30, 31, 11 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(aggregateEvents.Select(item => item.Type).ToArray())
            .IsEquivalentTo(new[] { AggregateType.Add, AggregateType.Add, AggregateType.Remove, AggregateType.Add, AggregateType.Remove }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(aggregateEvents.Select(item => item.Item.IntValue).ToArray()).IsEquivalentTo(new[] { 10, 20, 10, 11, 20 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task InvalidateWhenResubscribesAndSuppressesUnchangedValues()
    {
        using var invalidate = new ReactiveUI.Primitives.Signals.Signal<RxUnit>();
        using var genericInvalidate = new ReactiveUI.Primitives.Signals.Signal<string?>();

        var unitValues = new List<int>();
        var genericValues = new List<int>();
        var unitSubscriptions = 0;
        var genericSubscriptions = 0;

        using var subscriptions = new CompositeDisposable(
            Observable.Defer(() => Observable.Return(++unitSubscriptions)).InvalidateWhen(invalidate).Subscribe(unitValues.Add),
            Observable.Defer(() => Observable.Return(++genericSubscriptions)).InvalidateWhen(genericInvalidate).Subscribe(genericValues.Add));

        invalidate.OnNext(default);
        invalidate.OnNext(default);
        genericInvalidate.OnNext("first");
        genericInvalidate.OnNext(null);

        await Assert.That(unitValues).IsEquivalentTo(new[] { 1, 2, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(genericValues).IsEquivalentTo(new[] { 1, 2, 3 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task DiagnosticsValueObjectsCompareHashAndFormatTheirCounts()
    {
        var latest = new ChangeStatistics(Index: 3, Adds: 4, Updates: 5, Removes: 6, Refreshes: 7, Moves: 8, Count: 9);
        var overall = new ChangeStatistics(Index: 3, Adds: 10, Updates: 11, Removes: 12, Refreshes: 13, Moves: 14, Count: 15);
        var summary = new ChangeSummary(index: 3, latest, overall);
        var same = new ChangeSummary(index: 3, latest, overall);
        var different = new ChangeSummary(index: 4, latest, overall);

        await Assert.That(summary.Equals(null)).IsFalse();
        await Assert.That(summary.Equals(summary)).IsTrue();
        await Assert.That(summary.Equals(new object())).IsFalse();
        await Assert.That(summary.Equals(same)).IsTrue();
        await Assert.That(summary.Equals(different)).IsFalse();
        await Assert.That(summary.GetHashCode()).IsEqualTo(same.GetHashCode());
        await Assert.That(summary.ToString()).IsEqualTo("CurrentIndex: 3, Latest Count: 9, Overall Count: 15");
        await Assert.That(latest.GetHashCode()).IsNotEqualTo(0);
        await Assert.That(latest.ToString()).Contains("CurrentIndex: 3, Adds: 4, Updates: 5, Removes: 6, Refreshes: 7, Count: 9, Timestamp:");
    }

    [Test]
    public async Task OptionHelpersExposePresentMissingAndThrowingBoundaries()
    {
        var values = new[] { "one", "two" };
        var matched = values.FirstOrOptional(value => value.StartsWith("t", StringComparison.Ordinal));
        var missing = values.FirstOrOptional(value => value.StartsWith("z", StringComparison.Ordinal));
        ReactiveUI.Primitives.Optional<string>? nullableNone = ReactiveUI.Primitives.Optional<string>.None;
        ReactiveUI.Primitives.Optional<string>? nullableSome = ReactiveUI.Primitives.Optional<string>.Some("present");

        var noneActionCalled = false;
        var someActionValue = string.Empty;
        nullableNone.IfHasValue(_ => noneActionCalled = true);
        nullableSome.IfHasValue(value => someActionValue = value);

        await Assert.That(matched.HasValue).IsTrue();
        await Assert.That(matched.Value).IsEqualTo("two");
        await Assert.That(missing.HasValue).IsFalse();
        await Assert.That(noneActionCalled).IsFalse();
        await Assert.That(someActionValue).IsEqualTo("present");
        await Assert.That(() => ((IEnumerable<string>?)null)!.FirstOrOptional(value => value.Length > 0)).Throws<ArgumentNullException>();
        await Assert.That(() => values.FirstOrOptional(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => ReactiveUI.Primitives.Optional<string>.None.ValueOrThrow(() => new InvalidOperationException("missing"))).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task EnumerableIListForwardsListArrayAndChangeSetBoundaries()
    {
        EnumerableIList<int> fromList = new List<int> { 1, 2 };
        EnumerableIList<int> fromArray = new[] { 3, 4 };
        var empty = EnumerableIList<int>.Empty;
        var copy = new int[2];
        var nonGenericItems = new List<int>();
        var genericItems = new List<int>();
        var changeSet = new ChangeSet<NumericSample, string> { new(ChangeReason.Add, "A", new NumericSample("A", 1, 1, 1, 1, 1, 1)) };
        var changeSetList = EnumerableIList.Create((IChangeSet<NumericSample, string>)changeSet);

        fromList[0] = 5;
        fromList.CopyTo(copy, 0);
        foreach (var item in (IEnumerable)fromList)
        {
            nonGenericItems.Add((int)item);
        }

        foreach (var item in (IEnumerable<int>)fromList)
        {
            genericItems.Add(item);
        }

        await Assert.That(empty.Equals(default(EnumerableIList<int>))).IsTrue();
        await Assert.That(fromList.IsReadOnly).IsFalse();
        await Assert.That(fromList.Contains(5)).IsTrue();
        await Assert.That(copy).IsEquivalentTo(new[] { 5, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(nonGenericItems).IsEquivalentTo(new[] { 5, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(genericItems).IsEquivalentTo(new[] { 5, 2 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fromArray.IsReadOnly).IsTrue();
        await Assert.That(fromArray[1]).IsEqualTo(4);
        await Assert.That(changeSetList.Count).IsEqualTo(1);
        await Assert.That(changeSetList[0].Current.Key).IsEqualTo("A");
    }

    [Test]
    public async Task SmallValueStructsHashAndFormatNullAndNonNullValues()
    {
        var aggregateWithNull = new AggregateItem<string?>(AggregateType.Add, null);
        var aggregateWithValue = new AggregateItem<string?>(AggregateType.Remove, "value");
        var indexedWithNull = new ItemWithIndex<string?>(null, 4);
        var indexedWithValue = new ItemWithIndex<string>("item", 5);
        var valuedWithNulls = new ItemWithValue<string?, string?>(null, null);
        var valuedWithValues = new ItemWithValue<string, int>("item", 6);

        await Assert.That(aggregateWithNull.GetHashCode()).IsNotEqualTo(aggregateWithValue.GetHashCode());
        await Assert.That(indexedWithNull.GetHashCode()).IsEqualTo(0);
        await Assert.That(indexedWithNull.ToString()).IsEqualTo(" (4)");
        await Assert.That(indexedWithValue.GetHashCode()).IsEqualTo(EqualityComparer<string>.Default.GetHashCode("item"));
        await Assert.That(indexedWithValue.ToString()).IsEqualTo("item (5)");
        await Assert.That(valuedWithNulls.GetHashCode()).IsEqualTo(0);
        await Assert.That(valuedWithNulls.ToString()).IsEqualTo(" ()");
        await Assert.That(valuedWithValues.ToString()).IsEqualTo("item (6)");
    }

    private sealed record NumericSample(
        string Key,
        int IntValue,
        long LongValue,
        double DoubleValue,
        decimal DecimalValue,
        float FloatValue,
        int Group,
        bool UseNullNullableValues = false)
    {
        public int? NullableIntValue => UseNullNullableValues ? null : IntValue;

        public long? NullableLongValue => UseNullNullableValues ? null : LongValue;

        public double? NullableDoubleValue => UseNullNullableValues ? null : DoubleValue;

        public decimal? NullableDecimalValue => UseNullNullableValues ? null : DecimalValue;

        public float? NullableFloatValue => UseNullNullableValues ? null : FloatValue;
    }
}
