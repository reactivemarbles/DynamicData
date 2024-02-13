using System;
using System.Linq;
using Bogus;
using DynamicData.Tests.Domain;
using DynamicData.Binding;
using System.Reactive.Linq;
using FluentAssertions;
using Xunit;

using Person = DynamicData.Tests.Domain.Person;
using System.Threading.Tasks;
using DynamicData.Kernel;

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
    private const int UpdateCount = 101;
#endif
    private readonly SourceCache<Person, string> _personCache = new (p => p.UniqueKey);
    private readonly ChangeSetAggregator<Person, string> _personResults;
    private readonly GroupChangeSetAggregator<Person, string, Color> _favoriteColorResults;
    private readonly Faker<Person> _personFaker;
    private readonly Randomizer _randomizer = new(0x3141_5926);

    public GroupOnObservableFixture()
    {
        _personFaker = Fakers.Person.Clone().WithSeed(_randomizer);
        _personResults = _personCache.Connect().AsAggregator();
        _favoriteColorResults = _personCache.Connect().GroupOnObservable(CreateFavoriteColorObservable).AsAggregator();
    }

    [Fact]
    public void ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount);
        _personResults.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsAddedValues()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));

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
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));

        // Act
        _personCache.RemoveKeys(_randomizer.ListItems(_personCache.Items.ToList(), RemoveCount).Select(p => p.UniqueKey));

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount - RemoveCount);
        _personResults.Messages.Count.Should().Be(2, "1 for Adds and 1 for Removes");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsUpdatedValues()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var replacements = _randomizer.ListItems(_personCache.Items.ToList(), UpdateCount)
            .Select(replacePerson => Person.CloneUniqueId(_personFaker.Generate(), replacePerson));

        // Act
        _personCache.AddOrUpdate(replacements);

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount, "Only replacements were made");
        _personResults.Messages.Count.Should().Be(2, "1 for Adds and 1 for Updates");
        VerifyGroupingResults();
    }

    [Fact]
    public void GroupRemovedWhenEmpty()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var usedColorList = _personCache.Items.Select(p => p.FavoriteColor).Distinct().ToList();
        var removeColor = _randomizer.ListItem(usedColorList);
        var colorCount = usedColorList.Count;

        // Act
        _personCache.Edit(updater => updater.Remove(updater.Items.Where(p => p.FavoriteColor == removeColor).Select(p => p.UniqueKey)));

        // Assert
        _personCache.Items.Select(p => p.FavoriteColor).Distinct().Count().Should().Be(colorCount - 1);
        _personResults.Messages.Count.Should().Be(2, "1 for Adds and 1 for Removes");
        _favoriteColorResults.Messages.Count.Should().Be(2, "1 for Adds and 1 for Removes");
        _favoriteColorResults.Summary.Overall.Adds.Should().Be(colorCount);
        _favoriteColorResults.Summary.Overall.Removes.Should().Be(1);
        VerifyGroupingResults();
    }

    [Fact]
    public void GroupNotRemovedIfAddedBackImmediately()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var usedColorList = _personCache.Items.Select(p => p.FavoriteColor).Distinct().ToList();
        var removeColor = _randomizer.ListItem(usedColorList);
        var colorCount = usedColorList.Count;

        // Act
        _personCache.Edit(updater =>
        {
            updater.Remove(updater.Items.Where(p => p.FavoriteColor == removeColor).Select(p => p.UniqueKey));
            var newPerson = _personFaker.Generate();
            newPerson.FavoriteColor = removeColor;
            updater.AddOrUpdate(newPerson);
        });

        // Assert
        _personCache.Items.Select(p => p.FavoriteColor).Distinct().Count().Should().Be(colorCount);
        _personResults.Messages.Count.Should().Be(2, "1 for Adds and 1 for Other Added Value");
        _favoriteColorResults.Messages.Count.Should().Be(1, "Shouldn't be removed/re-added");
        _favoriteColorResults.Summary.Overall.Adds.Should().Be(colorCount);
        _favoriteColorResults.Summary.Overall.Removes.Should().Be(0);
        VerifyGroupingResults();
    }

    [Fact]
    public void GroupingSequenceCompletesWhenEmpty()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var usedColorList = _personCache.Items.Select(p => p.FavoriteColor).Distinct().ToList();
        var removeColor = _randomizer.ListItem(usedColorList);

        var results = _personCache.Connect().GroupOnObservable(CreateFavoriteColorObservable)
            .Filter(grp => grp.Key == removeColor)
            .Take(1)
            .MergeMany(grp => grp.Cache.Connect())
            .AsAggregator();

        // Act
        _personCache.Edit(updater => updater.Remove(updater.Items.Where(p => p.FavoriteColor == removeColor).Select(p => p.UniqueKey)));

        // Assert
        results.IsCompleted.Should().BeTrue();
        VerifyGroupingResults();
    }

    [Fact]
    public void AllSequencesCompleteWhenSourceIsDisposed()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));

        var results = _personCache.Connect().GroupOnObservable(CreateFavoriteColorObservable)
            .MergeMany(grp => grp.Cache.Connect())
            .AsAggregator();

        // Act
        _personCache.Dispose();

        // Assert
        results.IsCompleted.Should().BeTrue();
        VerifyGroupingResults();
    }

    [Fact]
    public void AllGroupsRemovedWhenCleared()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var usedColorList = _personCache.Items.Select(p => p.FavoriteColor).Distinct().ToList();
        var colorCount = usedColorList.Count;

        // Act
        _personCache.Clear();

        // Assert
        _personCache.Items.Count().Should().Be(0);
        _personResults.Messages.Count.Should().Be(2, "1 for Adds and 1 for Removes");
        _favoriteColorResults.Summary.Overall.Adds.Should().Be(colorCount);
        _favoriteColorResults.Summary.Overall.Removes.Should().Be(colorCount);
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultsContainsCorrectRegroupedValues()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));

        // Act
        Enumerable.Range(0, UpdateCount).ForEach(_ => RandomFavoriteColorChange());

        // Assert
        VerifyGroupingResults();
    }

    [Fact]
    public async Task ResultsContainsCorrectRegroupedValuesAsync()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var tasks = Enumerable.Range(0, UpdateCount).Select(_ => Task.Run(RandomFavoriteColorChange));

        // Act
        await Task.WhenAll(tasks.ToArray());

        // Assert
        VerifyGroupingResults();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResultCompletesOnlyWhenSourceCompletes(bool completeSource)
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));

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
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<Person, string>>(expectedError);
        using var results = _personCache.Connect().Concat(throwObservable).GroupOnObservable(CreateFavoriteColorObservable).AsAggregator();

        // Act
        _personCache.Dispose();

        // Assert
        results.Error.Should().Be(expectedError);
    }

    [Fact]
    public void ResultFailsIfGroupObservableFails()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<Color>(expectedError);

        // Act
        using var results = _personCache.Connect().GroupOnObservable((person, key) => CreateFavoriteColorObservable(person, key).Take(1).Concat(throwObservable)).AsAggregator();

        // Assert
        results.Error.Should().Be(expectedError);
    }

    [Fact]
    public void OnErrorFiresIfSelectorThrows()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var expectedError = new Exception("Expected");

        // Act
        using var results = _personCache.Connect().GroupOnObservable<Person, string, Color>(_ => throw expectedError).AsAggregator();

        // Assert
        results.Error.Should().Be(expectedError);
    }

    public void Dispose()
    {
        _favoriteColorResults.Dispose();
        _personResults.Dispose();
        _personCache.Dispose();
    }

    private void RandomFavoriteColorChange()
    {
        var person = _randomizer.ListItem(_personCache.Items.ToList());
        lock (person)
        {
            // Pick a new favorite color
            person.FavoriteColor = _randomizer.RandomColor(person.FavoriteColor);
        }
    }

    private void VerifyGroupingResults() =>
        VerifyGroupingResults(_personCache, _personResults, _favoriteColorResults);

    private static void VerifyGroupingResults(ISourceCache<Person, string> personCache, ChangeSetAggregator<Person, string> personResults, GroupChangeSetAggregator<Person, string, Color> favoriteColorResults)
    {
        var expectedPersons = personCache.Items.ToList();
        var expectedGroupings = personCache.Items.GroupBy(p => p.FavoriteColor).ToList();

        // These should be subsets of each other
        expectedPersons.Should().BeEquivalentTo(personResults.Data.Items);
        favoriteColorResults.Groups.Count.Should().Be(expectedGroupings.Count);

        // Check each group
        foreach (var grouping in expectedGroupings)
        {
            var color = grouping.Key;
            var expectedGroup = grouping.ToList();
            var optionalGroup = favoriteColorResults.Groups.Lookup(color);

            optionalGroup.HasValue.Should().BeTrue();
            var actualGroup = optionalGroup.Value.Data.Items.ToList();

            expectedGroup.Should().BeEquivalentTo(actualGroup);
        }
    }

    private static IObservable<Color> CreateFavoriteColorObservable(Person person, string key) =>
         person.WhenPropertyChanged(p => p.FavoriteColor).Select(change => change.Value);
}
