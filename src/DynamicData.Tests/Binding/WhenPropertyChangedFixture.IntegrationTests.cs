using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Binding;
 
public static partial class WhenPropertyChangedFixture
{
    public sealed partial class IntegrationTests
        : IntegrationTestFixtureBase
    {
        private static readonly TimeSpan ConditionTimeout
            = TimeSpan.FromSeconds(30);

        private static void WaitForCondition(Func<bool> condition, TimeSpan? timeout = null) =>
            SpinWait.SpinUntil(condition, timeout ?? ConditionTimeout);

        [Fact(Skip = "AutoRefresh has a separate concurrency bug; tracked separately")]
        public async Task AutoRefreshThenFilter_ConcurrentAddsAndPropertyActivation_AllItemsObserved()
        {
            // One adder thread sequentially adds items to the cache while a single flipper thread
            // concurrently sets each item's Activated to true. Final filter contents must include
            // every item (every item ends Activated=true).
            //
            // KeyedActivable's setter only raises PropertyChanged on actual value change, so a
            // dropped false->true transition is unrecoverable.
            //
            // The race lives in AutoRefresh's internal Publish multicast: Sub 1 (Filter path)
            // receives the Add and reads the property before Sub 2 (MergeMany) subscribes the
            // per-item refresh handler. A concurrent flip landing in that gap is dropped. This
            // is not a WhenPropertyChanged issue: AutoRefresh calls WhenPropertyChanged with
            // notifyInitial=false, so the per-item subscribe attaches the handler immediately
            // and has no internal race window.
            const int iterations = 100;
            const int itemCount = 200;

            for (var iter = 0; iter < iterations; iter++)
            {
                using var cache = new SourceCache<KeyedActivable, int>(x => x.Id);
                var items = Enumerable.Range(0, itemCount).Select(i => new KeyedActivable(i)).ToList();

                using var results = cache.Connect()
                    .AutoRefresh(x => x.Activated)
                    .Filter(x => x.Activated)
                    .AsAggregator();

                using var barrier = new Barrier(2);

                var adder = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    foreach (var item in items) cache.AddOrUpdate(item);
                });

                var flipper = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    foreach (var item in items) item.Activated = true;
                });

                await Task.WhenAll(adder, flipper).WaitAsync(ConditionTimeout);

                var expected = items.Select(x => x.Id).ToHashSet();
                WaitForCondition(() => results.Data.Keys.ToHashSet().SetEquals(expected));

                var actual = results.Data.Keys.ToHashSet();
                actual.Should().BeEquivalentTo(expected, $"iter {iter}: every item ends Activated=true and must appear in the filter (missing: {string.Join(",", expected.Except(actual))})");
                results.Error.Should().BeNull($"iter {iter}: pipeline must not error");
            }
        }

        [Fact(Skip = "AutoRefresh has a separate concurrency bug; tracked separately")]
        public async Task AutoRefreshThenFilter_DualSubscribers_AllItemsObserved()
        {
            // Two independent cache subscribers running on the ThreadPool:
            //   Sub 1 (mutator): on every Add change, flips item.Activated to true
            //   Sub 2 (filter chain): AutoRefresh + Filter (filter = Activated)
            // Items start with Activated=false (filtered out). The mutator flips every item, so
            // the final filter contents must include every item.
            //
            // Same root cause as the single-flipper variant above: AutoRefresh's internal Publish
            // multicasts the Add to the Filter path before MergeMany subscribes the per-item
            // refresh handler. The mutator's flip can land in that gap and be dropped.
            const int iterations = 100;
            const int itemCount = 200;

            for (var iter = 0; iter < iterations; iter++)
            {
                using var cache = new SourceCache<KeyedActivable, int>(x => x.Id);
                var items = Enumerable.Range(0, itemCount).Select(i => new KeyedActivable(i)).ToList();

                using var mutator = cache.Connect()
                    .ObserveOn(TaskPoolScheduler.Default)
                    .Subscribe(changes =>
                    {
                        foreach (var change in changes)
                        {
                            if (change.Reason == ChangeReason.Add)
                            {
                                change.Current.Activated = true;
                            }
                        }
                    });

                using var results = cache.Connect()
                    .ObserveOn(TaskPoolScheduler.Default)
                    .AutoRefresh(x => x.Activated)
                    .Filter(x => x.Activated)
                    .AsAggregator();

                foreach (var item in items) cache.AddOrUpdate(item);

                var expected = items.Select(x => x.Id).ToHashSet();
                WaitForCondition(() => results.Data.Keys.ToHashSet().SetEquals(expected));

                var actual = results.Data.Keys.ToHashSet();
                actual.Should().BeEquivalentTo(expected, $"iter {iter}: every item was flipped to Activated=true by the mutator and must appear in the filter (missing: {string.Join(",", expected.Except(actual))})");
                results.Error.Should().BeNull($"iter {iter}: pipeline must not error");
            }
        }
    }
}
