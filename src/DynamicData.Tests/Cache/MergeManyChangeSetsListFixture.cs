using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public sealed class MergeManyChangeSetsListFixture : IDisposable
{
#if DEBUG
    const int InitialOwnerCount = 7;
    const int AddRangeSize = 5;
    const int RemoveRangeSize = 3;
#else
    const int InitialOwnerCount = 103;
    const int AddRangeSize = 53;
    const int RemoveRangeSize = 37;
#endif

    private readonly ISourceCache<AnimalOwner, Guid> _animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
    private readonly ChangeSetAggregator<AnimalOwner, Guid> _animalOwnerResults;
    private readonly ChangeSetAggregator<Animal> _animalResults;
    private readonly Faker<AnimalOwner> _animalOwnerFaker;
    private readonly Faker<Animal> _animalFaker;
    private readonly Randomizer _randomizer;

    public MergeManyChangeSetsListFixture()
    {
        _randomizer = new Randomizer(0x01221948);
        _animalFaker = Fakers.Animal.Clone().WithSeed(_randomizer);
        _animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(_randomizer).WithInitialAnimals(_animalFaker);
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        _animalOwnerResults = _animalOwners.Connect().AsAggregator();
        _animalResults = _animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();
    }

    [Test]
    [Arguments(5, 7)]
    [Arguments(10, 50)]
#if !DEBUG
    [Arguments(10, 1_000)]
    [Arguments(200, 500)]
    [Arguments(1_000, 10)]
#endif
    public async Task MultiThreadedStressTest(int ownerCount, int animalCount)
    {
        var MaxAddTime = TimeSpan.FromSeconds(0.250);
        var MaxRemoveTime = TimeSpan.FromSeconds(0.100);

        TimeSpan? GetRemoveTime() => _randomizer.Bool() ? _randomizer.TimeSpan(MaxRemoveTime) : null;

        IObservable<Unit> AddRemoveAnimalsStress(int ownerCount, int animalCount, int parallel, IScheduler scheduler) =>
            Observable.Create<Unit>(observer => new CompositeDisposable
                (
                    AddRemoveOwners(ownerCount, parallel, scheduler)
                        .Subscribe(
                            onNext: static _ => { },
                            onError: observer.OnError),

                    _animalOwners.Connect()
                        .MergeMany(owner => AddRemoveAnimals(owner, animalCount, parallel, scheduler))
                        .Subscribe(
                            onNext: static _ => { },
                            onError: observer.OnError,
                            onCompleted: observer.OnCompleted)
                ));

        IObservable<AnimalOwner> AddRemoveOwners(int ownerCount, int parallel, IScheduler scheduler) =>
            _animalOwnerFaker.IntervalGenerate(_randomizer, MaxAddTime, scheduler)
                .Parallelize(ownerCount, parallel, obs => obs.StressAddRemove(_animalOwners, _ => GetRemoveTime(), scheduler))
                .Finally(_animalOwners.Dispose);

        IObservable<Animal> AddRemoveAnimals(AnimalOwner owner, int animalCount, int parallel, IScheduler scheduler) =>
            _animalFaker.IntervalGenerate(_randomizer, MaxAddTime, scheduler)
                .Parallelize(animalCount, parallel, obs => obs.StressAddRemove(owner.Animals, _ => GetRemoveTime(), scheduler))
                .Finally(owner.Animals.Dispose);

        var mergeAnimals = _animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).Publish();
        var addingAnimals = true;
        var cacheCompleted = mergeAnimals.LastOrDefaultAsync().ToTask();
        using var animalResults = mergeAnimals.AsAggregator();
        using var connect = mergeAnimals.Connect();

        // Start asynchrononously modifying the parent list and the child lists
        using var addAnimals = AddRemoveAnimalsStress(ownerCount, animalCount, Environment.ProcessorCount, TaskPoolScheduler.Default)
            .Finally(() => addingAnimals = false)
            .Subscribe();

        // Subscribe / unsubscribe over and over while the collections are being modified
        do
        {
            // Ensure items are being added asynchronously before subscribing to the animal changes
            await Task.Yield();

            {
                // Subscribe
                var mergedSub = mergeAnimals.Subscribe();

                // Let other threads run
                await Task.Yield();

                // Unsubscribe
                mergedSub.Dispose();
            }
        }
        while (addingAnimals);

        // Wait for the source cache to finish delivering all notifications.
        await cacheCompleted;

        // Verify the results against the aggregator wired into the same Publish chain
        // that cacheCompleted observes.
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Test]
    public async Task NullChecks()
    {
        // Arrange
        var emptyChangeSetObs = Observable.Empty<IChangeSet<int, int>>();
        var nullChangeSetObs = (IObservable<IChangeSet<int, int>>)null!;
        var emptyKeySelector = new Func<int, int, IObservable<IChangeSet<string>>>((_, _) => Observable.Empty<IChangeSet<string>>());
        var nullKeySelector = (Func<int, int, IObservable<IChangeSet<string>>>)null!;
        var emptySelector = new Func<int, IObservable<IChangeSet<string>>>(i => Observable.Empty<IChangeSet<string>>());
        var nullSelector = (Func<int, IObservable<IChangeSet<string>>>)null!;

        // Act
        var checkParam1 = () => nullChangeSetObs.MergeManyChangeSets(emptyKeySelector);
        var checkParam2 = () => emptyChangeSetObs.MergeManyChangeSets(nullKeySelector);
        var checkParam3 = () => nullChangeSetObs.MergeManyChangeSets(emptySelector);
        var checkParam4 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector);

        // Assert
        await Assert.That(emptyChangeSetObs).IsNotNull();
        await Assert.That(emptyKeySelector).IsNotNull();
        await Assert.That(emptySelector).IsNotNull();
        await Assert.That(nullChangeSetObs).IsNull();
        await Assert.That(nullKeySelector).IsNull();
        await Assert.That(nullSelector).IsNull();

        await Assert.That(checkParam1).Throws<ArgumentNullException>();
        await Assert.That(checkParam2).Throws<ArgumentNullException>();
        await Assert.That(checkParam3).Throws<ArgumentNullException>();
        await Assert.That(checkParam4).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(1);
        await CheckResultContents();
    }

    [Test]
    public async Task ResultContainsChildrenFromAddedParents()
    {
        // Arrange
        var addThis = _animalOwnerFaker.Generate();

        // Act
        _animalOwners.AddOrUpdate(addThis);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount + 1);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        foreach (var added in addThis.Animals.Items) { await Assert.That(_animalResults.Data.Items).Contains(added); }
        await CheckResultContents();
    }

    [Test]
    public async Task ResultDoesNotContainChildrenFromParentsRemovedWithRemove()
    {
        // Arrange
        var removeThis = _randomizer.ListItem(_animalOwners.Items.ToList());

        // Act
        _animalOwners.Remove(removeThis);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount - 1);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        foreach (var removed in removeThis.Animals.Items) { await Assert.That(_animalResults.Data.Items).DoesNotContain(removed); }
        await CheckResultContents();
        removeThis.Dispose();
    }

    [Test]
    public async Task ResultDoesNotContainChildrenFromParentsBatchRemoved()
    {
        // Arrange
        var removeThese = _randomizer.ListItems(_animalOwners.Items.ToList(), RemoveRangeSize);

        // Act
        _animalOwners.Remove(removeThese);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount - RemoveRangeSize);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        foreach (var removed in removeThese.SelectMany(owner => owner.Animals.Items)) { await Assert.That(_animalResults.Data.Items).DoesNotContain(removed); }
        await CheckResultContents();
        removeThese.ForEach(owner => owner.Dispose());
    }

    [Test]
    public async Task ResultContainsCorrectItemsAfterParentUpdate()
    {
        // Arrange
        var replaceThis = _randomizer.ListItem(_animalOwners.Items.ToList());
        var withThis = CreateWithSameId(replaceThis);

        // Act
        _animalOwners.AddOrUpdate(withThis);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount); // Owner Count should not change
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 2 = Initial Add and one changeset with remove old items / add new items
        foreach (var removed in replaceThis.Animals.Items) { await Assert.That(_animalResults.Data.Items).DoesNotContain(removed); }
        foreach (var added in withThis.Animals.Items) { await Assert.That(_animalResults.Data.Items).Contains(added); }
        await CheckResultContents();
        replaceThis.Dispose();
    }

    [Test]
    public async Task ResultEmptyIfSourceIsCleared()
    {
        // Arrange
        var items = _animalOwners.Items.ToList();

        // Act
        _animalOwners.Clear();

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(0);
        await Assert.That(_animalResults.Data.Count).IsEqualTo(0);
        await CheckResultContents();
        items.ForEach(owner => owner.Dispose());
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithAddRange()
    {
        // Arrange
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);
        var totalAdded = new List<Animal>();

        // Act
        _animalOwners.Items.ForEach(owner => owner.Animals.AddRange(_animalFaker.Generate(AddRangeSize).With(added => totalAdded.AddRange(added))));

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(1 + InitialOwnerCount); // Initial + 1 for each Range Added
        foreach (var animal in totalAdded) { await Assert.That(_animalResults.Data.Items).Contains(animal); }
        await Assert.That(_animalOwners.Items.Sum(owner => owner.Animals.Count)).IsEqualTo(initialCount + totalAdded.Count);
        await CheckResultContents();
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithInsert()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var insertIndex = _randomizer.Number(randomOwner.Animals.Items.Count);
        var insertThis = _animalFaker.Generate();
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Insert(insertIndex, insertThis);

        // Assert
        await Assert.That(randomOwner.Animals.Items.ElementAt(insertIndex)).IsEqualTo(insertThis);
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        await Assert.That(_animalResults.Data.Items).Contains(insertThis);
        await Assert.That(_animalOwners.Items.Sum(owner => owner.Animals.Count)).IsEqualTo(initialCount + 1);
        await CheckResultContents();
    }

    [Test]
    public async Task ResultDoesNotContainChildrenRemovedWithRemove()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeThis = _randomizer.ListItem(randomOwner.Animals.Items.ToList());
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Remove(removeThis);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        await Assert.That(_animalResults.Data.Items).DoesNotContain(removeThis);
        await Assert.That(_animalOwners.Items.Sum(owner => owner.Animals.Count)).IsEqualTo(initialCount - 1);
        await CheckResultContents();
    }

    [Test]
    public async Task ResultDoesNotContainChildrenRemovedWithRemoveAt()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeIndex = _randomizer.Number(randomOwner.Animals.Count - 1);
        var removeThis = randomOwner.Animals.Items.ElementAt(removeIndex);
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.RemoveAt(removeIndex);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        await Assert.That(_animalResults.Data.Items).DoesNotContain(removeThis);
        await Assert.That(_animalOwners.Items.Sum(owner => owner.Animals.Count)).IsEqualTo(initialCount - 1);
        await CheckResultContents();
    }

    [Test]
    public async Task ResultDoesNotContainChildrenRemovedWithRemoveRange()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeCount = _randomizer.Number(1, randomOwner.Animals.Count - 1);
        var removeIndex = _randomizer.Number(randomOwner.Animals.Count - removeCount - 1);
        var removeThese = randomOwner.Animals.Items.Skip(removeIndex).Take(removeCount);

        // Act
        randomOwner.Animals.RemoveRange(removeIndex, removeCount);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        foreach (var removed in removeThese) { await Assert.That(randomOwner.Animals.Items).DoesNotContain(removed); }
        await CheckResultContents();
    }

    [Test]
    public async Task ResultDoesNotContainChildrenRemovedWithRemoveMany()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeCount = _randomizer.Number(1, randomOwner.Animals.Count - 1);
        var removeThese = _randomizer.ListItems(randomOwner.Animals.Items.ToList(), removeCount);

        // Act
        randomOwner.Animals.RemoveMany(removeThese);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        foreach (var removed in removeThese) { await Assert.That(randomOwner.Animals.Items).DoesNotContain(removed); }
        await CheckResultContents();
    }

    [Test]
    public async Task ResultContainsCorrectItemsAfterChildReplacement()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var replaceThis = _randomizer.ListItem(randomOwner.Animals.Items.ToList());
        var withThis = _animalFaker.Generate();

        // Act
        randomOwner.Animals.Replace(replaceThis, withThis);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        await Assert.That(randomOwner.Animals.Items).DoesNotContain(replaceThis);
        await Assert.That(randomOwner.Animals.Items).Contains(withThis);
        await CheckResultContents();
    }

    [Test]
    public async Task ResultContainsCorrectItemsAfterChildClear()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removedAnimals = randomOwner.Animals.Items.ToList();

        // Act
        randomOwner.Animals.Clear();

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2);
        await Assert.That(randomOwner.Animals.Count).IsEqualTo(0);
        foreach (var removed in removedAnimals) { await Assert.That(_animalResults.Data.Items).DoesNotContain(removed); }
        await CheckResultContents();
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ResultCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
    {
        // Arrange

        // Act
        _animalOwners.Items.Skip(completeChildren ? 0 : 1).ForEach(owner => owner.Dispose());
        if (completeSource)
        {
            _animalOwners.Dispose();
        }

        // Assert
        await Assert.That(_animalOwnerResults.IsCompleted).IsEqualTo(completeSource);
        await Assert.That(_animalResults.IsCompleted).IsEqualTo(completeSource && completeChildren);
    }

    [Test]
    public async Task ResultFailsIfSourceFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<AnimalOwner, Guid>>(expectedError);
        using var results = _animalOwners.Connect().Concat(throwObservable).MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

        // Act
        _animalOwners.Dispose();

        // Assert
        await Assert.That(results.Exception).IsEqualTo(expectedError);
    }

    public void Dispose()
    {
        _animalOwners.Items.ForEach(owner => owner.Dispose());
        _animalOwnerResults.Dispose();
        _animalResults.Dispose();
        _animalOwners.Dispose();
    }

    private AnimalOwner CreateWithSameId(AnimalOwner original)
    {
        var newOwner = _animalOwnerFaker.Generate();
        var sameId = new AnimalOwner(newOwner.Name, original.Id);
        sameId.Animals.AddRange(newOwner.Animals.Items);
        return sameId;
    }

    private Task CheckResultContents() => CheckResultContents(_animalOwners.Items, _animalOwnerResults, _animalResults);

    private static async Task CheckResultContents(IEnumerable<AnimalOwner> owners, ChangeSetAggregator<AnimalOwner, Guid> ownerResults, ChangeSetAggregator<Animal> animalResults)
    {
        var expectedOwners = owners.ToList();
        var expectedOwnerIds = new HashSet<Guid>(expectedOwners.Select(owner => owner.Id));
        var actualOwnerIds = new HashSet<Guid>(ownerResults.Data.Items.Select(owner => owner.Id));

        await Assert.That(actualOwnerIds.SetEquals(expectedOwnerIds)).IsTrue().Because("owner result ids should match the source owner ids");

        var expectedAnimalCountsById = expectedOwners
            .SelectMany(owner => owner.Animals.Items)
            .GroupBy(animal => animal.Id)
            .ToDictionary(group => group.Key, group => group.Count());

        var actualAnimalCountsById = animalResults.Data.Items
            .GroupBy(animal => animal.Id)
            .ToDictionary(group => group.Key, group => group.Count());

        await Assert.That(actualAnimalCountsById.Count).IsEqualTo(expectedAnimalCountsById.Count).Because("animal result ids should match the source animal ids");
        await Assert.That(actualAnimalCountsById.All(pair => expectedAnimalCountsById.TryGetValue(pair.Key, out var expectedCount) && expectedCount == pair.Value))
            .IsTrue()
            .Because("animal result id multiplicities should match the source animal id multiplicities");
    }
}
