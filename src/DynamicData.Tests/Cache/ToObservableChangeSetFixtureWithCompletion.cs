using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

using DynamicData.Tests.Domain;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class ToObservableChangeSetFixtureWithCompletion : IDisposable
{
    private readonly IDisposable _disposable;

    private readonly ISubject<Person> _observable;

    private readonly List<Person> _target;

    private bool _hasCompleted = false;

    public ToObservableChangeSetFixtureWithCompletion()
    {
        _observable = new Subject<Person>();

        _target = new List<Person>();

        _disposable = _observable.ToObservableChangeSet(p => p.Key).Clone(_target).Subscribe(x => { }, () => _hasCompleted = true);
    }

    [Fact] //- disabled as it's questionable whether the completion should be invoked
    public void ShouldReceiveUpdatesThenComplete()
    {
        _observable.OnNext(new Person("One", 1));
        _observable.OnNext(new Person("Two", 2));

        _target.Count.Should().Be(2);

        _observable.OnCompleted();
        _hasCompleted.Should().Be(true);

        _observable.OnNext(new Person("Three", 3));
        _target.Count.Should().Be(2);
    }

    public void Dispose() => _disposable.Dispose();
}
