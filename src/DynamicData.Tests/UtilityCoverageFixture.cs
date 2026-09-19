#if REACTIVE_TESTS
using DynamicData.Reactive.Alias;
using DynamicData.Reactive.Aggregation;
using DynamicData.Reactive.Diagnostics;
using DynamicData.Reactive.Kernel;
using RxUnit = System.Reactive.Unit;
#else
using DynamicData.Alias;
using DynamicData.Aggregation;
using DynamicData.Diagnostics;
using DynamicData.Kernel;
using RxUnit = ReactiveUI.Primitives.RxVoid;
#endif

namespace DynamicData.Tests;

public class UtilityCoverageFixture
{
    [Test]
    public async Task CacheStdDevOverloadsEmitFallbackAndUpdatedValues()
    {
        using var source = new SourceCache<Measure, string>(item => item.Key);

        var ints = new List<double>();
        var longs = new List<double>();
        var doubles = new List<double>();
        var decimals = new List<decimal>();
        var floats = new List<double>();

        using var subscriptions = new CompositeDisposable(
            source.Connect().StdDev(item => item.IntValue, 12).Subscribe(ints.Add),
            source.Connect().StdDev(item => item.LongValue, 13L).Subscribe(longs.Add),
            source.Connect().StdDev(item => item.DoubleValue, 14D).Subscribe(doubles.Add),
            source.Connect().StdDev(item => item.DecimalValue, 15M).Subscribe(decimals.Add),
            source.Connect().StdDev(item => item.FloatValue, 16F).Subscribe(floats.Add));

        source.AddOrUpdate(new Measure("A", 2));
        source.AddOrUpdate(new Measure("B", 4));
        source.AddOrUpdate(new Measure("C", 6));
        source.Remove("A");
        source.Remove("B");
        source.Remove("C");

        await AssertStdDevValues(ints, 12D, Math.Sqrt(2D), 2D, Math.Sqrt(2D), 12D, 12D);
        await AssertStdDevValues(longs, 13D, Math.Sqrt(2D), 2D, Math.Sqrt(2D), 13D, 13D);
        await AssertStdDevValues(doubles, 14D, Math.Sqrt(2D), 2D, Math.Sqrt(2D), 14D, 14D);
        await AssertDecimalStdDevValues(decimals, 15M, 1.4142135623730950488016887242M, 2M, 1.4142135623730950488016887242M, 15M, 15M);
        await AssertStdDevValues(floats, 16D, Math.Sqrt(2D), 2D, Math.Sqrt(2D), 16D, 16D);
    }

    [Test]
    public async Task ListStdDevOverloadsEmitFallbackAndUpdatedValues()
    {
        using var source = new SourceList<Measure>();

        var ints = new List<double>();
        var longs = new List<double>();
        var doubles = new List<double>();
        var decimals = new List<decimal>();
        var floats = new List<double>();

        using var subscriptions = new CompositeDisposable(
            source.Connect().StdDev(item => item.IntValue, 12).Subscribe(ints.Add),
            source.Connect().StdDev(item => item.LongValue, 13L).Subscribe(longs.Add),
            source.Connect().StdDev(item => item.DoubleValue, 14D).Subscribe(doubles.Add),
            source.Connect().StdDev(item => item.DecimalValue, 15M).Subscribe(decimals.Add),
            source.Connect().StdDev(item => item.FloatValue, 16F).Subscribe(floats.Add));

        var first = new Measure("A", 2);
        source.Add(first);
        source.Add(new Measure("B", 4));
        source.Add(new Measure("C", 6));
        source.Remove(first);
        source.Clear();

        await AssertStdDevValues(ints, 12D, Math.Sqrt(2D), 2D, Math.Sqrt(2D), 12D);
        await AssertStdDevValues(longs, 13D, Math.Sqrt(2D), 2D, Math.Sqrt(2D), 13D);
        await AssertStdDevValues(doubles, 14D, Math.Sqrt(2D), 2D, Math.Sqrt(2D), 14D);
        await AssertDecimalStdDevValues(decimals, 15M, 1.4142135623730950488016887242M, 2M, 1.4142135623730950488016887242M, 15M);
        await AssertStdDevValues(floats, 16D, Math.Sqrt(2D), 2D, Math.Sqrt(2D), 16D);
    }

