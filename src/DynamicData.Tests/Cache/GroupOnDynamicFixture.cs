using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;

using Person = DynamicData.Tests.Domain.Person;

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
    private readonly BehaviorSubject<Func<Person, string, string>?> _keySelectionSubject = new (null);
    private readonly Subject<Unit> _regroupSubject = new ();

    public GroupOnDynamicFixture()
    {
        unchecked { _randomizer = new((int)0xc001_d00d); }
        _faker = Fakers.Person.Clone().WithSeed(_randomizer);
        _results = _cache.Connect().AsAggregator();
        _groupResults = _cache.Connect().Group(KeySelectionObservable, _regroupSubject).AsAggregator();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
#if !DEBUG
    [InlineData(200)]
    [InlineData(500)]
#endif
    public async Task MultiThreadedStressTest(int changeCount)
    {
        var MaxIntervalTime = TimeSpan.FromMilliseconds(10);

        var taskCacheChanges = Task.Run(async () =>
            await _randomizer.Interval(MaxIntervalTime)
                .Take(changeCount)
                .Do(x =>
                    _cache.Edit(updater =>
                    {
                        if ((x % 2 == 0) || updater.Count == 0)
                        {
                            updater.AddOrUpdate(_faker.Generate(AddCount));
                        }
                        else
                        {
                            updater.RemoveKeys(_randomizer.ListItems(updater.Items.ToList(), Math.Min(RemoveCount, updater.Count - 1)).Select(p => p.UniqueKey));
                        }
                    })));

        var taskGrouperChanges = Task.Run(async () =>
            await _randomizer.Interval(MaxIntervalTime)
                .Take(changeCount)
                .Select<long, Action>(x => (x % 3) switch
                {
                    0L => GroupByFavColor,
                    1L => GroupByParentName,
                    2L => GroupByPetType,
                    _ => throw new NotImplementedException()
                })
                .Do(action => action.Invoke()));

        var taskRegrouperChanges = Task.Run(async () =>
            await _randomizer.Interval(MaxIntervalTime)
                .Take(changeCount)
                .Do(x =>
                {
                    _cache.Edit(updater =>
                    {
                        if (updater.Count > 0)
                        {
                            var changeList = _randomizer.ListItems(updater.Items.ToList(), Math.Min(UpdateCount, updater.Count - 1));
                            changeList.ForEach(person => person.PetType = _randomizer.Enum<AnimalFamily>());
                            changeList.ForEach(person => person.FavoriteColor = _randomizer.Enum<Color>());
                        }
                    });
                    ForceRegroup();
                }));

        await Task.WhenAll(taskCacheChanges, taskGrouperChanges, taskRegrouperChanges);

        // Verify the results
        VerifyGroupingResults();
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
        _groupResults.Groups.Items.ForEach(group => group.Messages.Count.Should().Be(1));
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
        _groupResults.Groups.Items.ForEach(group => group.Messages.Count.Should().BeLessThanOrEqualTo(2));
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
        _groupResults.Groups.Items.ForEach(group => group.Messages.Count.Should().BeLessThanOrEqualTo(2));
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
        _groupResults.Groups.Items.ForEach(group => group.Messages.Count.Should().BeLessThanOrEqualTo(2));
        VerifyGroupingResults();
    }

    [Fact]
    public void ResultContainsRefreshedValues()
    {
        // Arrange
        GroupByPetType();
        InitialPopulate();
        var refreshList = _randomizer.ListItems(_cache.Items.ToList(), UpdateCount);
        refreshList.ForEach(person => person.PetType = _randomizer.Enum<AnimalFamily>());

        // Act
        _cache.Refresh(refreshList);

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
        using var results = _cache.Connect().Concat(throwObservable).Group(KeySelectionObservable, _regroupSubject).AsAggregator();

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
        _keySelectionSubject.Dispose();
        _regroupSubject.Dispose();
    }

    private IObservable<Func<Person, string, string>> KeySelectionObservable => _keySelectionSubject.Where(v => v is not null).Select(v => v!);

    private void InitialPopulate() => _cache.AddOrUpdate(_faker.Generate(InitialCount));

    private void VerifyGroupingResults() =>
        VerifyGroupingResults(_cache, _results, _groupResults, _keySelectionSubject.Value);

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

        // No groups should be empty
        groupResults.Groups.Items.ForEach(group => group.Data.Count.Should().BeGreaterThan(0, "Empty groups should be removed"));
    }

    private void ForceRegroup() => _regroupSubject.OnNext(Unit.Default);

    private void GroupByFavColor() => _keySelectionSubject.OnNext(FavColor);

    private void GroupByParentName() => _keySelectionSubject.OnNext(ParentName);

    private void GroupByPetType() => _keySelectionSubject.OnNext(PetType);

    private static string FavColor(Person person, string _) => person.FavoriteColor.ToString();

    private static string ParentName(Person person, string _) => person.ParentName ?? string.Empty;

    private static string PetType(Person person, string _) => person.PetType.ToString();
}
