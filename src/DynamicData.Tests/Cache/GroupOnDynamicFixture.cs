using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;
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
    private readonly ReactiveUI.Primitives.Signals.StateSignal<Func<Person, string, string>?> _keySelectionSubject = new(null);
    private readonly ReactiveUI.Primitives.Signals.Signal<Unit> _regroupSubject = new();

    public GroupOnDynamicFixture()
    {
        unchecked { _randomizer = new((int)0xc001_d00d); }
        _faker = Fakers.Person.Clone().WithSeed(_randomizer);
        _results = _cache.Connect().AsAggregator();
        _groupResults = _cache.Connect().Group(KeySelectionObservable, _regroupSubject).AsAggregator();
    }

    [Test]
    [Arguments(5)]
    [Arguments(10)]
#if !DEBUG
    [Arguments(200)]
    [Arguments(500)]
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
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultEmptyIfSelectionKeyDoesNotFire()
    {
        // Arrange

        // Act
        InitialPopulate();

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("The child observables fire on subscription so everything should appear as a single changeset");
        await Assert.That(_groupResults.Messages.Count).IsEqualTo(0);
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultContainsAllInitialChildren()
    {
        // Arrange
        InitialPopulate();

        // Act
        GroupByFavColor();

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("The child observables fire on subscription so everything should appear as a single changeset");
        foreach (var group in _groupResults.Groups.Items) { await Assert.That(group.Messages.Count).IsEqualTo(1); }
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultContainsAllAddedChildren()
    {
        // Arrange
        GroupByFavColor();

        // Act
        InitialPopulate();

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("The child observables fire on subscription so everything should appear as a single changeset");
        foreach (var group in _groupResults.Groups.Items) { await Assert.That(group.Messages.Count).IsEqualTo(1); }
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultContainsAddedValues()
    {
        // Arrange
        InitialPopulate();
        GroupByPetType();

        // Act
        _cache.AddOrUpdate(_faker.Generate(AddCount));

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount + AddCount);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("Initial Adds and then the subsequent Additions should each be a single message");
        foreach (var group in _groupResults.Groups.Items) { await Assert.That(group.Messages.Count).IsLessThanOrEqualTo(2); }
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultDoesNotContainRemovedValues()
    {
        // Arrange
        InitialPopulate();
        GroupByPetType();

        // Act
        _cache.RemoveKeys(_randomizer.ListItems(_cache.Items.ToList(), RemoveCount).Select(p => p.UniqueKey));

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount - RemoveCount);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("1 for Adds and 1 for Removes");
        foreach (var group in _groupResults.Groups.Items) { await Assert.That(group.Messages.Count).IsLessThanOrEqualTo(2); }
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultContainsUpdatedValues()
    {
        // Arrange
        GroupByPetType();
        InitialPopulate();
        var replacements = _randomizer.ListItems(_cache.Items.ToList(), UpdateCount)
            .Select(replacePerson => Person.CloneUniqueId(_faker.Generate(), replacePerson));

        // Act
        _cache.AddOrUpdate(replacements);

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount).Because("Only replacements were made");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("1 for Adds and 1 for Updates");
        foreach (var group in _groupResults.Groups.Items) { await Assert.That(group.Messages.Count).IsLessThanOrEqualTo(2); }
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultContainsRefreshedValues()
    {
        // Arrange
        GroupByPetType();
        InitialPopulate();
        var refreshList = _randomizer.ListItems(_cache.Items.ToList(), UpdateCount);
        refreshList.ForEach(person => person.PetType = _randomizer.Enum<AnimalFamily>());

        // Act
        _cache.Refresh(refreshList);

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount).Because("Only replacements were made");
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("1 for Adds and 1 for Updates");
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultIsCorrectWhenGroupSelectorChanges()
    {
        // Arrange
        InitialPopulate();
        GroupByFavColor();
        var usedColorList = _cache.Items.Select(p => p.FavoriteColor).Distinct().Select(x => x.ToString()).ToList();
        var usedPetTypeList = _cache.Items.Select(p => p.PetType).Distinct().Select(x => x.ToString()).ToList();

        // Act
        GroupByPetType();

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("The child observables fire on subscription so everything should appear as a single changeset");
        await Assert.That(_groupResults.Summary.Overall.Adds).IsEqualTo(usedColorList.Count + usedPetTypeList.Count);
        await Assert.That(_groupResults.Summary.Overall.Removes).IsEqualTo(usedColorList.Count);
        foreach (var group in _groupResults.Groups.Items) { await Assert.That(group.Messages.Count).IsLessThanOrEqualTo(2); }
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultIsCorrectAfterForcedRegroup()
    {
        // Arrange
        InitialPopulate();
        GroupByFavColor();
        _cache.Items.ForEach(person => person.FavoriteColor = _randomizer.RandomColor(person.FavoriteColor));

        // Act
        ForceRegroup();

        // Assert
        await Assert.That(_results.Data.Count).IsEqualTo(InitialCount);
        await Assert.That(_results.Messages.Count).IsEqualTo(1).Because("The child observables fire on subscription so everything should appear as a single changeset");
        foreach (var group in _groupResults.Groups.Items) { await Assert.That(group.Messages.Count).IsLessThanOrEqualTo(2).Because("1 for adds and 1 for regrouping"); }
        await VerifyGroupingResults();
    }

    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
    public async Task ResultCompletesOnlyWhenAllInputsComplete(bool completeSource, bool completeKeySelector, bool completeRegrouper)
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
        await Assert.That(_results.IsCompleted).IsEqualTo(completeSource);
        await Assert.That(_groupResults.IsCompleted).IsEqualTo(completeSource && completeKeySelector && completeRegrouper);
    }

    [Test]
    public async Task ResultFailsIfSourceFails()
    {
        // Arrange
        InitialPopulate();
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<Person, string>>(expectedError);
        using var results = _cache.Connect().Concat(throwObservable).Group(KeySelectionObservable, _regroupSubject).AsAggregator();

        // Act
        _cache.Dispose();

        // Assert
        await Assert.That(results.Error).IsEqualTo(expectedError);
    }

    [Test]
    public async Task ResultFailsIfGroupObservableFails()
    {
        // Arrange
        InitialPopulate();
        var expectedError = new Exception("Expected");

        // Act
        _keySelectionSubject.OnError(expectedError);

        // Assert
        await Assert.That(_groupResults.Error).IsEqualTo(expectedError);
    }

    [Test]
    public async Task ResultFailsIfRegrouperFails()
    {
        // Arrange
        InitialPopulate();
        var expectedError = new Exception("Expected");

        // Act
        _regroupSubject.OnError(expectedError);

        // Assert
        await Assert.That(_groupResults.Error).IsEqualTo(expectedError);
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

    private Task VerifyGroupingResults() =>
        VerifyGroupingResults(_cache, _results, _groupResults, _keySelectionSubject.Value);

    private static async Task VerifyGroupingResults(ISourceCache<Person, string> cache, ChangeSetAggregator<Person, string> cacheResults, GroupChangeSetAggregator<Person, string, string> groupResults, Func<Person, string, string>? groupKeySelector)
    {
        if (groupKeySelector is null)
        {
            await Assert.That(groupResults.Data.Count).IsEqualTo(0);
            await Assert.That(groupResults.Groups.Count).IsEqualTo(0);
            return;
        }

        var expectedItems = cache.Items.ToList();
        var expectedGroupings = expectedItems.GroupBy(p => groupKeySelector(p, string.Empty)).ToList();

        // These datasets should be equivalent
        await Assert.That(expectedItems).IsEquivalentTo(cacheResults.Data.Items);
        await Assert.That(expectedGroupings.Select(g => g.Key)).IsEquivalentTo(groupResults.Groups.Keys);

        // Check each group
        foreach (var grouping in expectedGroupings) { await Assert.That(grouping).IsEquivalentTo(groupResults.Groups.Lookup(grouping.Key).Value.Data.Items); }

        // No groups should be empty
        foreach (var group in groupResults.Groups.Items) { await Assert.That(group.Data.Count).IsGreaterThan(0).Because("Empty groups should be removed"); }
    }

    private void ForceRegroup() => _regroupSubject.OnNext(Unit.Default);

    private void GroupByFavColor() => _keySelectionSubject.OnNext(FavColor);

    private void GroupByParentName() => _keySelectionSubject.OnNext(ParentName);

    private void GroupByPetType() => _keySelectionSubject.OnNext(PetType);

    private static string FavColor(Person person, string _) => person.FavoriteColor.ToString();

    private static string ParentName(Person person, string _) => person.ParentName ?? string.Empty;

    private static string PetType(Person person, string _) => person.PetType.ToString();
}
