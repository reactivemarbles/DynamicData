using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData.Kernel;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public sealed class SuspendNotificationsFixture : IDisposable
{
    private readonly SourceCache<int, int> _source = new(static x => x);

    private readonly ChangeSetAggregator<int, int> _results;

    private readonly List<int> _countChangeHistory = [];

    private readonly IDisposable _countChangeSubscription;

    public SuspendNotificationsFixture()
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

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
        _countChangeSubscription.Dispose();
    }
}
