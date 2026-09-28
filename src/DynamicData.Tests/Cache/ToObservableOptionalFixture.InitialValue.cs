using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DynamicData.Tests.Cache;

public partial class ToObservableOptionalFixture
{
    private const int InitialValueSeed = 0x0F710;

    private readonly ITestOutputHelper _output;

    [Fact]
    public async Task InitialOptionalNeverFollowsConcurrentSomeWithoutRemoval()
    {
        const int maximumIterations = 20_000;
        const int maximumUnwatchedAdds = 16;
        var value = CreateInitialValue();
        var expected = Optional.Some(value);
        var randomizer = new Randomizer(InitialValueSeed);
        var unwatchedValues = Enumerable.Range(0, maximumUnwatchedAdds)
            .Select(index => Create($"{value.Key}/{index}", randomizer.AlphaNumeric(16)))
            .ToArray();
        var changeSets = Enumerable.Range(0, maximumUnwatchedAdds + 1)
            .Select(count => new ChangeSet<KeyValuePair, string>(unwatchedValues.Take(count)
                .Select(item => new Change<KeyValuePair, string>(ChangeReason.Add, item.Key, item))
                .Append(new Change<KeyValuePair, string>(ChangeReason.Add, value.Key, value))))
            .ToArray();
        var timeout = TimeSpan.FromSeconds(10);
        using var cancellation = new CancellationTokenSource();
        using var barrier = new Barrier(2);
        IObserver<IChangeSet<KeyValuePair, string>>? sourceObserver = null;
        var completedIterations = 0;
        var invalidSequences = 0;
        var invalidFinalNones = 0;
        string? firstFailure = null;
        _output.WriteLine("Initial optional race: seed={0}, maximumIterations={1}, maximumUnwatchedAdds={2}",
            InitialValueSeed, maximumIterations, maximumUnwatchedAdds);

        // Only the Add races subscription. Vary real changeset work (ignored keys before the watched
        // key), not sleeps or spins. Every input contains exactly one Add for the watched key and no Remove.
        var producer = Task.Factory.StartNew(() =>
        {
            try
            {
                for (var iteration = 0; iteration < maximumIterations; iteration++)
                {
                    Rendezvous(iteration, "subscription started");
                    sourceObserver!.OnNext(changeSets[iteration % changeSets.Length]);
                    Rendezvous(iteration, "value delivered and subscription returned");
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // The subscriber stops the bounded run immediately after finding a violation.
            }
            catch
            {
                cancellation.Cancel();
                throw;
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        try
        {
            for (var iteration = 0; iteration < maximumIterations; iteration++)
            {
                var source = Observable.Create<IChangeSet<KeyValuePair, string>>(observer =>
                {
                    sourceObserver = observer;
                    Rendezvous(iteration, "subscription started");
                    return Disposable.Empty;
                });
                using var subscription = source
                    .ToObservableOptional(value.Key, initialOptionalWhenMissing: true)
                    .ValidateSynchronization()
                    .RecordValues(out var results);

                Rendezvous(iteration, "value delivered and subscription returned");

                // Both racing calls have returned. Complete only now, on this thread, so the probe
                // does not also race completion against initialization. No further source delivery
                // can occur in this round, and the synchronous terminal notification must be recorded.
                sourceObserver!.OnCompleted();
                completedIterations++;

                var values = results.RecordedValues;
                var validValues = (values.Count == 1 && values[0].Equals(expected))
                    || (values.Count == 2 && !values[0].HasValue && values[1].Equals(expected));
                var validTermination = results.Error is null
                    && results.HasCompleted
                    && results.WhenFinalized.IsCompletedSuccessfully
                    && results.Notifications.Count == values.Count + 1
                    && results.Notifications[^1].Value.Kind is NotificationKind.OnCompleted;
                if (!validValues || !validTermination)
                {
                    invalidSequences++;
                    if (values.Count != 0 && !values[^1].HasValue)
                        invalidFinalNones++;

                    var sequence = string.Join(", ", values.Select(optional => optional.HasValue ? $"Some({optional.Value.Value})" : "None"));
                    firstFailure = $"iteration={iteration}, unwatchedAdds={iteration % changeSets.Length}, values=[{sequence}], completed={results.HasCompleted}, error={results.Error}";
                    break;
                }
            }
        }
        finally
        {
            // There is never an unbounded worker left waiting for a round that the subscriber skipped.
            cancellation.Cancel();
            await producer.WaitAsync(timeout);
            _output.WriteLine("Initial optional race: completedIterations={0}/{1}, invalidSequences={2}, invalidFinalNones={3}, firstFailure={4}",
                completedIterations, maximumIterations, invalidSequences, invalidFinalNones, firstFailure ?? "none");
        }

        invalidSequences.Should().Be(0,
            "one Add for the watched key with no removal permits only [Some(value)] or [None, Some(value)], followed by completion; {0}",
            firstFailure ?? "all iterations satisfied the contract");

        void Rendezvous(int iteration, string phase)
        {
            if (!barrier.SignalAndWait(timeout, cancellation.Token))
                throw new TimeoutException($"Initial optional race stalled at iteration {iteration}, phase '{phase}'.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialOptionalPreservesSynchronousEmptyCompletion(bool initialOptionalWhenMissing)
    {
        using var subscription = Observable.Empty<IChangeSet<KeyValuePair, string>>()
            .ToObservableOptional(Key1, initialOptionalWhenMissing)
            .ValidateSynchronization()
            .RecordValues(out var results);

        var expected = initialOptionalWhenMissing
            ? new[] { Optional.None<KeyValuePair>() }
            : Array.Empty<Optional<KeyValuePair>>();
        results.RecordedValues.Should().Equal(expected);
        results.Error.Should().BeNull();
        results.HasCompleted.Should().BeTrue();
        results.Notifications.Select(notification => notification.Value.Kind).Should().Equal(
            initialOptionalWhenMissing
                ? new[] { NotificationKind.OnNext, NotificationKind.OnCompleted }
                : new[] { NotificationKind.OnCompleted });
    }

    [Fact]
    public void InitialOptionalDoesNotEmitNoneAfterSynchronousError()
    {
        var error = new InvalidOperationException("The source failed during subscription.");
        using var subscription = Observable.Throw<IChangeSet<KeyValuePair, string>>(error)
            .ToObservableOptional(Key1, initialOptionalWhenMissing: true)
            .ValidateSynchronization()
            .RecordValues(out var results);

        results.RecordedValues.Should().BeEmpty();
        results.Error.Should().BeSameAs(error);
        results.HasCompleted.Should().BeFalse();
        results.Notifications.Select(notification => notification.Value.Kind).Should().Equal(NotificationKind.OnError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialOptionalPreservesSynchronousValuesAndCompletion(bool removeBeforeCompletion)
    {
        var value = CreateInitialValue();
        var subscriptions = 0;
        var disposals = 0;
        var source = Observable.Create<IChangeSet<KeyValuePair, string>>(observer =>
        {
            subscriptions++;
            observer.OnNext(new ChangeSet<KeyValuePair, string> { new(ChangeReason.Add, value.Key, value) });
            if (removeBeforeCompletion)
                observer.OnNext(new ChangeSet<KeyValuePair, string> { new(ChangeReason.Remove, value.Key, value) });
            observer.OnCompleted();
            return Disposable.Create(() => disposals++);
        });
        using var subscription = source
            .ToObservableOptional(value.Key, initialOptionalWhenMissing: true)
            .ValidateSynchronization()
            .RecordValues(out var results);

        var expected = removeBeforeCompletion
            ? new[] { Optional.Some(value), Optional.None<KeyValuePair>() }
            : new[] { Optional.Some(value) };
        results.RecordedValues.Should().Equal(expected, "initialization must neither prepend nor append None to synchronous source values");
        results.Error.Should().BeNull();
        results.HasCompleted.Should().BeTrue();
        results.Notifications.Select(notification => notification.Value.Kind).Should().Equal(
            removeBeforeCompletion
                ? new[] { NotificationKind.OnNext, NotificationKind.OnNext, NotificationKind.OnCompleted }
                : new[] { NotificationKind.OnNext, NotificationKind.OnCompleted });
        subscriptions.Should().Be(1);
        disposals.Should().Be(1);
    }

    [Fact]
    public void InitialOptionalStateIsPerSubscription()
    {
        var value = CreateInitialValue();
        using var source = new TestSourceCache<KeyValuePair, string>(item => item.Key);
        var optional = source.Connect().ToObservableOptional(value.Key, initialOptionalWhenMissing: true);
        source.AddOrUpdate(value);
        using var firstSubscription = optional.ValidateSynchronization().RecordValues(out var firstResults);

        source.RemoveKey(value.Key);
        using var secondSubscription = optional.ValidateSynchronization().RecordValues(out var secondResults);
        secondResults.RecordedValues.Should().Equal(new[] { Optional.None<KeyValuePair>() }, "the new subscriber starts while the key is absent");

        source.AddOrUpdate(value);
        firstSubscription.Dispose();
        source.RemoveKey(value.Key);
        source.Complete();

        firstResults.RecordedValues.Should().Equal(Optional.Some(value), Optional.None<KeyValuePair>(), Optional.Some(value));
        firstResults.HasCompleted.Should().BeFalse();
        firstResults.Error.Should().BeNull();
        secondResults.RecordedValues.Should().Equal(Optional.None<KeyValuePair>(), Optional.Some(value), Optional.None<KeyValuePair>());
        secondResults.HasCompleted.Should().BeTrue();
        secondResults.Error.Should().BeNull();
    }

    [Fact]
    public void InitialOptionalDisposalDuringInitialNoneReleasesSource()
    {
        var subscriptions = 0;
        var disposals = 0;
        var source = Observable.Create<IChangeSet<KeyValuePair, string>>(_ =>
        {
            subscriptions++;
            return Disposable.Create(() => disposals++);
        });
        using var subscription = source
            .ToObservableOptional(Key1, initialOptionalWhenMissing: true)
            .Take(1)
            .ValidateSynchronization()
            .RecordValues(out var results);

        results.RecordedValues.Should().Equal(Optional.None<KeyValuePair>());
        results.Error.Should().BeNull();
        results.HasCompleted.Should().BeTrue();
        results.Notifications.Select(notification => notification.Value.Kind)
            .Should().Equal(NotificationKind.OnNext, NotificationKind.OnCompleted);
        subscriptions.Should().Be(1);
        disposals.Should().Be(1, "Take(1) must unsubscribe even though initialization ran before Subscribe returned");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialOptionalPropagatesAsynchronousTermination(bool failSource)
    {
        var value = CreateInitialValue();
        var error = new InvalidOperationException("The source failed after subscription.");
        using var source = new Subject<IChangeSet<KeyValuePair, string>>();
        using var subscription = source
            .ToObservableOptional(value.Key, initialOptionalWhenMissing: true)
            .ValidateSynchronization()
            .RecordValues(out var results);

        results.RecordedValues.Should().Equal(Optional.None<KeyValuePair>());
        results.HasCompleted.Should().BeFalse();
        source.OnNext(new ChangeSet<KeyValuePair, string> { new(ChangeReason.Add, value.Key, value) });
        if (failSource)
            source.OnError(error);
        else
            source.OnCompleted();

        results.RecordedValues.Should().Equal(Optional.None<KeyValuePair>(), Optional.Some(value));
        results.Error.Should().BeSameAs(failSource ? error : null);
        results.HasCompleted.Should().Be(!failSource);
        results.Notifications.Select(notification => notification.Value.Kind).Should().Equal(
            NotificationKind.OnNext,
            NotificationKind.OnNext,
            failSource ? NotificationKind.OnError : NotificationKind.OnCompleted);
        source.HasObservers.Should().BeFalse();
    }

    private KeyValuePair CreateInitialValue()
    {
        var randomizer = new Randomizer(InitialValueSeed);
        _output.WriteLine("Initial optional data: seed={0}", InitialValueSeed);
        return Create(randomizer.AlphaNumeric(12), randomizer.AlphaNumeric(16));
    }
}
