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
    private readonly SourceCache<Person, string> _cache = new(p => p.UniqueKey);
    private readonly ChangeSetAggregator<Person, string> _results;
    private readonly GroupChangeSetAggregator<Person, string, string> _groupResults;
    private readonly Faker<Person> _faker;
    private readonly Randomizer _randomizer;
    private readonly Subject<Func<Person, string, string>> _keySelectionSubject = new ();
    private readonly Subject<Unit> _regroupSubject = new ();
    private readonly IDisposable _cleanup;
    private Func<Person, string, string>? _groupKeySelector;

    public GroupOnDynamicFixture()
    {
        unchecked { _randomizer = new((int)0xc001_d00d); }
        _faker = Fakers.Person.Clone().WithSeed(_randomizer);
        _results = _cache.Connect().AsAggregator();
        _groupResults = _cache.Connect().Group(_keySelectionSubject, _regroupSubject).AsAggregator();
        _cleanup = _keySelectionSubject.Subscribe(func => _groupKeySelector = func, static _ => { });
    }

    [Fact]
    public void ResultEmptyIfSelectionKeyDoesNotFire()
    {
        // Arrange

        // Act
        InitialPopulate();

        // Assert
        _results.Data.Count.Should().Be(InitialCount);
        _results.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        _groupResults.Messages.Count.Should().Be(0);
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsAllInitialChildren()
    {
        // Arrange
        InitialPopulate();

        // Act
        GroupByFavColor();

        // Assert
        _results.Data.Count.Should().Be(InitialCount);
        _results.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        _groupResults.Groups.Items.ForEach(group => group.Messages.Count.Should().Be(1));
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsAllAddedChildren()
    {
        // Arrange
        GroupByFavColor();

        // Act
        InitialPopulate();

        // Assert
        _results.Data.Count.Should().Be(InitialCount);
        _results.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsAddedValues()
    {
        // Arrange
        InitialPopulate();
        GroupByPetType();

        // Act
        _cache.AddOrUpdate(_faker.Generate(AddCount));

        // Assert
        _results.Data.Count.Should().Be(InitialCount + AddCount);
        _results.Messages.Count.Should().Be(2, "Initial Adds and then the subsequent Additions should each be a single message");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultDoesNotContainRemovedValues()
    {
        // Arrange
        InitialPopulate();
        GroupByPetType();

        // Act
        _cache.RemoveKeys(_randomizer.ListItems(_cache.Items.ToList(), RemoveCount).Select(p => p.UniqueKey));

        // Assert
        _results.Data.Count.Should().Be(InitialCount - RemoveCount);
        _results.Messages.Count.Should().Be(2, "1 for Adds and 1 for Removes");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsUpdatedValues()
    {
        // Arrange
        GroupByPetType();
        InitialPopulate();
        var replacements = _randomizer.ListItems(_cache.Items.ToList(), UpdateCount)
            .Select(replacePerson => Person.CloneUniqueId(_faker.Generate(), replacePerson));

        // Act
        _cache.AddOrUpdate(replacements);

        // Assert
        _results.Data.Count.Should().Be(InitialCount, "Only replacements were made");
        _results.Messages.Count.Should().Be(2, "1 for Adds and 1 for Updates");
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultIsCorrectWhenGroupSelectorChanges()
    {
        // Arrange
        InitialPopulate();
        GroupByFavColor();
        var usedColorList = _cache.Items.Select(p => p.FavoriteColor).Distinct().Select(x => x.ToString()).ToList();
        var usedPetTypeList = _cache.Items.Select(p => p.PetType).Distinct().Select(x => x.ToString()).ToList();

        // Act
        GroupByPetType();

        // Assert
        _results.Data.Count.Should().Be(InitialCount);
        _results.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        _groupResults.Summary.Overall.Adds.Should().Be(usedColorList.Count + usedPetTypeList.Count);
        _groupResults.Summary.Overall.Removes.Should().Be(usedColorList.Count);
        _groupResults.Groups.Items.ForEach(group => group.Messages.Count.Should().BeLessThanOrEqualTo(2));
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultIsCorrectAfterForcedRegroup()
    {
        // Arrange
        InitialPopulate();
        GroupByFavColor();
        _cache.Items.ForEach(person => person.FavoriteColor = _randomizer.RandomColor(person.FavoriteColor));

        // Act
        ForceRegroup();

        // Assert
        _results.Data.Count.Should().Be(InitialCount);
        _results.Messages.Count.Should().Be(1, "The child observables fire on subscription so everything should appear as a single changeset");
        _groupResults.Groups.Items.ForEach(group => group.Messages.Count.Should().BeLessThanOrEqualTo(2, "1 for adds and 1 for regrouping"));
        VerifyGroupingResults();
    }


    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void ResultCompletesOnlyWhenAllInputsComplete(bool completeSource, bool completeKeySelector, bool completeRegrouper)
    {
        // Arrange
        InitialPopulate();
        GroupByFavColor();

        // Act
        if (completeSource)
        {
            _cache.Dispose();
        }
        if (completeKeySelector)
        {
            _keySelectionSubject.OnCompleted();
        }
        if (completeRegrouper)
        {
            _regroupSubject.OnCompleted();
        }

        // Assert
        _results.IsCompleted.Should().Be(completeSource);
        _groupResults.IsCompleted.Should().Be(completeSource && completeKeySelector && completeRegrouper);
    }

    [Fact]
    public void ResultFailsIfSourceFails()
    {
        // Arrange
        InitialPopulate();
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<Person, string>>(expectedError);
        using var results = _cache.Connect().Concat(throwObservable).Group(_keySelectionSubject, _regroupSubject).AsAggregator();

        // Act
        _cache.Dispose();

        // Assert
        results.Error.Should().Be(expectedError);
    }

    [Fact]
    public void ResultFailsIfGroupObservableFails()
    {
        // Arrange
        InitialPopulate();
        var expectedError = new Exception("Expected");

        // Act
        _keySelectionSubject.OnError(expectedError);

        // Assert
        _groupResults.Error.Should().Be(expectedError);
    }

    [Fact]
    public void ResultFailsIfRegrouperFails()
    {
        // Arrange
        InitialPopulate();
        var expectedError = new Exception("Expected");

        // Act
        _regroupSubject.OnError(expectedError);

        // Assert
        _groupResults.Error.Should().Be(expectedError);
    }

    public void Dispose()
    {
        _groupResults.Dispose();
        _results.Dispose();
        _cache.Dispose();
        _cleanup.Dispose();
        _keySelectionSubject.Dispose();
        _regroupSubject.Dispose();
    }

    private void InitialPopulate() => _cache.AddOrUpdate(_faker.Generate(InitialCount));

    private void VerifyGroupingResults() =>
        VerifyGroupingResults(_cache, _results, _groupResults, _groupKeySelector);

    private static void VerifyGroupingResults(ISourceCache<Person, string> cache, ChangeSetAggregator<Person, string> cacheResults, GroupChangeSetAggregator<Person, string, string> groupResults, Func<Person, string, string>? groupKeySelector)
    {
        if (groupKeySelector is null)
        {
            groupResults.Data.Count.Should().Be(0);
            groupResults.Groups.Count.Should().Be(0);
            return;
        }

        var expectedItems = cache.Items.ToList();
        var expectedGroupings = expectedItems.GroupBy(p => groupKeySelector(p, string.Empty)).ToList();

        // These datasets should be equivalent
        expectedItems.Should().BeEquivalentTo(cacheResults.Data.Items);
        expectedGroupings.Select(g => g.Key).Should().BeEquivalentTo(groupResults.Groups.Keys);

        // Check each group
        expectedGroupings.ForEach(grouping => grouping.Should().BeEquivalentTo(groupResults.Groups.Lookup(grouping.Key).Value.Data.Items));
    }

    private void ForceRegroup() => _regroupSubject.OnNext(Unit.Default);

    private void GroupByFavColor() => _keySelectionSubject.OnNext(FavColor);

    private void GroupByParentName() => _keySelectionSubject.OnNext(ParentName);

    private void GroupByPetType() => _keySelectionSubject.OnNext(PetType);

    private static string FavColor(Person person, string _) => person.FavoriteColor.ToString();

    private static string ParentName(Person person, string _) => person.ParentName ?? string.Empty;

    private static string PetType(Person person, string _) => person.PetType.ToString();
}
