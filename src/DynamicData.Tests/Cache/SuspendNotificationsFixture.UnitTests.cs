using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using FluentAssertions;
using Xunit;

using DynamicData.Kernel;

namespace DynamicData.Tests.Cache;

public static partial class SuspendNotificationsFixture
{
    public sealed class UnitTests
        : IDisposable
    {
        private readonly SourceCache<int, int> _source = new(static x => x);

        private readonly ChangeSetAggregator<int, int> _results;

        private readonly List<int> _countChangeHistory = [];

        private readonly IDisposable _countChangeSubscription;

        public UnitTests()
        {
            _results = _source.Connect().AsAggregator();
            _countChangeSubscription = _source.CountChanged.Do(_countChangeHistory.Add).Subscribe();
        }

        [Fact]
        public void NotificationsCanBeSuspended()
        {
            // Arrange
            using var suspend = _source.SuspendNotifications();

            // Act
            _source.AddOrUpdate(1);

            // Assert
            _results.Messages.Count.Should().Be(0, "Should have no item updates");
            _results.Data.Count.Should().Be(0, "Should not receive data after suspend");
            _results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void SuspendingNotificationsDoesNotImpactPreview()
        {
            // Arrange
            using var previewResults = _source.Preview().AsAggregator();
            using var suspend = _source.SuspendNotifications();

            // Act
            _source.AddOrUpdate(1);

            // Assert
            previewResults.Messages.Count.Should().Be(1, "should have received a message in Preview");
            _results.Messages.Count.Should().Be(0, "should not have gotten any updates");
            _results.Data.Count.Should().Be(0, "should not receive data after suspend");
            _results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void SuspendingNotificationsPreventsWatch()
        {
            // Arrange
            var gotData = false;
            using var suspend = _source.SuspendNotifications();
            using var sub = _source.Watch(1).Do(_ => gotData = true).Subscribe();

            // Act
            _source.AddOrUpdate(1);

            // Assert
            gotData.Should().BeFalse("Should not have received data after suspend");
            _results.Messages.Count.Should().Be(0, "Should have no item updates");
            _results.Data.Count.Should().Be(0, "Should not receive data after suspend");
            _results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void NotificationsCanBeResumed()
        {
            // Arrange
            {
                using var suspend = _source.SuspendNotifications();
            }

            // Act
            Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);

            // Assert
            _results.Messages.Count.Should().Be(37, "Should receive updates after resume");
            _results.Data.Count.Should().Be(37, "Should receive data after resume");
            _results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void ExistingDataNotEmittedWhileSuspended()
        {
            // Arrange
            var suspend = _source.SuspendNotifications();
            Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);

            // Act
            using var results = _source.Connect().AsAggregator();

            // Assert
            results.Messages.Count.Should().Be(0, "Should have no item updates");
            results.Data.Count.Should().Be(0, "Should not receive data after suspend");
            results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void ExistingDataNotEmittedViaWatchUntilResumed()
        {
            // Arrange
            var gotData = false;
            var suspend = _source.SuspendNotifications();
            Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);
            using var sub = _source.Watch(1).Do(_ => gotData = true).Subscribe();

            // Act
            suspend.Dispose();

            // Assert
            gotData.Should().BeTrue("should have received a notice after the suspend was released");
        }

        [Fact]
        public void ExistingDataNotEmittedUntilResumed()
        {
            // Arrange
            var suspend = _source.SuspendNotifications();
            Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);
            using var results = _source.Connect().AsAggregator();

            // Act
            suspend.Dispose();

            // Assert
            results.Messages.Count.Should().Be(1, "Should receive updates after resume");
            results.Data.Count.Should().Be(37, "Should receive data after resume");
            results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void ExistingAndNewDataEmittedAsASingleChangesetOnResume()
        {
            // Arrange
            var suspend = _source.SuspendNotifications();
            Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);
            using var results = _source.Connect().AsAggregator();
            Enumerable.Range(101, 37).ForEach(_source.AddOrUpdate);

            // Act
            suspend.Dispose();

            // Assert
            results.Messages.Count.Should().Be(1, "Should receive single changeset on resume");
            results.Data.Count.Should().Be(37 * 2, "Should receive data after resume");
            _results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void PendingNotificationsEmittedAsSingleChangeSetOnResume()
        {
            // Arrange
            var suspend = _source.SuspendNotifications();
            Enumerable.Range(1, 37).ForEach(_source.AddOrUpdate);
            _source.RemoveKey(1);

            // Act
            suspend.Dispose();

            // Assert
            _results.Data.Count.Should().Be(36, "Should receive data after resume");
            _results.Messages.Count.Should().Be(1, "Should receive single changeset on resume");
            _results.Messages[0].Adds.Should().Be(37, "Should have 37 adds");
            _results.Messages[0].Removes.Should().Be(1, "Should show the remove");
            _results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void MultipleSuspendsAreCumulative()
        {
            // Arrange
            var suspend = _source.SuspendNotifications();
            using var suspend2 = _source.SuspendNotifications();
            _source.AddOrUpdate(1);

            // Act
            suspend.Dispose();

            // Assert
            _results.Messages.Count.Should().Be(0, "Should have no item updates");
            _results.Data.Count.Should().Be(0, "Should not receive data after suspend");
            _results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void MultipleSuspendsCanBeResumed()
        {
            // Arrange
            var suspend = _source.SuspendNotifications();
            var suspend2 = _source.SuspendNotifications();
            _source.AddOrUpdate(1);
            suspend.Dispose();

            // Act
            suspend2.Dispose();

            // Assert
            _results.Messages.Count.Should().Be(1, "Should receive updates after resume");
            _results.Data.Count.Should().Be(1, "Should receive data after resume");
            _results.IsCompleted.Should().BeFalse("IsCompleted should not have fired");
        }

        [Fact]
        public void OnCompletedFiresIfCacheDisposedWhileSuspended()
        {
            // Arrange
            using var suspend = _source.SuspendNotifications();
            using var results = _source.Connect().AsAggregator();
            Enumerable.Range(101, 37).ForEach(_source.AddOrUpdate);

            // Act
            _source.Dispose();

            // Assert
            results.IsCompleted.Should().BeTrue("IsCompleted should fire even if Notifications are suspended");
            results.Messages.Count.Should().Be(0, "Shouldn't receive any Changesets");
            results.Data.Count.Should().Be(0, "Shouldn't receive any Data");
        }

        [Fact]
        public void CountNotificationsCanBeSuspended()
        {
            // Arrange
            using var suspend = _source.SuspendCount();

            // Act
            _source.AddOrUpdate(1);

            // Assert
            _countChangeHistory.Count.Should().Be(1, "Should Not receive count updates");
            _countChangeHistory[0].Should().Be(0, "Should have only received the empty list");
        }

        [Fact]
        public void CountNotificationsCanBeResumed()
        {
            // Arrange
            {
                using var suspend = _source.SuspendCount();
            }

            // Act
            _source.AddOrUpdate(1);

            // Assert
            _countChangeHistory.Count.Should().Be(2, "Should receive count updates");
            _countChangeHistory[0].Should().Be(0, "Should have received the empty list");
            _countChangeHistory[1].Should().Be(1, "Should have received the updated count");
        }

        [Fact]
        public void CountChangedAlwaysStartsWithInitialEvenWhenSuspended()
        {
            // Arrange
            _source.AddOrUpdate(Enumerable.Range(1, 50));
            var countChangeHistory = new List<int>();
            using var suspend = _source.SuspendCount();
            using var countChangeSubscription = _source.CountChanged.Do(countChangeHistory.Add).Subscribe();

            // Act
            Enumerable.Range(100, 50).ForEach(_source.AddOrUpdate);

            // Assert
            countChangeHistory.Count.Should().Be(1, "Should receive initial value");
            countChangeHistory[0].Should().Be(50, "Should have received the correct initial value");
        }

        [Fact]
        public void PendingCountNotificationsEmittedOnResume()
        {
            // Arrange
            var suspend = _source.SuspendCount();
            _source.AddOrUpdate(1);
            _source.AddOrUpdate(2);
            _source.AddOrUpdate(3);

            // Act
            suspend.Dispose();

            // Assert
            _countChangeHistory.Count.Should().Be(2, "Should receive count updates");
            _countChangeHistory[0].Should().Be(0, "Should have received the initial 0 count");
            _countChangeHistory[1].Should().Be(3, "Should have received the updated count");
        }

        [Fact]
        public void MultipleCountSuspendsAreCumulative()
        {
            // Arrange
            var suspend = _source.SuspendCount();
            using var suspend2 = _source.SuspendCount();
            _source.AddOrUpdate(1);
            _source.AddOrUpdate(2);
            _source.AddOrUpdate(3);

            // Act
            suspend.Dispose();

            // Assert
            _countChangeHistory.Count.Should().Be(1, "Should Not receive count updates");
            _countChangeHistory[0].Should().Be(0, "Should have only received the empty list");
        }

        [Fact]
        public void MultipleCountSuspendsCanBeResumed()
        {
            // Arrange
            var suspend = _source.SuspendCount();
            var suspend2 = _source.SuspendCount();
            _source.AddOrUpdate(1);
            _source.AddOrUpdate(2);
            _source.AddOrUpdate(3);
            suspend.Dispose();

            // Act
            suspend2.Dispose();

            // Assert
            _countChangeHistory.Count.Should().Be(2, "Should receive count updates");
            _countChangeHistory[0].Should().Be(0, "Should have received the initial 0 count");
            _countChangeHistory[1].Should().Be(3, "Should have received the updated count");
        }

        [Fact]
        public async Task SuspensionsAreThreadSafe()
        {
            // Arrange
            var suspend = _source.SuspendNotifications();
            var tasks = Enumerable.Range(1, 100).Select(x => Task.Run(() => _source.AddOrUpdate(x))).ToArray();
            await Task.WhenAll(tasks);

            // Act
            await Task.Run(suspend.Dispose);

            // Assert
            _results.Data.Count.Should().Be(100, "Should receive data after resume");
            _results.Messages.Count.Should().Be(1, "Should receive single changeset on resume");
            _results.Messages[0].Adds.Should().Be(100, "Should have 100 adds");
        }

        [Fact]
        public void ResumeThenReSuspendDeliversFirstBatchOnly()
        {
            // Forces the ordering: resume completes before re-suspend.
            // The deferred subscriber activates with the first batch snapshot,
            // then re-suspend holds the second batch until final resume.
            using var cache = new SourceCache<int, int>(static x => x);
            var dataSet1 = Enumerable.Range(0, 100).ToList();
            var dataSet2 = Enumerable.Range(1000, 100).ToList();
            var allData = dataSet1.Concat(dataSet2).ToList();

            var suspend1 = cache.SuspendNotifications();
            cache.AddOrUpdate(dataSet1);

            using var results = cache.Connect().AsAggregator();
            results.Messages.Count.Should().Be(0, "no messages during suspension");

            // Resume first — subscriber activates
            suspend1.Dispose();

            results.Messages.Count.Should().Be(1, "exactly one message after resume");
            results.Messages[0].Adds.Should().Be(dataSet1.Count, $"snapshot should have {dataSet1.Count} adds");
            results.Messages[0].Removes.Should().Be(0, "no removes");
            results.Messages[0].Updates.Should().Be(0, "no updates");
            results.Messages[0].Select(x => x.Key).Should().Equal(dataSet1, "snapshot should contain first batch keys");

            // Re-suspend, write second batch
            var suspend2 = cache.SuspendNotifications();
            cache.AddOrUpdate(dataSet2);

            results.Messages.Count.Should().Be(1, "still one message — second batch held by suspension");
            results.Summary.Overall.Adds.Should().Be(dataSet1.Count, $"still {dataSet1.Count} adds total");

            // Final resume
            suspend2.Dispose();

            results.Messages.Count.Should().Be(2, "two messages total");
            results.Messages[1].Adds.Should().Be(dataSet2.Count, $"second message has {dataSet2.Count} adds");
            results.Messages[1].Removes.Should().Be(0, "no removes in second message");
            results.Messages[1].Updates.Should().Be(0, "no updates in second message");
            results.Messages[1].Select(x => x.Key).Should().Equal(dataSet2, "second message should contain second batch keys");

            results.Summary.Overall.Adds.Should().Be(allData.Count, $"exactly {allData.Count} adds total");
            results.Summary.Overall.Removes.Should().Be(0, "no removes");
            results.Data.Count.Should().Be(allData.Count, $"{allData.Count} items in final state");
            results.Error.Should().BeNull();
            results.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void ReSuspendThenResumeDeliversAllInSingleBatch()
        {
            // Forces the ordering: re-suspend before resume.
            // Suspend count goes 1→2→1, no resume signal fires.
            // Both batches accumulate and arrive as a single changeset on final resume.
            using var cache = new SourceCache<int, int>(static x => x);
            var dataSet1 = Enumerable.Range(0, 100).ToList();
            var dataSet2 = Enumerable.Range(1000, 100).ToList();
            var allData = dataSet1.Concat(dataSet2).ToList();

            var suspend1 = cache.SuspendNotifications();
            cache.AddOrUpdate(dataSet1);

            using var results = cache.Connect().AsAggregator();
            results.Messages.Count.Should().Be(0, "no messages during suspension");

            // Re-suspend first — count goes 1→2
            var suspend2 = cache.SuspendNotifications();

            // Resume first suspend — count goes 2→1, still suspended
            suspend1.Dispose();

            results.Messages.Count.Should().Be(0, "no messages — still suspended (count=1)");
            results.Summary.Overall.Adds.Should().Be(0, "no adds — still suspended");

            // Write second batch while still suspended
            cache.AddOrUpdate(dataSet2);

            results.Messages.Count.Should().Be(0, "still no messages");

            // Final resume — count goes 1→0
            suspend2.Dispose();

            results.Messages.Count.Should().Be(1, "single message with all data");
            results.Messages[0].Adds.Should().Be(allData.Count, $"all {allData.Count} items in one changeset");
            results.Messages[0].Removes.Should().Be(0, "no removes");
            results.Messages[0].Updates.Should().Be(0, "no updates");
            results.Messages[0].Select(c => c.Key).OrderBy(k => k).Should().Equal(allData, "should contain both batches in order");

            results.Summary.Overall.Adds.Should().Be(allData.Count, $"exactly {allData.Count} adds total");
            results.Summary.Overall.Removes.Should().Be(0, "no removes");
            results.Summary.Overall.Updates.Should().Be(0, "no updates");
            results.Data.Count.Should().Be(allData.Count, $"{allData.Count} items in final state");
            results.Error.Should().BeNull();
            results.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void OnCompletedFiresIfCacheDisposedAfterConnectingWhileSuspended()
        {
            // A connection made while suspended is deferred until the suspension lifts. Once it
            // activates it must behave exactly like any other subscriber, including terminating
            // when the source does. Previously the deferral dropped the completion, leaving the
            // subscriber alive forever.
            var suspend = _source.SuspendNotifications();
            using var results = _source.Connect().AsAggregator();
            Enumerable.Range(101, 37).ForEach(_source.AddOrUpdate);

            // Act
            suspend.Dispose();
            _source.AddOrUpdate(1000);
            _source.Dispose();

            // Assert
            results.IsCompleted.Should().BeTrue("a connection deferred by a suspension should still complete when the source does");
            results.Error.Should().BeNull("no error should have occurred");
            results.Data.Count.Should().Be(38, "all data written before disposal should have arrived");
        }

        [Fact]
        public void OnErrorFiresIfCacheFailsAfterConnectingWhileSuspended()
        {
            // The same applies to the error case: a deferred connection that has activated must
            // still see a failure of the source.
            using var source = new Subject<IChangeSet<int, int>>();
            using var cache = new ObservableCache<int, int>(source);

            var suspend = cache.SuspendNotifications();
            using var results = cache.Connect().AsAggregator();
            source.OnNext(new ChangeSet<int, int> { new(ChangeReason.Add, 1, 1) });

            // Act
            suspend.Dispose();
            var expectedError = new Exception("Test Exception");
            source.OnError(expectedError);

            // Assert
            results.Error.Should().Be(expectedError, "a connection deferred by a suspension should still see the source fail");
            results.Data.Count.Should().Be(1, "the data written before the failure should have arrived");
        }

        [Fact]
        public void OnCompletedFiresIfCacheDisposedAfterWatchingWhileSuspended()
        {
            // Watch() defers the same way Connect() does, and has the same obligation.
            var suspend = _source.SuspendNotifications();
            var isCompleted = false;
            using var subscription = _source.Watch(1).Subscribe(static _ => { }, () => isCompleted = true);
            _source.AddOrUpdate(1);

            // Act
            suspend.Dispose();
            _source.Dispose();

            // Assert
            isCompleted.Should().BeTrue("a watch deferred by a suspension should still complete when the source does");
        }

        public void Dispose()
        {
            _source.Dispose();
            _results.Dispose();
            _countChangeSubscription.Dispose();
        }
    }
}
