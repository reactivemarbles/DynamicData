using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.List;

public class SwitchFixture : IDisposable
{
    private readonly ChangeSetAggregator<int> _results;

    private readonly ISourceList<int> _source;

    private readonly ISubject<ISourceList<int>> _switchable;

    public SwitchFixture()
    {
        _source = new SourceList<int>();
        _switchable = new BehaviorSubject<ISourceList<int>>(_source);
        _results = _switchable.Switch().AsAggregator();
    }

    [Fact]
    public void ClearsForNewSource()
    {
        var inital = Enumerable.Range(1, 100).ToArray();
        _source.AddRange(inital);

        _results.Data.Count.Should().Be(100);

        var newSource = new SourceList<int>();
        _switchable.OnNext(newSource);

        _results.Data.Count.Should().Be(0);

        newSource.AddRange(inital);
        _results.Data.Count.Should().Be(100);

        var nextUpdates = Enumerable.Range(100, 100).ToArray();
        newSource.AddRange(nextUpdates);
        _results.Data.Count.Should().Be(200);
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }

    [Fact]
    public void PoulatesFirstSource()
    {
        var inital = Enumerable.Range(1, 100).ToArray();
        _source.AddRange(inital);

        _results.Data.Count.Should().Be(100);

        inital.Should().BeEquivalentTo(_source.Items);
    }

    [Fact]
    public void PropagatesOuterErrors()
    {
        using var source = new SourceList<int>();
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<int>>>(source.Connect());
        using var results = switchable.Switch().AsAggregator();

        source.AddRange(Enumerable.Range(1, 100));

        var error = new Exception("Test");
        switchable.OnError(error);

        results.Exception.Should().Be(error);
    }

    [Fact]
    public void PropagatesInnerErrors()
    {
        using var source = new SourceList<int>();
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<int>>>(source.Connect());
        using var results = switchable.Switch().AsAggregator();

        source.AddRange(Enumerable.Range(1, 100));

        using var source2 = new Subject<IChangeSet<int>>();
        switchable.OnNext(source2);

        var error = new Exception("Test");
        source2.OnError(error);

        results.Exception.Should().Be(error);
    }

    [Fact]
    public void CompletesWhenSourcesAndInnerComplete()
    {
        using var source = new SourceList<int>();
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<int>>>(source.Connect());
        using var results = switchable.Switch().AsAggregator();

        source.AddRange(Enumerable.Range(1, 100));

        switchable.OnCompleted();
        results.IsCompleted.Should().BeFalse("the inner sequence is still running");

        source.Dispose();

        results.IsCompleted.Should().BeTrue("both the sources and the inner sequence have completed");
        results.Exception.Should().BeNull();
        results.Data.Count.Should().Be(100, "all data should have been received before completion");
    }

    [Fact]
    public void DoesNotCompleteWhileInnerIsStillRunning()
    {
        using var source = new SourceList<int>();
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<int>>>(source.Connect());
        using var results = switchable.Switch().AsAggregator();

        switchable.OnCompleted();
        source.Add(1);

        results.IsCompleted.Should().BeFalse("the inner sequence has not completed");
        results.Data.Count.Should().Be(1, "changes should still flow after the sources sequence completes");
    }

    [Fact]
    public void DoesNotCompleteWhenOnlyASupersededInnerCompletes()
    {
        using var first = new SourceList<int>();
        using var second = new SourceList<int>();
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<int>>>(first.Connect());
        using var results = switchable.Switch().AsAggregator();

        switchable.OnNext(second.Connect());
        switchable.OnCompleted();

        first.Dispose();

        results.IsCompleted.Should().BeFalse("the superseded sequence is not the current one");

        second.Add(1);
        results.Data.Count.Should().Be(1, "the current sequence should still be delivering");

        second.Dispose();
        results.IsCompleted.Should().BeTrue("the current sequence has now completed");
    }

