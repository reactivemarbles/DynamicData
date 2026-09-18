using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class SwitchFixture
{
    [Test]
    public async Task ClearsForNewSource()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new ReactiveUI.Primitives.Signals.StateSignal<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();

        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        await Assert.That(results.Data.Count).IsEqualTo(100);

        var newSource = new SourceCache<Person, string>(p => p.Name);
        switchable.OnNext(newSource);

        await Assert.That(results.Data.Count).IsEqualTo(0);

        newSource.AddOrUpdate(inital);
        await Assert.That(results.Data.Count).IsEqualTo(100);

        var nextUpdates = Enumerable.Range(101, 100).Select(i => new Person("Person" + i, i)).ToArray();
        newSource.AddOrUpdate(nextUpdates);
        await Assert.That(results.Data.Count).IsEqualTo(200);
    }

    [Test]
    public async Task PoulatesFirstSource()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new ReactiveUI.Primitives.Signals.StateSignal<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();

        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        await Assert.That(results.Data.Count).IsEqualTo(100);
    }

    [Test]
    public async Task PropagatesOuterErrors()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new ReactiveUI.Primitives.Signals.StateSignal<ISourceCache<Person, string>>(source);
        var results = switchable.Switch().AsAggregator();

        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        var error = new Exception("Test");
        switchable.OnError(error);

        await Assert.That(results.Error).IsEqualTo(error);
    }

    [Test]
    public async Task PropagatesInnerErrors()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new ReactiveUI.Primitives.Signals.StateSignal<IObservable<IChangeSet<Person, string>>>(source.Connect());
        var results = switchable.Switch().AsAggregator();

        var inital = Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray();
        source.AddOrUpdate(inital);

        using var source2 = new ReactiveUI.Primitives.Signals.StateSignal<IChangeSet<Person, string>>(ChangeSet<Person, string>.Empty);

        switchable.OnNext(source2);

        var error = new Exception("Test");
        source2.OnError(error);

        await Assert.That(results.Error).IsEqualTo(error);
    }

    [Test]
    public async Task CompletesWhenSourcesAndInnerComplete()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new ReactiveUI.Primitives.Signals.StateSignal<IObservable<IChangeSet<Person, string>>>(source.Connect());
        using var results = switchable.Switch().AsAggregator();

        source.AddOrUpdate(Enumerable.Range(1, 100).Select(i => new Person("Person" + i, i)).ToArray());

        switchable.OnCompleted();
        await Assert.That(results.IsCompleted).IsFalse().Because("the inner sequence is still running");

        source.Dispose();

        await Assert.That(results.IsCompleted).IsTrue().Because("both the sources and the inner sequence have completed");
        await Assert.That(results.Error).IsNull();
        await Assert.That(results.Data.Count).IsEqualTo(100).Because("all data should have been received before completion");
    }

    [Test]
    public async Task DoesNotCompleteWhileInnerIsStillRunning()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new ReactiveUI.Primitives.Signals.StateSignal<IObservable<IChangeSet<Person, string>>>(source.Connect());
        using var results = switchable.Switch().AsAggregator();

        switchable.OnCompleted();
        source.AddOrUpdate(new Person("Person1", 1));

        await Assert.That(results.IsCompleted).IsFalse().Because("the inner sequence has not completed");
        await Assert.That(results.Data.Count).IsEqualTo(1).Because("changes should still flow after the sources sequence completes");
    }

    [Test]
    public async Task DoesNotCompleteWhenOnlyASupersededInnerCompletes()
    {
        using var first = new SourceCache<Person, string>(p => p.Name);
        using var second = new SourceCache<Person, string>(p => p.Name);
        using var switchable = new ReactiveUI.Primitives.Signals.StateSignal<IObservable<IChangeSet<Person, string>>>(first.Connect());
        using var results = switchable.Switch().AsAggregator();

        switchable.OnNext(second.Connect());
        switchable.OnCompleted();

        first.Dispose();

        await Assert.That(results.IsCompleted).IsFalse().Because("the superseded sequence is not the current one");

        second.AddOrUpdate(new Person("Person1", 1));
        await Assert.That(results.Data.Count).IsEqualTo(1).Because("the current sequence should still be delivering");

        second.Dispose();
        await Assert.That(results.IsCompleted).IsTrue().Because("the current sequence has now completed");
    }

    [Test]
    public async Task CompletesWhenSourcesAndInnerCompleteSynchronously()
    {
        using var results = Observable.Return(Observable.Empty<IChangeSet<Person, string>>()).Switch().AsAggregator();

        await Assert.That(results.IsCompleted).IsTrue().Because("everything completed during subscription");
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task DeliversChangesEmittedBeforeSynchronousCompletion()
    {
        var change = new ChangeSet<Person, string> { new(ChangeReason.Add, "Person1", new Person("Person1", 1)) };
        using var results = Observable.Return(Observable.Return((IChangeSet<Person, string>)change)).Switch().AsAggregator();

        await Assert.That(results.Data.Count).IsEqualTo(1).Because("changes emitted before a synchronous completion must not be lost");
        await Assert.That(results.IsCompleted).IsTrue().Because("the source completed");
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task IgnoresChangesFromASupersededSource()
    {
        using var first = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Person, string>>();
        using var second = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Person, string>>();
        using var switchable = new ReactiveUI.Primitives.Signals.StateSignal<IObservable<IChangeSet<Person, string>>>(first);
        using var results = switchable.Switch().AsAggregator();

        first.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "Person1", new Person("Person1", 1)) });
        await Assert.That(results.Data.Count).IsEqualTo(1);

        switchable.OnNext(second);
        await Assert.That(results.Data.Count).IsEqualTo(0).Because("moving to a new source drops what the previous one contributed");

        first.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "Person2", new Person("Person2", 2)) });

        await Assert.That(results.Data.Count).IsEqualTo(0).Because("a superseded source must not be able to write into the result");
        await Assert.That(results.Error).IsNull();
    }

    [Test]
    public async Task PropagatesInnerErrorsRaisedSynchronously()
    {
        var error = new Exception("Test");
        using var results = Observable.Return(Observable.Throw<IChangeSet<Person, string>>(error)).Switch().AsAggregator();

        await Assert.That(results.Error).IsEqualTo(error).Because("the error was raised during subscription");
    }

    [Test]
    public async Task DoesNotHoldALockWhileDeliveringDownstream()
    {
        // Observable.Switch holds its gate for the whole of downstream delivery, which is the shape that
        // deadlocks when a pipeline crosses into another cache. Delivery has to go through the queue, which
        // enqueues and returns, so a producer is never held up by whatever a subscriber is doing.
        using var switchable = new ReactiveUI.Primitives.Signals.Signal<IObservable<IChangeSet<Person, string>>>();
        using var first = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Person, string>>();

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

        await Assert.That(isDelivering.Wait(TimeSpan.FromSeconds(10))).IsTrue().Because("the subscriber should have been handed the change");

        var producer = new Thread(() => switchable.OnNext(new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Person, string>>())) { IsBackground = true };
        producer.Start();

        var producerFinished = producer.Join(TimeSpan.FromSeconds(2));

        release.Set();
        deliverer.Join(TimeSpan.FromSeconds(10));
        producer.Join(TimeSpan.FromSeconds(10));

        await Assert.That(producerFinished).IsTrue().Because("writing to the source must not block while a subscriber holds onto a notification");
    }

    [Test]
    public async Task IgnoresErrorsFromASupersededSource()
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

        using var switchable = new ReactiveUI.Primitives.Signals.Signal<IObservable<IChangeSet<Person, string>>>();
        using var current = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Person, string>>();

        using var results = switchable.Switch().AsAggregator();

        switchable.OnNext(superseded);
        switchable.OnNext(current);

        await Assert.That(supersededObserver).IsNotNull().Because("the superseded source should have been subscribed");
        supersededObserver!.OnError(new Exception("Test"));

        await Assert.That(results.Error).IsNull().Because("the failed source had already been switched away from");

        current.OnNext(new ChangeSet<Person, string> { new(ChangeReason.Add, "a", new Person("a", 1)) });

        await Assert.That(results.Data.Count).IsEqualTo(1).Because("the selected source should still be delivering");
    }
}
