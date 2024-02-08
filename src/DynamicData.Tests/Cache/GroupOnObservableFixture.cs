﻿using System;
using System.Linq;
using Bogus;
using DynamicData.Tests.Domain;
using DynamicData.Binding;
using System.Reactive.Linq;
using FluentAssertions;
using Xunit;

using Person = DynamicData.Tests.Domain.Person;
using System.Threading.Tasks;

namespace DynamicData.Tests.Cache;

public class GroupOnObservableFixture : IDisposable
{
#if DEBUG
    private const int InitialCount = 7;
    private const int AddCount = 5;
    private const int RemoveCount = 3;
    private const int UpdateCount = 2;
#else
    private const int InitialCount = 103;
    private const int AddCount = 53;
    private const int RemoveCount = 37;
    private const int UpdateCount = 31;
#endif
    private readonly SourceCache<Person, string> _personCache = new (p => p.Key);
    private readonly ChangeSetAggregator<Person, string> _personResults;
    private readonly GroupChangeSetAggregator<Person, string, Color> _personByColorResults;
    private readonly Faker<Person> _personFaker;
    private readonly Randomizer _randomizer = new(0x3141_5926);

    public GroupOnObservableFixture()
    {
        _personFaker = Fakers.Person.Clone().WithSeed(_randomizer);
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        _personResults = _personCache.Connect().AsAggregator();
        _personByColorResults = _personCache.Connect().GroupOnObservable(CreateColorGroupObservable).AsAggregator();
    }

    [Fact]
    public void ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount);
        _personResults.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsAddedValues()
    {
        // Arrange

        // Act
        _personCache.AddOrUpdate(_personFaker.Generate(AddCount));

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount + AddCount);
        _personResults.Messages.Count.Should().Be(2, "Initial Adds and then the subsequent Additions should each be a single message");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultDoesNotContainRemovedValues()
    {
        // Arrange

        // Act
        _personCache.RemoveKeys(_randomizer.ListItems(_personCache.Items.ToList(), RemoveCount).Select(p => p.Key));

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount - RemoveCount);
        _personResults.Messages.Count.Should().Be(2, "1 for Adds and 1 for Removes");
        VerifyGroupingResults();
    }


    [Fact]
    public async Task ResultUpdatesOnFutureValues()
    {
        // Arrange
        void updateColor()
        {
            var person = _randomizer.ListItem(_personCache.Items.ToList());
            // Pick a new favorite color
            person.FavoriteColor = _randomizer.RandomColor(person.FavoriteColor);
        }

        var tasks = Enumerable.Range(0, UpdateCount).Select(_ => Task.Run(updateColor));

        // Act
        await Task.WhenAll(tasks.ToArray());
        await Task.Delay(1000);

        // Assert
        VerifyGroupingResults();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResultCompletesOnlyWhenSourceCompletes(bool completeSource)
    {
        // Arrange

        // Act
        if (completeSource)
        {
            _personCache.Dispose();
        }

        // Assert
        _personResults.IsCompleted.Should().Be(completeSource);
    }

    [Fact]
    public void ResultFailsIfSourceFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<Person, string>>(expectedError);
        using var results = _personCache.Connect().Concat(throwObservable).GroupOnObservable(CreateColorGroupObservable).AsAggregator();

        // Act
        _personCache.Dispose();

        // Assert
        results.Error.Should().Be(expectedError);
    }

    [Fact]
    public void ResultFailsIfGroupObservableFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<Color>(expectedError);

        // Act
        using var results = _personCache.Connect().GroupOnObservable((person, key) => CreateColorGroupObservable(person, key).Take(1).Concat(throwObservable)).AsAggregator();

        // Assert
        results.Error.Should().Be(expectedError);
    }

    [Fact]
    public void OnErrorFiresIfSelectorThrows()
    {
        // Arrange
        var expectedError = new Exception("Expected");

        // Act
        using var results = _personCache.Connect().GroupOnObservable<Person, string, Color>(_ => throw expectedError).AsAggregator();

        // Assert
        results.Error.Should().Be(expectedError);
    }

    public void Dispose()
    {
        _personByColorResults.Dispose();
        _personResults.Dispose();
        _personCache.Dispose();
    }

    private void VerifyGroupingResults() =>
        VerifyGroupingResults(_personCache, _personResults, _personByColorResults);

    private static void VerifyGroupingResults(ISourceCache<Person, string> personCache, ChangeSetAggregator<Person, string> personResults, GroupChangeSetAggregator<Person, string, Color> personByColorResults)
    {
        var expectedPersons = personCache.Items.ToList();
        var expectedGroupings = personCache.Items.GroupBy(p => p.FavoriteColor).ToList();

        // These should be subsets of each other
        expectedPersons.Should().BeSubsetOf(personResults.Data.Items);
        personResults.Data.Items.Count().Should().Be(expectedPersons.Count);
        personByColorResults.Groups.Count.Should().Be(personByColorResults.Data.Count);
        personByColorResults.Data.Count.Should().Be(expectedGroupings.Count);

        // Check each group
        foreach (var grouping in expectedGroupings)
        {
            var color = grouping.Key;
            var expectedGroup = grouping.ToList();
            var optionalGroup = personByColorResults.Groups.Lookup(color);

            optionalGroup.HasValue.Should().BeTrue();
            var actualGroup = optionalGroup.Value.Data.Items.ToList();

            expectedGroup.Should().BeSubsetOf(actualGroup);
            actualGroup.Count.Should().Be(expectedGroup.Count);
        }
    }

    private static IObservable<Color> CreateColorGroupObservable(Person person, string key) =>
         person.WhenPropertyChanged(p => p.FavoriteColor).Select(change => change.Value);
}
