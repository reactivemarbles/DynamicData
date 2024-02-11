using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

using Bogus;
using DynamicData.Tests.Domain;
using DynamicData.Binding;
using DynamicData.Kernel;
using FluentAssertions;
using Xunit;

using Person = DynamicData.Tests.Domain.Person;
using System.Reactive.Subjects;
using System.Reactive;

namespace DynamicData.Tests.Cache;

public class GroupOnDynamicFixture : IDisposable
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
    private readonly SourceCache<Person, string> _personCache = new(p => p.UniqueKey);
    private readonly ChangeSetAggregator<Person, string> _personResults;
    private readonly GroupChangeSetAggregator<Person, string, string> _groupResults;
    private readonly Faker<Person> _personFaker;
    private readonly Randomizer _randomizer;
    private readonly Subject<Func<Person, string, string>> _keySelectionSubject = new ();
    private readonly Subject<Unit> _regroupSubject = new ();
    private readonly IDisposable _cleanup;
    private Func<Person, string, string>? _groupKeySelector;

    public GroupOnDynamicFixture()
    {
        unchecked { _randomizer = new((int)0xc001_d00d); }
        _personFaker = Fakers.Person.Clone().WithSeed(_randomizer);
        _personResults = _personCache.Connect().AsAggregator();
        _groupResults = _personCache.Connect().Group(_keySelectionSubject, _regroupSubject).AsAggregator();
        _cleanup = _keySelectionSubject.Do(func => _groupKeySelector = func).Subscribe();
    }

    [Fact]
    public void ResultEmptyIfSelectionKeyDoesNotFire()
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
    public void ResultContainsAllInitialChildren()
    {
        // Arrange
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));

        // Act
        GroupByFavColor();

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount);
        _personResults.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsAllAddedChildren()
    {
        // Arrange
        GroupByFavColor();

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
        GroupByPetType();

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
        GroupByPetType();

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
        GroupByPetType();
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
    public void ResultIsCorrectWhenGroupSelectorChanges()
    {
        // Arrange
        GroupByFavColor();
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        var usedColorList = _personCache.Items.Select(p => p.FavoriteColor).Distinct().Select(x => x.ToString()).ToList();
        var usedPetTypeList = _personCache.Items.Select(p => p.PetType).Distinct().Select(x => x.ToString()).ToList();

        // Act
        GroupByPetType();

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount);
        _personResults.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        _groupResults.Summary.Overall.Adds.Should().Be(usedColorList.Count + usedPetTypeList.Count);
        _groupResults.Summary.Overall.Removes.Should().Be(usedColorList.Count);
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultIsCorrectAfterForcedRegroup()
    {
        // Arrange
        GroupByFavColor();
        _personCache.AddOrUpdate(_personFaker.Generate(InitialCount));
        _personCache.Items.ForEach(person => person.FavoriteColor = _randomizer.RandomColor(person.FavoriteColor));

        // Act
        ForceRegroup();

        // Assert
        _personResults.Data.Count.Should().Be(InitialCount);
        _personResults.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        VerifyGroupingResults();
    }

    public void Dispose()
    {
        _groupResults.Dispose();
        _personResults.Dispose();
        _personCache.Dispose();
        _cleanup.Dispose();
        _keySelectionSubject.Dispose();
        _regroupSubject.Dispose();
    }

    private void VerifyGroupingResults() =>
        VerifyGroupingResults(_personCache, _personResults, _groupResults, _groupKeySelector);

    private static void VerifyGroupingResults(ISourceCache<Person, string> personCache, ChangeSetAggregator<Person, string> personResults, GroupChangeSetAggregator<Person, string, string> groupResults, Func<Person, string, string>? groupKeySelector)
    {
        if (groupKeySelector is null)
        {
            groupResults.Data.Count.Should().Be(0);
            groupResults.Groups.Count.Should().Be(0);
            return;
        }

        var expectedPersons = personCache.Items.ToList();
        var expectedGroupings = personCache.Items.GroupBy(p => groupKeySelector(p, string.Empty)).ToList();

        // These should be subsets of each other
        expectedPersons.Should().BeEquivalentTo(personResults.Data.Items);
        groupResults.Groups.Count.Should().Be(expectedGroupings.Count);

        // Check each group
        foreach (var grouping in expectedGroupings)
        {
            var key = grouping.Key;
            var expectedGroup = grouping.ToList();
            var optionalGroup = groupResults.Groups.Lookup(key);

            optionalGroup.HasValue.Should().BeTrue();
            var actualGroup = optionalGroup.Value.Data.Items.ToList();

            expectedGroup.Should().BeEquivalentTo(actualGroup);
        }
    }

    private void ForceRegroup() => _regroupSubject.OnNext(Unit.Default);

    private void GroupByFavColor() => _keySelectionSubject.OnNext(FavColor);

    private void GroupByParentName() => _keySelectionSubject.OnNext(ParentName);

    private void GroupByPetType() => _keySelectionSubject.OnNext(PetType);

    private static string FavColor(Person person, string _) => person.FavoriteColor.ToString();

    private static string ParentName(Person person, string _) => person.ParentName ?? string.Empty;

    private static string PetType(Person person, string _) => person.PetType.ToString();
}