    [Test]
    public async Task StdDevUsesSampleVarianceForNonIntegerMeanAndLargeValues()
    {
        using var source = new SourceCache<Measure, string>(item => item.Key);

        var ints = new List<double>();
        var longs = new List<double>();
        var decimals = new List<decimal>();

        using var subscriptions = new CompositeDisposable(
            source.Connect().StdDev(item => item.IntValue, 0).Subscribe(ints.Add),
            source.Connect().StdDev(item => item.LongValue, 0L).Subscribe(longs.Add),
            source.Connect().StdDev(item => item.DecimalValue, 0M).Subscribe(decimals.Add));

        source.AddOrUpdate(new Measure("A", 1));
        source.AddOrUpdate(new Measure("B", 2));
        source.AddOrUpdate(new Measure("C", 4));

        await AssertClose(ints[^1], Math.Sqrt(7D / 3D));
        await AssertClose(longs[^1], Math.Sqrt(7D / 3D));
        await AssertClose((double)decimals[^1], Math.Sqrt(7D / 3D));

        source.AddOrUpdate(new Measure("A", int.MaxValue - 2, 4_000_000_000L, 4_000_000_000M, 4_000_000_000D, 4_000_000_000F));
        source.AddOrUpdate(new Measure("B", int.MaxValue - 1, 4_000_000_001L, 4_000_000_001M, 4_000_000_001D, 4_000_000_001F));
        source.AddOrUpdate(new Measure("C", int.MaxValue, 4_000_000_002L, 4_000_000_002M, 4_000_000_002D, 4_000_000_002F));

        await AssertClose(ints[^1], 1D);
        await AssertClose(longs[^1], 1D);
        // Removing a very different value introduces decimal rounding in central moments.
        await AssertDecimalClose(decimals[^1], 1M, 0.000000001M);
    }

    [Test]
    public async Task StdDevDistinguishesAdjacentLargeIntegralValues()
    {
        using var source = new SourceList<long>();
        var longs = new List<double>();
        var decimals = new List<decimal>();
        using var longSubscription = source.Connect().StdDev(value => value, 0L).Subscribe(longs.Add);
        using var decimalSubscription = source.Connect().StdDev(value => (decimal)value, 0M).Subscribe(decimals.Add);

        // Keep the spread small: central moments, unlike the mean, must be squared.
        source.AddRange(new[] { 9_000_000_000_000_000_000L, 9_000_000_000_000_000_001L, 9_000_000_000_000_000_002L });

        await AssertClose(longs[^1], 1D);
        await AssertDecimalClose(decimals[^1], 1M);
    }

