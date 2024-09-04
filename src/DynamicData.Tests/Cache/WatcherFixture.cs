#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Experimental;
using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

#endregion

namespace DynamicData.Tests.Cache;

public class WatcherFixture : IDisposable
{
    private readonly IDisposable _cleanUp;

    private readonly ChangeSetAggregator<SelfObservingPerson, string> _results;

    private readonly TestScheduler _scheduler = new();

    private readonly ISourceCache<Person, string> _source;

    private readonly IWatcher<Person, string> _watcher;

    public WatcherFixture()
    {
        _scheduler = new TestScheduler();
        _source = new SourceCache<Person, string>(p => p.Key);
        _watcher = _source.Connect().AsWatcher(_scheduler);

        _results = new ChangeSetAggregator<SelfObservingPerson, string>(_source.Connect().Transform(p => new SelfObservingPerson(_watcher.Watch(p.Key).Select(w => w.Current))).DisposeMany());

        _cleanUp = Disposable.Create(
            () =>
            {
                _results.Dispose();
                _source.Dispose();
                _watcher.Dispose();
            });
    }

    [Fact]
    public void AddNew()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
        var result = _results.Data.Items[0];
        result.UpdateCount.Should().Be(1, "Person should have received 1 update");
        result.Completed.Should().Be(false, "Person should have received 1 update");
    }

    public void Dispose()
    {
        _cleanUp.Dispose();
        _results.Dispose();
        _source.Dispose();
        _watcher.Dispose();
    }

    [Fact]
    public void Remove()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
        _source.Remove(person.Key);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(11).Ticks);
        _results.Messages.Count.Should().Be(2, "Should be 1 updates");
        _results.Data.Count.Should().Be(0, "Should be 0 item in the cache");

        var secondResult = _results.Messages[1].First();
        secondResult.Current.UpdateCount.Should().Be(1, "Second Person should have received 1 update");
        secondResult.Current.Completed.Should().Be(true, "Second person  should have received 1 update");
    }

    [Fact]
    public void Update()
    {
        var first = new Person("Adult1", 50);
        var second = new Person("Adult1", 51);
        _source.AddOrUpdate(first);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
        _source.AddOrUpdate(second);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(10).Ticks);
        _results.Messages.Count.Should().Be(2, "Should be 1 updates");
        _results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

        var secondResult = _results.Messages[1].First();
        secondResult.Previous.Value.UpdateCount.Should().Be(1, "Second Person should have received 1 update");
        secondResult.Previous.Value.Completed.Should().Be(true, "Second person  should have received 1 update");
    }

    [Fact]
    public void Watch()
    {
        var person = new Person("Adult1", 50);
        _source.AddOrUpdate(person);

        var result = new List<Change<Person, string>>(3);
        var watch = _watcher.Watch("Adult1").Subscribe(result.Add);

        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(50).Ticks);
        result.Count.Should().Be(1, "Should be 1 updates");
        result[0].Current.Should().Be(person, "Should be 1 item in the cache");

        _source.Edit(updater => updater.Remove(("Adult1")));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);

        watch.Dispose();
    }

    [Fact]
    public void WatchMany()
    {
        _source.AddOrUpdate(new Person("Adult1", 50));

        var result = new List<Change<Person, string>>(3);
        var watch1 = _watcher.Watch("Adult1").Subscribe(result.Add);
        var watch2 = _watcher.Watch("Adult1").Subscribe(result.Add);
        var watch3 = _watcher.Watch("Adult1").Subscribe(result.Add);
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(100).Ticks);

        result.Count.Should().Be(3, "Should be 3 updates");
        foreach (var update in result)
        {
            update.Reason.Should().Be(ChangeReason.Add, "Change reason should be add");
        }

        result.Clear();

        _source.AddOrUpdate(new Person("Adult1", 51));
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);
        result.Count.Should().Be(3, "Should be 3 updates");
        foreach (var update in result)
        {
            update.Reason.Should().Be(ChangeReason.Update, "Change reason should be add");
        }

        result.Clear();

        _source.Remove("Adult1");
        _scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);
        result.Count.Should().Be(3, "Should be 3 updates");
        foreach (var update in result)
        {
            update.Reason.Should().Be(ChangeReason.Remove, "Change reason should be add");
        }

        result.Clear();

        watch1.Dispose();
        watch2.Dispose();
        watch3.Dispose();
    }
}
