using DynamicData.Experimental;

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
    public void SubscribesToTheSourceOnce()
    {
        var subscriptions = 0;
        using var source = new Subject<IChangeSet<Person, string>>();

        using var watcher = Observable.Defer(() => { subscriptions++; return source; }).AsWatcher(_scheduler);

        subscriptions.Should().Be(1, "a cold source runs once for every subscription");
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
    public void WatchCompletesAfterPendingChangesWhenTheSourceCompletes()
    {
        using var source = new TestSourceCache<Person, string>(p => p.Key);
        using var watcher = source.Connect().AsWatcher(_scheduler);

        var received = new List<Change<Person, string>>();
        var receivedAtCompletion = 0;
        var isCompleted = false;
        using var subscription = watcher.Watch("Adult1").Subscribe(received.Add, () => { isCompleted = true; receivedAtCompletion = received.Count; });

        source.AddOrUpdate(new Person("Adult1", 50));
        source.Complete();
        _scheduler.Start();

        isCompleted.Should().BeTrue("the source completed");
        receivedAtCompletion.Should().Be(1, "the change was scheduled ahead of the completion, so it should arrive first");
    }

    [Fact]
    public void WatchFailsWhenTheSourceFails()
    {
        using var source = new TestSourceCache<Person, string>(p => p.Key);
        using var watcher = source.Connect().AsWatcher(_scheduler);

        var actualError = default(Exception);
        var isCompleted = false;
        using var subscription = watcher.Watch("Adult1").Subscribe(static _ => { }, error => actualError = error, () => isCompleted = true);

        var expectedError = new Exception("Test Exception");
        source.SetError(expectedError);
        _scheduler.Start();

        actualError.Should().BeSameAs(expectedError, "a watch should be told when the source fails");
        isCompleted.Should().BeFalse("the source failed, it did not complete");
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

    [Fact]
    public void WatchStartedAfterTheSourceCompletedReceivesTheCurrentValueThenCompletes()
    {
        using var source = new TestSourceCache<Person, string>(p => p.Key);
        using var watcher = source.Connect().AsWatcher(_scheduler);

        var person = new Person("Adult1", 50);
        source.AddOrUpdate(person);
        source.Complete();
        _scheduler.Start();

        var received = new List<Change<Person, string>>();
        var isCompleted = false;
        using var subscription = watcher.Watch(person.Key).Subscribe(received.Add, () => isCompleted = true);

        received.Should().ContainSingle("the watch starts with the current value").Which.Current.Should().Be(person);
        isCompleted.Should().BeTrue("a watch on a finished source can never receive anything more");
    }

    [Fact]
    public void WatchJoiningAKeyWhoseWatchesHaveEndedReceivesTheLastValueThenEnds()
    {
        using var source = new TestSourceCache<Person, string>(p => p.Key);
        using var watcher = source.Connect().AsWatcher(_scheduler);

        var person = new Person("Adult1", 50);
        source.AddOrUpdate(person);
        using var first = watcher.Watch(person.Key).Subscribe(static _ => { }, static _ => { });

        var expectedError = new Exception("Test Exception");
        source.SetError(expectedError);
        _scheduler.Start();

        var received = new List<Change<Person, string>>();
        var actualError = default(Exception);
        using var second = watcher.Watch(person.Key).Subscribe(received.Add, error => actualError = error);

        received.Should().ContainSingle("the watch starts with the last value").Which.Current.Should().Be(person);
        actualError.Should().BeSameAs(expectedError, "the key's watches have already failed with the source");
    }

    [Fact]
    public void WatchStartedWhileTheSourceIsDeliveringAChangeReceivesItOnce()
    {
        // A hot source hands each change to its subscribers one after another, so a watch can start part way through.
        var observers = new List<IObserver<IChangeSet<Person, string>>>();
        using var watcher = Observable.Create<IChangeSet<Person, string>>(observer => { observers.Add(observer); return Disposable.Empty; }).AsWatcher(_scheduler);

        var person = new Person("Adult1", 50);
        var changes = new ChangeSet<Person, string> { new(ChangeReason.Add, person.Key, person) };
        observers[0].OnNext(changes);

        var received = new List<Change<Person, string>>();
        using var subscription = watcher.Watch(person.Key).Subscribe(received.Add);

        foreach (var observer in observers.Skip(1))
        {
            observer.OnNext(changes);
        }

        _scheduler.Start();

        received.Should().ContainSingle("the person was added once").Which.Current.Should().Be(person);
    }
}
