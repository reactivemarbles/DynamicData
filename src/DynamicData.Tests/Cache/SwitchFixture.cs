namespace DynamicData.Tests.Cache;

public class SwitchFixture
{
    [Fact]
    public void ClearsForNewSource()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();

        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        results.Data.Count.Should().Be(100);

        var newSource = new SourceCache<Person, string>(p => p.Name);
        switchable.OnNext(newSource);

        results.Data.Count.Should().Be(0);

        newSource.AddOrUpdate(inital);
        results.Data.Count.Should().Be(100);

        var nextUpdates = Enumerable.Range(101, 100).Select(i => new Person("Person" + i, i)).ToArray();
        newSource.AddOrUpdate(nextUpdates);
        results.Data.Count.Should().Be(200);
    }

    [Fact]
    public void PoulatesFirstSource()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();

        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        results.Data.Count.Should().Be(100);
    }

    [Fact]
    public void PropagatesOuterErrors()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();

        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        var error = new Exception("Test");
        switchable.OnError(error);

        results.Error.Should().Be(error);
    }

    [Fact]
    public void PropagatesInnerErrors()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<Person, string>>>(source.Connect());
        var results = switchable.Switch().AsAggregator();

        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        using var source2 = new BehaviorSubject<IChangeSet<Person, string>>(ChangeSet<Person, string>.Empty);

        switchable.OnNext(source2);

        var error = new Exception("Test");
        source2.OnError(error);

        results.Error.Should().Be(error);
    }

    [Fact]
    public void CompletesWhenSourcesAndInnerComplete()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<Person, string>>>(source.Connect());
        using var results = switchable.Switch().AsAggregator();

        source.AddOrUpdate(Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray());

        switchable.OnCompleted();
        results.IsCompleted.Should().BeFalse("the inner sequence is still running");

        source.Dispose();

        results.IsCompleted.Should().BeTrue("both the sources and the inner sequence have completed");
        results.Error.Should().BeNull();
        results.Data.Count.Should().Be(100, "all data should have been received before completion");
    }

    [Fact]
    public void DoesNotCompleteWhileInnerIsStillRunning()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<Person, string>>>(source.Connect());
        using var results = switchable.Switch().AsAggregator();

        switchable.OnCompleted();
        source.AddOrUpdate(new Person("Person1", 1));

        results.IsCompleted.Should().BeFalse("the inner sequence has not completed");
        results.Data.Count.Should().Be(1, "changes should still flow after the sources sequence completes");
    }

    [Fact]
    public void DoesNotCompleteWhenOnlyASupersededInnerCompletes()
    {
        using var first = new SourceCache<Person, string>(p => p.Name);
        using var second = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<Person, string>>>(first.Connect());
        using var results = switchable.Switch().AsAggregator();

        switchable.OnNext(second.Connect());
        switchable.OnCompleted();

        first.Dispose();

        results.IsCompleted.Should().BeFalse("the superseded sequence is not the current one");

        second.AddOrUpdate(new Person("Person1", 1));
        results.Data.Count.Should().Be(1, "the current sequence should still be delivering");

        second.Dispose();
        results.IsCompleted.Should().BeTrue("the current sequence has now completed");
    }

    [Fact]
    public void CompletesWhenSourcesAndInnerCompleteSynchronously()
    {
        using var results = Observable.Return(Observable.Empty<IChangeSet<Person, string>>()).Switch().AsAggregator();

        results.IsCompleted.Should().BeTrue("everything completed during subscription");
        results.Error.Should().BeNull();
    }

    [Fact]
    public void DeliversChangesEmittedBeforeSynchronousCompletion()
    {
        var change = new ChangeSet<Person, string> { new(ChangeReason.Add, "Person1", new Person("Person1", 1)) };
        using var results = Observable.Return(Observable.Return((IChangeSet<Person, string>)change)).Switch().AsAggregator();

        results.Data.Count.Should().Be(1, "changes emitted before a synchronous completion must not be lost");
        results.IsCompleted.Should().BeTrue("the source completed");
        results.Error.Should().BeNull();
    }

    [Fact]
    public void IgnoresChangesFromASupersededSource()
    {
        using var first = new Subject<IChangeSet<Person, string>>();
        using var second = new Subject<IChangeSet<Person, string>>();
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<Person, string>>>(first);
        using var results = switchable.Switch().AsAggregator();

        first.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "Person1", new Person("Person1", 1)) });
        results.Data.Count.Should().Be(1);

        switchable.OnNext(second);
        results.Data.Count.Should().Be(0, "moving to a new source drops what the previous one contributed");

        first.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "Person2", new Person("Person2", 2)) });

        results.Data.Count.Should().Be(0, "a superseded source must not be able to write into the result");
        results.Error.Should().BeNull();
    }

    [Fact]
    public void PropagatesInnerErrorsRaisedSynchronously()
    {
        var error = new Exception("Test");
        using var results = Observable.Return(Observable.Throw<IChangeSet<Person, string>>(error)).Switch().AsAggregator();

        results.Error.Should().Be(error, "the error was raised during subscription");
    }

    [Fact]
    public void DoesNotHoldALockWhileDeliveringDownstream()
    {
        // Observable.Switch holds its gate for the whole of downstream delivery, which is the shape that
        // deadlocks when a pipeline crosses into another cache. Delivery has to go through the queue, which
        // enqueues and returns, so a producer is never held up by whatever a subscriber is doing.
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        using var first = new Subject<IChangeSet<Person, string>>();

        using var isDelivering = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        using var subscription = switchable.Switch().Subscribe(_ =>
        {
            isDelivering.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        });

        switchable.OnNext(first);

        var deliverer = new Thread(() => first.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "a", new Person("a", 1)) })) { IsBackground = true };
        deliverer.Start();

        isDelivering.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the subscriber should have been handed the change");

        var producer = new Thread(() => switchable.OnNext(new Subject<IChangeSet<Person, string>>())) { IsBackground = true };
        producer.Start();

        var producerFinished = producer.Join(TimeSpan.FromSeconds(2));

        release.Set();
        deliverer.Join(TimeSpan.FromSeconds(10));
        producer.Join(TimeSpan.FromSeconds(10));

        producerFinished.Should().BeTrue("writing to the source must not block while a subscriber holds onto a notification");
    }

    [Fact]
    public void IgnoresErrorsFromASupersededSource()
    {
        // Switching away from a source means anything it produces afterwards belongs to a source that
        // is no longer selected, and that includes its failures. Ordinarily disposal stops a
        // superseded source being heard from again, but disposal cannot reach a notification that is
        // already in flight, so the operator has to discard it on arrival. The raw observable hands
        // back the observer directly, which is how that in-flight failure is reproduced here without
        // needing a race to land.
        var supersededObserver = default(IObserver<IChangeSet<Person, string>>);
        var superseded = RawAnonymousObservable.Create<IChangeSet<Person, string>>(observer =>
        {
            supersededObserver = observer;
            return Disposable.Empty;
        });

        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        using var current = new Subject<IChangeSet<Person, string>>();

        using var results = switchable.Switch().AsAggregator();

        switchable.OnNext(superseded);
        switchable.OnNext(current);

        supersededObserver.Should().NotBeNull("the superseded source should have been subscribed");
        supersededObserver!.OnError(new Exception("Test"));

        results.Error.Should().BeNull("the failed source had already been switched away from");

        current.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "a", new Person("a", 1)) });

        results.Data.Count.Should().Be(1, "the selected source should still be delivering");
    }
}
