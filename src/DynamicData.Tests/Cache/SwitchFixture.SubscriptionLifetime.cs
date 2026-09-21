using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace DynamicData.Tests.Cache;

public partial class SwitchFixture
{
    private readonly ITestOutputHelper _output;

    public SwitchFixture(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ReplacementReleasesPreviousSubscriptionBeforeAcquiringResource()
    {
        var people = CreateSubscriptionPeople(2);
        Person? resourceOwner = null;
        var released = new List<Person>();
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        using var subscription = switchable.Switch()
            .ValidateSynchronization()
            .ValidateChangeSets(person => person.Name)
            .RecordCacheItems(out var results);

        switchable.OnNext(CreateExclusiveSource(people[0]));
        results.RecordedItemsByKey.Values.Should().Equal(people[0]);

        switchable.OnNext(CreateExclusiveSource(people[1]));

        results.Error.Should().BeNull("the previous subscription must release the resource before its replacement subscribes");
        results.RecordedItemsByKey.Should().BeEquivalentTo(new[] { people[1] }.ToDictionary(person => person.Name));
        resourceOwner.Should().BeSameAs(people[1]);
        released.Should().Equal(people[0]);

        subscription.Dispose();

        resourceOwner.Should().BeNull();
        released.Should().Equal(people);
        switchable.HasObservers.Should().BeFalse();
        results.HasCompleted.Should().BeFalse("unsubscription is not completion");

        IObservable<IChangeSet<Person, string>> CreateExclusiveSource(Person person) =>
            Observable.Create<IChangeSet<Person, string>>(observer =>
            {
                if (resourceOwner is not null)
                {
                    observer.OnError(new InvalidOperationException("The previous subscription still owns the resource."));
                    return Disposable.Empty;
                }

                resourceOwner = person;
                observer.OnNext(AddSubscriptionPerson(person));
                return Disposable.Create(() =>
                {
                    released.Add(person);
                    resourceOwner = null;
                });
            });
    }

    [Theory]
    [InlineData(null)]
    [InlineData(NotificationKind.OnCompleted)]
    [InlineData(NotificationKind.OnError)]
    public void ReentrantSelectionDuringSynchronousAddKeepsLatestSubscription(NotificationKind? supersededTermination)
    {
        var people = CreateSubscriptionPeople(2);
        var supersededDisposals = 0;
        var supersededError = new InvalidOperationException("The superseded source failed before Subscribe returned.");
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        using var latest = new Subject<IChangeSet<Person, string>>();
        var first = Observable.Create<IChangeSet<Person, string>>(observer =>
        {
            // The downstream callback selects latest before this Subscribe can return its disposable.
            observer.OnNext(AddSubscriptionPerson(people[0]));
            if (supersededTermination is NotificationKind.OnCompleted)
                observer.OnCompleted();
            else if (supersededTermination is NotificationKind.OnError)
                observer.OnError(supersededError);

            return Disposable.Create(() => supersededDisposals++);
        });
        using var subscription = switchable.Switch()
            .ValidateSynchronization()
            .ValidateChangeSets(person => person.Name)
            .Do(changes =>
            {
                if (changes.Any(change => change.Reason is ChangeReason.Add && change.Key == people[0].Name))
                    switchable.OnNext(latest);
            })
            .RecordCacheItems(out var results);

        switchable.OnNext(first);
        var latestRemainedSubscribed = latest.HasObservers;
        latest.OnNext(AddSubscriptionPerson(people[1]));
        switchable.OnCompleted();
        var completedBeforeLatest = results.HasCompleted;
        latest.OnCompleted();

        results.Error.Should().BeNull("termination of a superseded source must not terminate the selected source");
        latestRemainedSubscribed.Should().BeTrue("the late return from the first Subscribe must not dispose the newer subscription");
        supersededDisposals.Should().Be(1);
        completedBeforeLatest.Should().BeFalse();
        results.HasCompleted.Should().BeTrue();
        results.RecordedItemsByKey.Should().BeEquivalentTo(new[] { people[1] }.ToDictionary(person => person.Name));
        results.RecordedChangeSets.SelectMany(changes => changes)
            .Select(change => (change.Reason, change.Key, change.Current))
            .Should().Equal(
                (ChangeReason.Add, people[0].Name, people[0]),
                (ChangeReason.Remove, people[0].Name, people[0]),
                (ChangeReason.Add, people[1].Name, people[1]));
    }

    [Fact]
    public void ReentrantSelectionDuringResetDoesNotSubscribeSupersededReplacement()
    {
        var people = CreateSubscriptionPeople(3);
        var supersededSubscriptions = 0;
        using var first = new SourceCache<Person, string>(person => person.Name);
        using var latest = new TestSourceCache<Person, string>(person => person.Name);
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        first.AddOrUpdate(people[0]);
        latest.AddOrUpdate(people[1]);
        var superseded = Observable.Defer(() =>
        {
            supersededSubscriptions++;
            return Observable.Never<IChangeSet<Person, string>>();
        });
        using var subscription = switchable.Switch()
            .ValidateSynchronization()
            .ValidateChangeSets(person => person.Name)
            .Do(changes =>
            {
                if (changes.Any(change => change.Reason is ChangeReason.Remove && change.Key == people[0].Name))
                    switchable.OnNext(latest.Connect());
            })
            .RecordCacheItems(out var results);

        switchable.OnNext(first.Connect());
        switchable.OnNext(superseded);
        latest.AddOrUpdate(people[2]);
        switchable.OnCompleted();
        latest.Complete();

        supersededSubscriptions.Should().Be(0, "the reset callback selected a newer source before the pending subscription started");
        results.Error.Should().BeNull();
        results.HasCompleted.Should().BeTrue();
        results.RecordedItemsByKey.Should().BeEquivalentTo(people.Skip(1).ToDictionary(person => person.Name));
        results.RecordedChangeSets.SelectMany(changes => changes)
            .Select(change => (change.Reason, change.Key, change.Current))
            .Should().Equal(
                (ChangeReason.Add, people[0].Name, people[0]),
                (ChangeReason.Remove, people[0].Name, people[0]),
                (ChangeReason.Add, people[1].Name, people[1]),
                (ChangeReason.Add, people[2].Name, people[2]));
    }

    [Fact]
    public void ReentrantSelectionDuringDisposalDoesNotSubscribeSupersededReplacement()
    {
        var people = CreateSubscriptionPeople(2);
        var firstDisposals = 0;
        var supersededSubscriptions = 0;
        using var latest = new Subject<IChangeSet<Person, string>>();
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        var first = Observable.Create<IChangeSet<Person, string>>(observer =>
        {
            observer.OnNext(AddSubscriptionPerson(people[0]));
            return Disposable.Create(() =>
            {
                firstDisposals++;
                switchable.OnNext(latest);
            });
        });
        var superseded = Observable.Defer(() =>
        {
            supersededSubscriptions++;
            return Observable.Never<IChangeSet<Person, string>>();
        });
        using var subscription = switchable.Switch()
            .ValidateSynchronization()
            .ValidateChangeSets(person => person.Name)
            .RecordCacheItems(out var results);

        switchable.OnNext(first);
        switchable.OnNext(superseded);
        latest.OnNext(AddSubscriptionPerson(people[1]));
        switchable.OnCompleted();
        latest.OnCompleted();

        firstDisposals.Should().Be(1);
        supersededSubscriptions.Should().Be(0, "releasing the old subscription selected latest before the replacement could start");
        results.Error.Should().BeNull();
        results.HasCompleted.Should().BeTrue();
        results.RecordedItemsByKey.Should().BeEquivalentTo(new[] { people[1] }.ToDictionary(person => person.Name));
        results.RecordedChangeSets.SelectMany(changes => changes)
            .Select(change => (change.Reason, change.Key, change.Current))
            .Should().Equal(
                (ChangeReason.Add, people[0].Name, people[0]),
                (ChangeReason.Remove, people[0].Name, people[0]),
                (ChangeReason.Add, people[1].Name, people[1]));
    }

    [Fact]
    public void DisposalDuringResetDoesNotSubscribeReplacement()
    {
        var people = CreateSubscriptionPeople(2);
        var replacementSubscriptions = 0;
        using var first = new BehaviorSubject<IChangeSet<Person, string>>(AddSubscriptionPerson(people[0]));
        using var replacementChanges = new Subject<IChangeSet<Person, string>>();
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        var replacement = Observable.Defer(() =>
        {
            replacementSubscriptions++;
            return replacementChanges;
        });
        var results = new CacheItemRecordingObserver<Person, string>(Scheduler.Immediate);
        IObserver<IChangeSet<Person, string>> recorder = results;
        using var subscription = new SingleAssignmentDisposable();
        subscription.Disposable = switchable.Switch()
            .ValidateSynchronization()
            .ValidateChangeSets(person => person.Name)
            .Subscribe(
                changes =>
                {
                    // Record the reset before disposing, so the expected observable state is unambiguous.
                    recorder.OnNext(changes);
                    if (changes.Removes != 0)
                        subscription.Dispose();
                },
                recorder.OnError,
                recorder.OnCompleted);

        switchable.OnNext(first);
        switchable.OnNext(replacement);
        var notificationsAtDisposal = results.Notifications.ToArray();
        replacementChanges.OnNext(AddSubscriptionPerson(people[1]));
        replacementChanges.OnCompleted();

        replacementSubscriptions.Should().Be(0, "disposal in the reset callback must cancel the pending subscription, not just immediately dispose it");
        first.HasObservers.Should().BeFalse();
        replacementChanges.HasObservers.Should().BeFalse();
        switchable.HasObservers.Should().BeFalse();
        results.RecordedItemsByKey.Should().BeEmpty();
        results.Notifications.Should().Equal(notificationsAtDisposal);
        results.Error.Should().BeNull();
        results.HasCompleted.Should().BeFalse();
    }

    [Fact]
    public void OuterErrorDuringResetDoesNotSubscribeReplacement()
    {
        var person = CreateSubscriptionPeople(1)[0];
        var error = new InvalidOperationException("The outer source failed during reset.");
        var replacementSubscriptions = 0;
        using var first = new BehaviorSubject<IChangeSet<Person, string>>(AddSubscriptionPerson(person));
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        var replacement = Observable.Defer(() =>
        {
            replacementSubscriptions++;
            return Observable.Never<IChangeSet<Person, string>>();
        });
        using var subscription = switchable.Switch()
            .ValidateSynchronization()
            .ValidateChangeSets(value => value.Name)
            .Do(changes =>
            {
                if (changes.Removes != 0)
                    switchable.OnError(error);
            })
            .RecordCacheItems(out var results);

        switchable.OnNext(first);
        switchable.OnNext(replacement);

        replacementSubscriptions.Should().Be(0, "a pending subscription must not start after terminal delivery");
        results.Error.Should().BeSameAs(error);
        results.HasCompleted.Should().BeFalse();
        results.RecordedItemsByKey.Should().BeEmpty();
        results.Notifications.Select(notification => notification.Value.Kind)
            .Should().Equal(NotificationKind.OnNext, NotificationKind.OnNext, NotificationKind.OnError);
        first.HasObservers.Should().BeFalse();
        switchable.HasObservers.Should().BeFalse();
    }

    [Fact]
    public void OuterCompletionDuringResetWaitsForReplacement()
    {
        var people = CreateSubscriptionPeople(2);
        using var first = new BehaviorSubject<IChangeSet<Person, string>>(AddSubscriptionPerson(people[0]));
        using var replacement = new TestSourceCache<Person, string>(person => person.Name);
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        using var subscription = switchable.Switch()
            .ValidateSynchronization()
            .ValidateChangeSets(person => person.Name)
            .Do(changes =>
            {
                if (changes.Removes != 0)
                    switchable.OnCompleted();
            })
            .RecordCacheItems(out var results);

        switchable.OnNext(first);
        switchable.OnNext(replacement.Connect());
        results.HasCompleted.Should().BeFalse("outer completion must not cancel the already-selected inner source");

        replacement.AddOrUpdate(people[1]);
        replacement.Complete();

        results.Error.Should().BeNull();
        results.HasCompleted.Should().BeTrue();
        results.RecordedItemsByKey.Should().BeEquivalentTo(new[] { people[1] }.ToDictionary(person => person.Name));
        first.HasObservers.Should().BeFalse();
    }

    [Fact]
    public void SynchronousOuterSubscriptionFailureReleasesActivatedInner()
    {
        var person = CreateSubscriptionPeople(1)[0];
        var error = new InvalidOperationException("The outer Subscribe failed after activating an inner source.");
        var innerDisposals = 0;
        using var inner = new BehaviorSubject<IChangeSet<Person, string>>(AddSubscriptionPerson(person));
        var source = RawAnonymousObservable.Create<IObservable<IChangeSet<Person, string>>>(observer =>
        {
            observer.OnNext(inner.Finally(() => innerDisposals++));
            throw error;
        });
        using var subscription = source.Switch()
            .ValidateSynchronization()
            .ValidateChangeSets(value => value.Name)
            .RecordCacheItems(out var results);

        results.Error.Should().BeSameAs(error);
        results.HasCompleted.Should().BeFalse();
        results.RecordedItemsByKey.Should().BeEquivalentTo(new[] { person }.ToDictionary(value => value.Name));
        results.Notifications.Select(notification => notification.Value.Kind)
            .Should().Equal(NotificationKind.OnNext, NotificationKind.OnError);
        inner.HasObservers.Should().BeFalse("a failed outer activation must release the inner it already subscribed");
        innerDisposals.Should().Be(1);
    }

    [Fact]
    public void SubscriptionsKeepIndependentCurrentSources()
    {
        var people = CreateSubscriptionPeople(2);
        using var first = new BehaviorSubject<IChangeSet<Person, string>>(AddSubscriptionPerson(people[0]));
        using var second = new BehaviorSubject<IChangeSet<Person, string>>(AddSubscriptionPerson(people[1]));
        using var switchable = new Subject<IObservable<IChangeSet<Person, string>>>();
        var switched = switchable.Switch();
        using var firstSubscription = switched
            .ValidateSynchronization()
            .ValidateChangeSets(person => person.Name)
            .RecordCacheItems(out var firstResults);
        using var secondSubscription = switched
            .ValidateSynchronization()
            .ValidateChangeSets(person => person.Name)
            .RecordCacheItems(out var secondResults);

        switchable.OnNext(first);
        firstSubscription.Dispose();
        switchable.OnNext(second);
        switchable.OnCompleted();
        second.OnCompleted();

        firstResults.Error.Should().BeNull();
        firstResults.HasCompleted.Should().BeFalse();
        firstResults.RecordedItemsByKey.Should().BeEquivalentTo(new[] { people[0] }.ToDictionary(person => person.Name));
        secondResults.Error.Should().BeNull();
        secondResults.HasCompleted.Should().BeTrue();
        secondResults.RecordedItemsByKey.Should().BeEquivalentTo(new[] { people[1] }.ToDictionary(person => person.Name));
        first.HasObservers.Should().BeFalse();
        second.HasObservers.Should().BeFalse();
        switchable.HasObservers.Should().BeFalse();
    }

    private static IChangeSet<Person, string> AddSubscriptionPerson(Person person) =>
        new ChangeSet<Person, string> { new(ChangeReason.Add, person.Name, person) };

    private Person[] CreateSubscriptionPeople(int count)
    {
        const int seed = 0x51A7;
        _output.WriteLine("Subscription lifetime data: seed={0}, count={1}", seed, count);
        return Fakers.Person.Clone().UseSeed(seed).Generate(count).ToArray();
    }
}
