using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;
using Person = DynamicData.Tests.Domain.Person;

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
    private readonly SourceCache<Person, string> _cache = new(p => p.UniqueKey);
    private readonly ChangeSetAggregator<Person, string> _results;
    private readonly GroupChangeSetAggregator<Person, string, Color> _groupResults;
    private readonly ReactiveUI.Primitives.Signals.Signal<Unit> _grouperShutdown;
    private readonly Faker<Person> _faker;
    private readonly Randomizer _randomizer = new(0x3141_5926);

    public GroupOnObservableFixture()
    {
        _faker = Fakers.Person.Clone().WithSeed(_randomizer);
        _grouperShutdown = new();
        _results = _cache.Connect().AsAggregator();
        _groupResults = _cache.Connect().GroupOnObservable(CreateFavoriteColorObservable).AsAggregator();
    }

    [Test]
    public async Task ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act
        _cache.AddOrUpdate(_faker.Generate(InitialCount));

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
        _cache.AddOrUpdate(_faker.Generate(InitialCount));

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
        _cache.AddOrUpdate(_faker.Generate(InitialCount));

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
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
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
    public async Task GroupRemovedWhenEmpty()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
        var usedColorList = _cache.Items.Select(p => p.FavoriteColor).Distinct().ToList();
        var removeColor = _randomizer.ListItem(usedColorList);
        var colorCount = usedColorList.Count;

        // Act
        _cache.Edit(updater => updater.Remove(updater.Items.Where(p => p.FavoriteColor == removeColor).Select(p => p.UniqueKey)));

        // Assert
        await Assert.That(_cache.Items.Select(p => p.FavoriteColor).Distinct().Count()).IsEqualTo(colorCount - 1);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("1 for Adds and 1 for Removes");
        await Assert.That(_groupResults.Data.Count).IsEqualTo(colorCount - 1).Because($"{colorCount} colors were used and then all of the {removeColor} were removed");
        await Assert.That(_groupResults.Messages.Count).IsEqualTo(2).Because("1 for Adds and 1 for Removes");
        await Assert.That(_groupResults.Summary.Overall.Adds).IsEqualTo(colorCount);
        await Assert.That(_groupResults.Summary.Overall.Removes).IsEqualTo(1);
        await VerifyGroupingResults();
    }

    [Test]
    public async Task GroupNotRemovedIfAddedBackImmediately()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
        var usedColorList = _cache.Items.Select(p => p.FavoriteColor).Distinct().ToList();
        var removeColor = _randomizer.ListItem(usedColorList);
        var colorCount = usedColorList.Count;

        // Act
        _cache.Edit(updater =>
        {
            updater.Remove(updater.Items.Where(p => p.FavoriteColor == removeColor).Select(p => p.UniqueKey));
            var newPerson = _faker.Generate();
            newPerson.FavoriteColor = removeColor;
            updater.AddOrUpdate(newPerson);
        });

        // Assert
        await Assert.That(_cache.Items.Select(p => p.FavoriteColor).Distinct().Count()).IsEqualTo(colorCount);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("1 for Adds and 1 for Other Added Value");
        await Assert.That(_groupResults.Data.Count).IsEqualTo(colorCount);
        await Assert.That(_groupResults.Messages.Count).IsEqualTo(1).Because("Shouldn't be removed/re-added");
        await Assert.That(_groupResults.Summary.Overall.Adds).IsEqualTo(colorCount);
        await Assert.That(_groupResults.Summary.Overall.Removes).IsEqualTo(0);
        await Assert.That(_groupResults.Groups.Lookup(removeColor).Value.Data.Count).IsEqualTo(1).Because($"All the {removeColor} were removed and then 1 was added back");
        await VerifyGroupingResults();
    }

    [Test]
    public async Task GroupingSequenceCompletesWhenEmpty()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
        var usedColorList = _cache.Items.Select(p => p.FavoriteColor).Distinct().ToList();
        var removeColor = _randomizer.ListItem(usedColorList);

        var results = _cache.Connect().GroupOnObservable(CreateFavoriteColorObservable)
            .Filter(grp => grp.Key == removeColor)
            .Take(1)
            .MergeMany(grp => grp.Cache.Connect())
            .AsAggregator();

        // Act
        _cache.Edit(updater => updater.Remove(updater.Items.Where(p => p.FavoriteColor == removeColor).Select(p => p.UniqueKey)));

        // Assert
        await Assert.That(results.IsCompleted).IsTrue();
        await VerifyGroupingResults();
    }

    [Test]
    public async Task AllSequencesShouldCompleteWhenSourceAndGroupingObservablesComplete()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));

        var results = _cache.Connect().GroupOnObservable(CreateFavoriteColorObservable)
            .MergeMany(grp => grp.Cache.Connect())
            .AsAggregator();

        // Act
        _cache.Dispose();
        _grouperShutdown.OnNext(Unit.Default);

        // Assert
        await Assert.That(results.IsCompleted).IsTrue();
        await VerifyGroupingResults();
    }

    [Test]
    public async Task AllGroupsRemovedWhenCleared()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
        var usedColorList = _cache.Items.Select(p => p.FavoriteColor).Distinct().ToList();
        var colorCount = usedColorList.Count;

        // Act
        _cache.Clear();

        // Assert
        await Assert.That(_cache.Items.Count).IsEqualTo(0);
        await Assert.That(_results.Messages.Count).IsEqualTo(2).Because("1 for Adds and 1 for Removes");
        await Assert.That(_groupResults.Summary.Overall.Adds).IsEqualTo(colorCount);
        await Assert.That(_groupResults.Summary.Overall.Removes).IsEqualTo(colorCount);
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultsContainsCorrectRegroupedValues()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));

        // Act
        Enumerable.Range(0, UpdateCount).ForEach(_ => RandomFavoriteColorChange());

        // Assert
        await VerifyGroupingResults();
    }

    [Test]
    public async Task ResultsContainsCorrectRegroupedValuesAsync()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
        var tasks = Enumerable.Range(0, UpdateCount).Select(_ => Task.Run(RandomFavoriteColorChange));

        // Act
        await Task.WhenAll(tasks.ToArray());

        // Assert
        await VerifyGroupingResults();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task ResultCompletesOnlyWhenSourceAndAllGroupingObservablesComplete(bool completeSource, bool completeGroups)
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));

        // Act
        if (completeSource)
        {
            _cache.Dispose();
        }
        if (completeGroups)
        {
            _grouperShutdown.OnNext(Unit.Default);
        }

        // Assert
        await Assert.That(_results.IsCompleted).IsEqualTo(completeSource);
        await Assert.That(_groupResults.IsCompleted).IsEqualTo(completeGroups && completeSource);
    }

    [Test]
    public async Task ResultFailsIfSourceFails()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<Person, string>>(expectedError);
        using var results = _cache.Connect().Concat(throwObservable).GroupOnObservable(CreateFavoriteColorObservable).AsAggregator();

        // Act
        _cache.Dispose();

        // Assert
        await Assert.That(results.Error).IsEqualTo(expectedError);
    }

    [Test]
    public async Task ResultFailsIfGroupObservableFails()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<Color>(expectedError);

        // Act
        using var results = _cache.Connect().GroupOnObservable((person, key) => CreateFavoriteColorObservable(person, key).Take(1).Concat(throwObservable)).AsAggregator();

        // Assert
        await Assert.That(results.Error).IsEqualTo(expectedError);
    }

    [Test]
    public async Task OnErrorFiresIfSelectorThrows()
    {
        // Arrange
        _cache.AddOrUpdate(_faker.Generate(InitialCount));
        var expectedError = new Exception("Expected");

        // Act
        using var results = _cache.Connect().GroupOnObservable<Person, string, Color>(_ => throw expectedError).AsAggregator();

        // Assert
        await Assert.That(results.Error).IsEqualTo(expectedError);
    }

    public void Dispose()
    {
        _groupResults.Dispose();
        _results.Dispose();
        _cache.Dispose();
        _grouperShutdown.Dispose();
    }

    private void RandomFavoriteColorChange()
    {
        var person = _randomizer.ListItem(_cache.Items.ToList());
        lock (person)
        {
            // Pick a new favorite color
            person.FavoriteColor = _randomizer.RandomColor(person.FavoriteColor);
        }
    }

    private Task VerifyGroupingResults() =>
        VerifyGroupingResults(_cache, _results, _groupResults);

    private static async Task VerifyGroupingResults(ISourceCache<Person, string> cache, ChangeSetAggregator<Person, string> cacheResults, GroupChangeSetAggregator<Person, string, Color> groupResults)
    {
        var expectedPersons = cache.Items.ToList();
        var expectedGroupings = cache.Items.GroupBy(p => p.FavoriteColor).ToList();

        // These datasets should be equivalent
        await Assert.That(expectedPersons).IsEquivalentTo(cacheResults.Data.Items);
        await Assert.That(groupResults.Groups.Keys).IsEquivalentTo(expectedGroupings.Select(g => g.Key));

        // Check each group
        foreach (var grouping in expectedGroupings) { await Assert.That(grouping).IsEquivalentTo(groupResults.Groups.Lookup(grouping.Key).Value.Data.Items); }

        // No groups should be empty
        foreach (var group in groupResults.Groups.Items) { await Assert.That(group.Data.Count).IsGreaterThan(0).Because("Empty groups should be removed"); }
    }

    private IObservable<Color> CreateFavoriteColorObservable(Person person, string key) =>
         person.WhenPropertyChanged(p => p.FavoriteColor).Select(change => change.Value).TakeUntil(_grouperShutdown);
}
