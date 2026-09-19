using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.List;

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

    private readonly ISourceList<AnimalOwner> _animalOwners = new SourceList<AnimalOwner>();
    private readonly ChangeSetAggregator<AnimalOwner> _animalOwnerResults;
    private readonly ChangeSetAggregator<Animal> _animalResults;
    private readonly Faker<AnimalOwner> _animalOwnerFaker;
    private readonly Faker<Animal> _animalFaker;
    private readonly Randomizer _randomizer;

    public MergeManyChangeSetsListFixture()
    {
        _randomizer = new Randomizer(0x12291977);
        _animalFaker = Fakers.Animal.Clone().WithSeed(_randomizer);
        _animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(_randomizer).WithInitialAnimals(_animalFaker);
        _animalOwners.AddRange(_animalOwnerFaker.Generate(InitialOwnerCount));

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

        var mergeAnimals = _animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect());

        var addingAnimals = true;

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

        // Verify the results
        await CheckResultContents();
    }

    [Test]
    public async Task NullChecks()
    {
        // Arrange
        var emptyChangeSetObs = Observable.Empty<IChangeSet<int>>();
        var nullChangeSetObs = (IObservable<IChangeSet<int>>)null!;
        var emptySelector = new Func<int, IObservable<IChangeSet<string>>>(i => Observable.Empty<IChangeSet<string>>());
        var nullSelector = (Func<int, IObservable<IChangeSet<string>>>)null!;

        // Act
        var checkParam1 = () => nullChangeSetObs.MergeManyChangeSets(emptySelector);
        var checkParam2 = () => emptyChangeSetObs.MergeManyChangeSets(nullSelector);

        // Assert
        await Assert.That(emptyChangeSetObs).IsNotNull();
        await Assert.That(emptySelector).IsNotNull();
        await Assert.That(nullChangeSetObs).IsNull();
        await Assert.That(nullSelector).IsNull();

        await Assert.That(checkParam1).Throws<ArgumentNullException>();
        await Assert.That(checkParam2).Throws<ArgumentNullException>();
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
    public async Task ResultContainsChildrenFromParentsAddedWithAddRange()
    {
        // Arrange
        var addThese = _animalOwnerFaker.Generate(AddRangeSize);

        // Act
        _animalOwners.AddRange(addThese);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount + AddRangeSize);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for additional add
        await Assert.That(addThese.SelectMany(added => added.Animals.Items).All(added => _animalResults.Data.Items.Contains(added))).IsTrue();
        await CheckResultContents();
    }

    [Test]
    public async Task ResultContainsChildrenFromParentsAddedWithAdd()
    {
        // Arrange
        var addThis = _animalOwnerFaker.Generate();

        // Act
        _animalOwners.Add(addThis);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount + 1);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for additional add
        await Assert.That(addThis.Animals.Items.All(added => _animalResults.Data.Items.Contains(added))).IsTrue();
        await CheckResultContents();
    }

    [Test]
    public async Task ResultContainsChildrenFromParentsAddedWithInsert()
    {
        // Arrange
        var insertIndex = _randomizer.Number(_animalOwners.Count);
        var insertThis = _animalOwnerFaker.Generate();

        // Act
        _animalOwners.Insert(insertIndex, insertThis);

        // Assert
        await Assert.That(_animalOwners.Items.ElementAt(insertIndex)).IsEqualTo(insertThis);
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount + 1);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for additional add
        await Assert.That(insertThis.Animals.Items.All(added => _animalResults.Data.Items.Contains(added))).IsTrue();
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(removeThis.Animals.Items.All(removed => !_animalResults.Data.Items.Contains(removed))).IsTrue();
        await CheckResultContents();
        removeThis.Dispose();
    }

    [Test]
    public async Task ResultDoesNotContainChildrenFromParentsRemovedWithRemoveAt()
    {
        // Arrange
        var removeIndex = _randomizer.Number(_animalOwners.Count - 1);
        var removeThis = _animalOwners.Items.ElementAt(removeIndex);

        // Act
        _animalOwners.RemoveAt(removeIndex);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount - 1);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(removeThis.Animals.Items.All(removed => !_animalResults.Data.Items.Contains(removed))).IsTrue();
        await CheckResultContents();
        removeThis.Dispose();
    }

    [Test]
    public async Task ResultDoesNotContainChildrenFromParentsRemovedWithRemoveRange()
    {
        // Arrange
        var removeIndex = _randomizer.Number(_animalOwners.Count - RemoveRangeSize - 1);
        var removeThese = _animalOwners.Items.Skip(removeIndex).Take(RemoveRangeSize);

        // Act
        _animalOwners.RemoveRange(removeIndex, RemoveRangeSize);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount - RemoveRangeSize);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(removeThese.SelectMany(owner => owner.Animals.Items).All(removed => !_animalResults.Data.Items.Contains(removed))).IsTrue();
        await CheckResultContents();
        removeThese.ForEach(owner => owner.Dispose());
    }

    [Test]
    public async Task ResultDoesNotContainChildrenFromParentsRemovedWithRemoveMany()
    {
        // Arrange
        var removeThese = _randomizer.ListItems(_animalOwners.Items.ToList(), RemoveRangeSize);

        // Act
        _animalOwners.RemoveMany(removeThese);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount - RemoveRangeSize);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(removeThese.SelectMany(owner => owner.Animals.Items).All(removed => !_animalResults.Data.Items.Contains(removed))).IsTrue();
        await CheckResultContents();
        removeThese.ForEach(owner => owner.Dispose());
    }

    [Test]
    public async Task ResultContainsCorrectItemsAfterParentReplacement()
    {
        // Arrange
        var replaceThis = _randomizer.ListItem(_animalOwners.Items.ToList());
        var withThis = _animalOwnerFaker.Generate();

        // Act
        _animalOwners.Replace(replaceThis, withThis);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount); // Owner Count should not change
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(replaceThis.Animals.Items.All(removed => !_animalResults.Data.Items.Contains(removed))).IsTrue();
        await Assert.That(withThis.Animals.Items.All(added => _animalResults.Data.Items.Contains(added))).IsTrue();
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await CheckResultContents();
        items.ForEach(owner => owner.Dispose());
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithAddRange()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var addThese = _animalFaker.Generate(AddRangeSize);
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.AddRange(addThese);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for additional add
        await Assert.That(addThese.All(animal => _animalResults.Data.Items.Contains(animal))).IsTrue();
        await Assert.That(_animalOwners.Items.Sum(owner => owner.Animals.Count)).IsEqualTo(initialCount + AddRangeSize);
        await CheckResultContents();
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithAdd()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var addThis = _animalFaker.Generate();
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Add(addThis);

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(_animalResults.Data.Items).Contains(addThis);
        await Assert.That(_animalOwners.Items.Sum(owner => owner.Animals.Count)).IsEqualTo(initialCount + 1);
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for additional add
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(removeThese.All(removed => !randomOwner.Animals.Items.Contains(removed))).IsTrue();
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(removeThese.All(removed => !randomOwner.Animals.Items.Contains(removed))).IsTrue();
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for update
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
        await Assert.That(_animalResults.Messages.Count).IsEqualTo(2); // 1 for initial add, 1 for removing
        await Assert.That(randomOwner.Animals.Count).IsEqualTo(0);
        await Assert.That(removedAnimals.All(removed => !_animalResults.Data.Items.Contains(removed))).IsTrue();
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
        var throwObservable = Observable.Throw<IChangeSet<AnimalOwner>>(expectedError);
        using var results = _animalOwners.Connect().Concat(throwObservable).MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

        // Act
        _animalOwners.Dispose();

        // Assert
        await Assert.That(results.Exception).IsEqualTo(expectedError);
    }

    private Task CheckResultContents() => CheckResultContents(_animalOwners.Items, _animalOwnerResults, _animalResults);

    private static async Task CheckResultContents(IEnumerable<AnimalOwner> owners, ChangeSetAggregator<AnimalOwner> ownerResults, ChangeSetAggregator<Animal> animalResults)
    {
        var expectedOwners = owners.ToList();

        // These should be subsets of each other
        await Assert.That(expectedOwners.Except(ownerResults.Data.Items).Any()).IsFalse();
        await Assert.That(ownerResults.Data.Items.Count).IsEqualTo(expectedOwners.Count);

        // All owner animals should be in the results
        foreach (var owner in owners)
        {
            await Assert.That(owner.Animals.Items.Except(animalResults.Data.Items).Any()).IsFalse();
        }

        // Results should not have more than the total number of animals
        await Assert.That(animalResults.Data.Count).IsEqualTo(owners.Sum(owner => owner.Animals.Count));
    }

    public void Dispose()
    {
        var owners = _animalOwners.Items.ToArray();
        _animalOwnerResults.Dispose();
        _animalResults.Dispose();
        owners.ForEach(owner => owner.Dispose());
        _animalOwners.Dispose();
    }
}