    [Fact]
    public void CompletesWhenSourcesAndInnerCompleteSynchronously()
    {
        using var results = Observable.Return(Observable.Empty<IChangeSet<int>>()).Switch().AsAggregator();

        results.IsCompleted.Should().BeTrue("everything completed during subscription");
        results.Exception.Should().BeNull();
    }

    [Fact]
    public void DeliversChangesEmittedBeforeSynchronousCompletion()
    {
        var change = new ChangeSet<int> { new(ListChangeReason.Add, 42) };
        using var results = Observable.Return(Observable.Return((IChangeSet<int>)change)).Switch().AsAggregator();

        results.Data.Count.Should().Be(1, "changes emitted before a synchronous completion must not be lost");
        results.IsCompleted.Should().BeTrue("the source completed");
        results.Exception.Should().BeNull();
    }

    [Fact]
    public void IgnoresChangesFromASupersededSource()
    {
        using var first = new Subject<IChangeSet<int>>();
        using var second = new Subject<IChangeSet<int>>();
        using var switchable = new BehaviorSubject<IObservable<IChangeSet<int>>>(first);
        using var results = switchable.Switch().AsAggregator();

        first.OnNext(new ChangeSet<int> { new(ListChangeReason.Add, 1) });
        results.Data.Count.Should().Be(1);

        switchable.OnNext(second);
        results.Data.Count.Should().Be(0, "moving to a new source drops what the previous one contributed");

        first.OnNext(new ChangeSet<int> { new(ListChangeReason.Add, 2) });

        results.Data.Count.Should().Be(0, "a superseded source must not be able to write into the result");
        results.Exception.Should().BeNull();
    }

    [Fact]
    public void PropagatesInnerErrorsRaisedSynchronously()
    {
        var error = new Exception("Test");
        using var results = Observable.Return(Observable.Throw<IChangeSet<int>>(error)).Switch().AsAggregator();

        results.Exception.Should().Be(error, "the error was raised during subscription");
    }

    [Fact]
    public void DoesNotHoldALockWhileDeliveringDownstream()
    {
        // Observable.Switch holds its gate for the whole of downstream delivery, which is the shape that
        // deadlocks when a pipeline crosses into another collection. Delivery has to go through the queue,
        // which enqueues and returns, so a producer is never held up by whatever a subscriber is doing.
        using var switchable = new Subject<IObservable<IChangeSet<int>>>();
        using var first = new Subject<IChangeSet<int>>();

        using var isDelivering = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);

        using var subscription = switchable.Switch().Subscribe(_ =>
        {
            isDelivering.Set();
            release.Wait(TimeSpan.FromSeconds(10));
        });

        switchable.OnNext(first);

        var deliverer = new Thread(() => first.OnNext(new ChangeSet<int> { new Change<int>(ListChangeReason.Add, 1, 0) })) { IsBackground = true };
        deliverer.Start();

        isDelivering.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue("the subscriber should have been handed the change");

        var producer = new Thread(() => switchable.OnNext(new Subject<IChangeSet<int>>())) { IsBackground = true };
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
        var supersededObserver = default(IObserver<IChangeSet<int>>);
        var superseded = RawAnonymousObservable.Create<IChangeSet<int>>(observer =>
        {
            supersededObserver = observer;
            return Disposable.Empty;
        });

        using var switchable = new Subject<IObservable<IChangeSet<int>>>();
        using var current = new Subject<IChangeSet<int>>();

        using var results = switchable.Switch().AsAggregator();

        switchable.OnNext(superseded);
        switchable.OnNext(current);

        supersededObserver.Should().NotBeNull("the superseded source should have been subscribed");
        supersededObserver!.OnError(new Exception("Test"));

        results.Exception.Should().BeNull("the failed source had already been switched away from");

        current.OnNext(new ChangeSet<int> { new Change<int>(ListChangeReason.Add, 1, 0) });

        results.Data.Count.Should().Be(1, "the selected source should still be delivering");
    }
}
