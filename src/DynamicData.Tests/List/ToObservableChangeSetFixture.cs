using System;
using System.Collections.Generic;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Microsoft.Reactive.Testing;

using Xunit;

namespace DynamicData.Tests.List;

public class ToObservableChangeSetFixture : ReactiveTest, IDisposable
{
    private readonly IDisposable _disposable;

    private readonly Person _person1 = new("One", 1);

    private readonly Person _person2 = new("Two", 2);

    private readonly Person _person3 = new("Three", 3);

    private readonly TestScheduler _scheduler;

    private readonly List<Person> _target;

    private readonly IObservable<Person> _observable;

    public ToObservableChangeSetFixture()
    {
        _scheduler = new TestScheduler();
        _observable = _scheduler.CreateColdObservable(OnNext(1, _person1), OnNext(2, _person2), OnNext(3, _person3));

        _target = new List<Person>();

        _disposable = _observable.ToObservableChangeSet(2, _scheduler).Clone(_target).Subscribe();
    }

    public void Dispose()
    {
        _disposable.Dispose();
    }

    [Fact]
    public void ShouldLimitSizeOfBoundCollection()
    {
        _scheduler.AdvanceTo(2);
        _target.Count.Should().Be(2, "Should be 2 item in target collection");

        _scheduler.AdvanceTo(3);
        _target.Count.Should().Be(2, "Should be 2 item in target collection because of size limit");

        var expected = new[] { _person2, _person3 };

        _target.Should().BeEquivalentTo(expected);
    }
}