    [Test]
    public async Task StdDevWrappedOverloadsThrowWhenArgumentsAreNull()
    {
        IObservable<IChangeSet<Measure>>? listSource = null;
        IObservable<IChangeSet<Measure, string>>? cacheSource = null;
        Func<Measure, int>? selector = null;

        await Assert.That(() => listSource!.StdDev(item => item.IntValue, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => cacheSource!.StdDev(item => item.IntValue, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => Observable.Empty<IChangeSet<Measure>>().StdDev(selector!, 0)).Throws<ArgumentNullException>();
        await Assert.That(() => Observable.Empty<IChangeSet<Measure, string>>().StdDev(selector!, 0)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ObservableCacheAliasSelectOverloadsTransformAndForceRefresh()
    {
        using var source = new SourceCache<Measure, string>(item => item.Key);
        using var forceAll = new ReactiveUI.Primitives.Signals.Signal<RxUnit>();
        using var forceWithKey = new ReactiveUI.Primitives.Signals.Signal<Func<Measure, string, bool>>();
        using var forceWithoutKey = new ReactiveUI.Primitives.Signals.Signal<Func<Measure, bool>>();

        var keyAwareCount = 0;
        var keylessCount = 0;
        var keyAwareFilteredCount = 0;
        var keylessFilteredCount = 0;

        using var keyAware = ObservableCacheAlias.Select(
                source.Connect(),
                (Measure item, string key) => $"{key}:{item.IntValue}:{++keyAwareCount}",
                forceAll)
            .AsAggregator();
        using var keyless = ObservableCacheAlias.Select(
                source.Connect(),
                (Measure item) => $"{item.Key}:{item.IntValue}:{++keylessCount}",
                forceAll)
            .AsAggregator();
        using var keyAwareFiltered = ObservableCacheAlias.Select(
                source.Connect(),
                (Measure item, string key) => $"{key}:{item.IntValue}:{++keyAwareFilteredCount}",
                forceWithKey)
            .AsAggregator();
        using var keylessFiltered = ObservableCacheAlias.Select(
                source.Connect(),
                (Measure item) => $"{item.Key}:{item.IntValue}:{++keylessFilteredCount}",
                forceWithoutKey)
            .AsAggregator();

        source.AddOrUpdate(new Measure("A", 1));
        source.AddOrUpdate(new Measure("B", 2));
        forceAll.OnNext(default);
        forceWithKey.OnNext((_, key) => key == "A");
        forceWithoutKey.OnNext(item => item.Key == "B");

        await Assert.That(keyAware.Data.Items.OrderBy(item => item).ToArray()).IsEquivalentTo(new[] { "A:1:3", "B:2:4" });
        await Assert.That(keyless.Data.Items.OrderBy(item => item).ToArray()).IsEquivalentTo(new[] { "A:1:3", "B:2:4" });
        await Assert.That(keyAwareFiltered.Data.Items.OrderBy(item => item).ToArray()).IsEquivalentTo(new[] { "A:1:3", "B:2:2" });
        await Assert.That(keylessFiltered.Data.Items.OrderBy(item => item).ToArray()).IsEquivalentTo(new[] { "A:1:1", "B:2:3" });
    }

    [Test]
    public async Task ObservableCacheAliasSelectManyWhereAndSelectTreeDelegateToCacheOperators()
    {
        using var source = new SourceCache<TreeItem, string>(item => item.Key);
        using var predicateChanged = new ReactiveUI.Primitives.Signals.StateSignal<Func<TreeItem, bool>>(item => item.Score >= 10);
        using var reapply = new ReactiveUI.Primitives.Signals.Signal<RxUnit>();

        using var many = ObservableCacheAlias.SelectMany(
                source.Connect(),
                item => item.Children,
                child => child.Key)
            .AsAggregator();
        using var staticWhere = ObservableCacheAlias.Where(source.Connect(), (TreeItem item) => item.Score >= 10).AsAggregator();
        using var dynamicWhere = ObservableCacheAlias.Where(source.Connect(), predicateChanged).AsAggregator();
        using var refreshWhere = ObservableCacheAlias.Where(source.Connect(), predicateChanged, reapply).AsAggregator();
        using var tree = ObservableCacheAlias.SelectTree(source.Connect(), item => item.ParentKey).AsAggregator();

        var parent = new TreeItem("Parent", string.Empty, 10, new[] { new TreeItem("Child", "Parent", 1, Array.Empty<TreeItem>()) });
        var low = new TreeItem("Low", string.Empty, 1, Array.Empty<TreeItem>());

        source.AddOrUpdate(parent);
        source.AddOrUpdate(low);
        predicateChanged.OnNext(item => item.Score < 10);
        low.Score = 11;
        reapply.OnNext(default);

        await Assert.That(many.Data.Items.Select(item => item.Key).ToArray()).IsEquivalentTo(new[] { "Child" });
        await Assert.That(staticWhere.Data.Items.Select(item => item.Key).ToArray()).IsEquivalentTo(new[] { "Parent" });
        await Assert.That(dynamicWhere.Data.Items.Select(item => item.Key).ToArray()).IsEquivalentTo(new[] { "Low" });
        await Assert.That(refreshWhere.Data.Items.Select(item => item.Key).ToArray()).IsEquivalentTo(Array.Empty<string>());
        await Assert.That(tree.Data.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ObservableCacheAliasSelectSafeReportsErrorsAndForceRefreshes()
    {
        using var source = new SourceCache<Measure, string>(item => item.Key);
        using var forceAll = new ReactiveUI.Primitives.Signals.Signal<RxUnit>();
        using var forceWithKey = new ReactiveUI.Primitives.Signals.Signal<Func<Measure, string, bool>>();
        using var forceWithoutKey = new ReactiveUI.Primitives.Signals.Signal<Func<Measure, bool>>();

        var errors = new List<Error<Measure, string>>();
        var keyAwareErrors = new List<Error<Measure, string>>();
        var allErrors = new List<Error<Measure, string>>();

        using var keyless = ObservableCacheAlias.SelectSafe(
                source.Connect(),
                TransformOrThrow,
                errors.Add,
                forceWithoutKey)
            .AsAggregator();
        using var keyAware = ObservableCacheAlias.SelectSafe(
                source.Connect(),
                (Measure item, string key) => TransformOrThrow(item) + key,
                keyAwareErrors.Add,
                forceWithKey)
            .AsAggregator();
        using var all = ObservableCacheAlias.SelectSafe(
                source.Connect(),
                (Measure item, string key) => TransformOrThrow(item) + key,
                allErrors.Add,
                forceAll)
            .AsAggregator();

        source.AddOrUpdate(new Measure("A", 1));
        source.AddOrUpdate(new Measure("B", -1));
        forceWithoutKey.OnNext(item => item.Key == "B");
        forceWithKey.OnNext((item, _) => item.Key == "B");
        forceAll.OnNext(default);

        await Assert.That(keyless.Data.Items.ToArray()).IsEquivalentTo(new[] { "A:1" });
        await Assert.That(keyAware.Data.Items.ToArray()).IsEquivalentTo(new[] { "A:1A" });
        await Assert.That(all.Data.Items.ToArray()).IsEquivalentTo(new[] { "A:1A" });
        await Assert.That(errors).HasSingleItem();
        await Assert.That(keyAwareErrors).HasSingleItem();
        await Assert.That(allErrors).HasSingleItem();
        await Assert.That(errors[0].Key).IsEqualTo("B");
        await Assert.That(errors[0].Value.IntValue).IsEqualTo(-1);
    }

    [Test]
    public async Task KernelRetryWithBackOffRetriesUntilStrategyStops()
    {
        var attempts = 0;
        var errors = new List<Exception>();
        var values = new List<int>();

        var source = Observable.Create<int>(
            observer =>
            {
                attempts++;
                if (attempts < 3)
                {
                    observer.OnError(new InvalidOperationException($"Attempt {attempts}"));
                }
                else
                {
                    observer.OnNext(42);
                    observer.OnCompleted();
                }

                return Disposable.Empty;
            });

        var result = source.RetryWithBackOff<int, InvalidOperationException>((_, count) => count < 3 ? TimeSpan.Zero : null);

        using var subscription = result.Subscribe(values.Add, errors.Add);

        await Assert.That(values).IsEquivalentTo(new[] { 42 });
        await Assert.That(errors).IsEmpty();
        await Assert.That(attempts).IsEqualTo(3);
    }

    [Test]
    public async Task KernelRetryWithBackOffPropagatesWhenStrategyStops()
    {
        var attempts = 0;
        var errors = new List<Exception>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var source = Observable.Create<int>(
            observer =>
            {
                attempts++;
                observer.OnError(new InvalidOperationException("Stop"));
                return Disposable.Empty;
            });

        var result = source.RetryWithBackOff<int, InvalidOperationException>((_, _) => null);

        using var subscription = result.Subscribe(_ => { }, error => { errors.Add(error); completed.TrySetResult(); });

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(attempts).IsEqualTo(1);
        await Assert.That(errors).HasSingleItem();
        await Assert.That(errors[0]).IsTypeOf<InvalidOperationException>();
    }

    [Test]
    public async Task KernelScheduleRecurringActionReschedulesUntilDisposed()
    {
        var scheduler = new TestScheduler();
        var count = 0;
        var intervals = new Queue<TimeSpan>(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) });

        using var recurring = scheduler.ScheduleRecurringAction(() => intervals.Count == 0 ? TimeSpan.FromSeconds(3) : intervals.Dequeue(), () => count++);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks - 1);
        await Assert.That(count).IsEqualTo(0);

        scheduler.AdvanceBy(1);
        await Assert.That(count).IsEqualTo(1);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks - 1);
        await Assert.That(count).IsEqualTo(1);

        scheduler.AdvanceBy(1);
        await Assert.That(count).IsEqualTo(2);

        recurring.Dispose();

        scheduler.AdvanceBy(TimeSpan.FromSeconds(3).Ticks);
        await Assert.That(count).IsEqualTo(2);

        var fixedCount = 0;
        using var fixedRecurring = scheduler.ScheduleRecurringAction(TimeSpan.FromSeconds(4), () => fixedCount++);

        scheduler.AdvanceBy(TimeSpan.FromSeconds(4).Ticks - 1);
        await Assert.That(fixedCount).IsEqualTo(0);

        scheduler.AdvanceBy(1);
        await Assert.That(fixedCount).IsEqualTo(1);

        fixedRecurring.Dispose();

        scheduler.AdvanceBy(TimeSpan.FromSeconds(4).Ticks);
        await Assert.That(fixedCount).IsEqualTo(1);
    }

    [Test]
    public async Task KernelHelpersSwapConvertToUnitAndCreateFreshReturnValues()
    {
        var first = "left";
        var second = "right";
        InternalEx.Swap(ref first, ref second);

        var unitValues = new List<RxUnit>();
        var counter = 0;
        using var subscription = InternalEx.Return(() => ++counter).ToUnit().Subscribe(unitValues.Add);
        using var secondSubscription = InternalEx.Return(() => ++counter).Subscribe(_ => { });
        using var signal = new ReactiveUI.Primitives.Signals.Signal<RxUnit>();
        var signalCount = 0;
        using var signalSubscription = signal.Subscribe(_ => signalCount++);

        signal.OnNext();

        await Assert.That(first).IsEqualTo("right");
        await Assert.That(second).IsEqualTo("left");
        await Assert.That(unitValues).HasSingleItem();
        await Assert.That(counter).IsEqualTo(2);
        await Assert.That(signalCount).IsEqualTo(1);
        await Assert.That(InternalEx.NewLock()).IsNotNull();
        await Assert.That(InternalEx.NewMonitorGate()).IsNotNull();
    }

    [Test]
    public async Task ParallelSelectRunsWithBoundedConcurrencyAndPreservesSourceOrder()
    {
        var running = 0;
        var maxRunning = 0;

        var result = await Enumerable.Range(1, 6).SelectParallel(
            async item =>
            {
                var active = Interlocked.Increment(ref running);
                maxRunning = Math.Max(maxRunning, active);

                await Task.Delay(item % 2 == 0 ? 1 : 5);

                Interlocked.Decrement(ref running);
                return item * 10;
            },
            maximumThreads: 2);

        await Assert.That(result.SequenceEqual(new[] { 10, 20, 30, 40, 50, 60 })).IsTrue();
        await Assert.That(maxRunning).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task DiagnosticOperatorsCollectCacheAndListStatistics()
    {
        using var cache = new SourceCache<Measure, string>(item => item.Key);
        using var list = new SourceList<Measure>();
        var cacheSummaries = new List<ChangeSummary>();
        var listSummaries = new List<ChangeSummary>();

        using var cacheSubscription = cache.Connect().CollectUpdateStats().Subscribe(cacheSummaries.Add);
        using var listSubscription = list.Connect().CollectUpdateStats().Subscribe(listSummaries.Add);

        cache.AddOrUpdate(new Measure("A", 1));
        cache.AddOrUpdate(new Measure("A", 2));
        cache.Remove("A");

        var listItem = new Measure("L", 1);
        list.Add(listItem);
        list.ReplaceAt(0, new Measure("L", 2));
        list.RemoveAt(0);

        var cacheOverall = cacheSummaries.Last().Overall;
        var cacheLatest = cacheSummaries.Last().Latest;
        var listOverall = listSummaries.Last().Overall;
        var listLatest = listSummaries.Last().Latest;

        await Assert.That(cacheOverall.Adds).IsEqualTo(1);
        await Assert.That(cacheOverall.Updates).IsEqualTo(1);
        await Assert.That(cacheOverall.Removes).IsEqualTo(1);
        await Assert.That(cacheLatest.Removes).IsEqualTo(1);
        await Assert.That(listOverall.Adds).IsEqualTo(1);
        await Assert.That(listOverall.Updates).IsEqualTo(1);
        await Assert.That(listOverall.Removes).IsEqualTo(1);
        await Assert.That(listLatest.Removes).IsEqualTo(1);
    }

    private static string TransformOrThrow(Measure item)
    {
        if (item.IntValue < 0)
        {
            throw new InvalidOperationException(item.Key);
        }

        return $"{item.Key}:{item.IntValue}";
    }

    private static async Task AssertStdDevValues(IReadOnlyList<double> values, double fallback, params double[] expectedTail)
    {
        await Assert.That(values.Count).IsEqualTo(expectedTail.Length + 1);
        await Assert.That(values[0]).IsEqualTo(fallback);

        for (var i = 0; i < expectedTail.Length; i++)
        {
            await AssertClose(values[values.Count - expectedTail.Length + i], expectedTail[i]);
        }
    }

    private static async Task AssertDecimalStdDevValues(IReadOnlyList<decimal> values, decimal fallback, params decimal[] expectedTail)
    {
        await Assert.That(values.Count).IsEqualTo(expectedTail.Length + 1);
        await Assert.That(values[0]).IsEqualTo(fallback);

        for (var i = 0; i < expectedTail.Length; i++)
        {
            await AssertDecimalClose(values[values.Count - expectedTail.Length + i], expectedTail[i]);
        }
    }

    private static async Task AssertClose(double actual, double expected, double tolerance = 0.000_000_001D)
    {
        await Assert.That(Math.Abs(actual - expected)).IsLessThanOrEqualTo(tolerance);
    }

    private static async Task AssertDecimalClose(decimal actual, decimal expected, decimal tolerance = 0.000_000_000_000_000_000_000_001M)
    {
        await Assert.That(Math.Abs(actual - expected)).IsLessThanOrEqualTo(tolerance);
    }

    private sealed record Measure(string Key, int IntValue, long? LongValueOverride = null, decimal? DecimalValueOverride = null, double? DoubleValueOverride = null, float? FloatValueOverride = null)
    {
        public long LongValue => LongValueOverride ?? IntValue;

        public double DoubleValue => DoubleValueOverride ?? IntValue;

        public decimal DecimalValue => DecimalValueOverride ?? IntValue;

        public float FloatValue => FloatValueOverride ?? IntValue;
    }

    private sealed class TreeItem(string key, string parentKey, int score, IEnumerable<TreeItem> children)
    {
        public string Key { get; } = key;

        public string ParentKey { get; } = parentKey;

        public int Score { get; set; } = score;

        public IEnumerable<TreeItem> Children { get; } = children;
    }
}
