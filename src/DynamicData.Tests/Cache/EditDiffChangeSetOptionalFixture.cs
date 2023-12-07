using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;
using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class EditDiffChangeSetOptionalFixture
{
    private static readonly Optional<Person> s_noPerson = Optional.None<Person>();

    private const int MaxItems = 1097;

    [Fact]
    [Description("Required to maintain test coverage percentage")]
    public void NullChecksArePerformed()
    {
        Action actionNullKeySelector = () => Observable.Empty<Optional<Person>>().EditDiff<Person, int>(null!);
        Action actionNullObservable = () => default(IObservable<Optional<Person>>)!.EditDiff<Person, int>(null!);

        actionNullKeySelector.Should().Throw<ArgumentNullException>().WithParameterName("keySelector");
        actionNullObservable.Should().Throw<ArgumentNullException>().WithParameterName("source");
    }

    [Fact]
    public void OptionalSomeCreatesAddChange()
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = Observable.Return(optional);

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(1);
        results.Messages.Count.Should().Be(1);
    }

    [Fact]
    public void OptionalNoneCreatesRemoveChange()
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = new[] {optional, s_noPerson}.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(0);
        results.Messages.Count.Should().Be(2);
        results.Messages[0].Adds.Should().Be(1);
        results.Messages[1].Removes.Should().Be(1);
        results.Messages[1].Updates.Should().Be(0);
    }

    [Fact]
    public void OptionalSomeWithSameKeyCreatesUpdateChange()
    {
        // having
        var optional1 = CreatePerson(0, "Name");
        var optional2 = CreatePerson(0, "Update");
        var optObservable = new[] { optional1, optional2 }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(1);
        results.Messages.Count.Should().Be(2);
        results.Messages[0].Adds.Should().Be(1);
        results.Messages[1].Removes.Should().Be(0);
        results.Messages[1].Updates.Should().Be(1);
    }

    [Fact]
    public void OptionalSomeWithSameReferenceCreatesNoChanges()
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = new[] { optional, optional }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(1);
        results.Messages.Count.Should().Be(1);
        results.Summary.Overall.Adds.Should().Be(1);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void OptionalSomeWithSameCreatesNoChanges()
    {
        // having
        var optional1 = CreatePerson(0, "Name");
        var optional2 = CreatePerson(0, "Name");
        var optObservable = new[] { optional1, optional2 }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id, new PersonComparer());
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(1);
        results.Messages.Count.Should().Be(1);
        results.Summary.Overall.Adds.Should().Be(1);
        results.Summary.Overall.Removes.Should().Be(0);
        results.Summary.Overall.Updates.Should().Be(0);
    }

    [Fact]
    public void OptionalSomeWithDifferentKeyCreatesAddRemoveChanges()
    {
        // having
        var optional1 = CreatePerson(0, "Name");
        var optional2 = CreatePerson(1, "Update");
        var optObservable = new[] { optional1, optional2 }.ToObservable();

        // when
        var observableChangeSet = optObservable.EditDiff(p => p.Id);
        using var results = observableChangeSet.AsAggregator();

        // then
        results.Data.Count.Should().Be(1);
        results.Messages.Count.Should().Be(2);
        results.Messages[0].Adds.Should().Be(1);
        results.Messages[1].Removes.Should().Be(1);
        results.Messages[1].Updates.Should().Be(0);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResultCompletesIfAndOnlyIfSourceCompletes(bool completeSource)
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = Observable.Return(optional);
        if (!completeSource)
        {
            optObservable = optObservable.Concat(Observable.Never<Optional<Person>>());
        }
        bool completed = false;

        // when
        using var results = optObservable.Subscribe(_ => { }, () => completed = true);

        // then
        completed.Should().Be(completeSource);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ResultFailsIfAndOnlyIfSourceFails (bool failSource)
    {
        // having
        var optional = CreatePerson(0, "Name");
        var optObservable = Observable.Return(optional);
        var testException = new Exception("Test");
        if (failSource)
        {
            optObservable = optObservable.Concat(Observable.Throw<Optional<Person>>(testException));
        }
        var receivedError = default(Exception);

        // when
        using var results = optObservable.Subscribe(_ => { }, err => receivedError = err);

        // then
        receivedError.Should().Be(failSource ? testException : default);
    }

    private static Optional<Person> CreatePerson(int id, string name) => Optional.Some(new Person(id, name));

    private class PersonComparer : IEqualityComparer<Person>
    {
        public bool Equals([DisallowNull] Person x, [DisallowNull] Person y) =>
            EqualityComparer<string>.Default.Equals(x.Name, y.Name) && EqualityComparer<int>.Default.Equals(x.Id, y.Id);
        public int GetHashCode([DisallowNull] Person obj) => throw new NotImplementedException();
    }

    private class Person(int id, string name)
    {
        public int Id { get; } = id;

        public string Name { get; } = name;
    }
}
