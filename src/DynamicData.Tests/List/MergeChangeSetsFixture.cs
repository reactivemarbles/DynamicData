using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;
using System.Collections.Concurrent;

namespace DynamicData.Tests.List;

public sealed class MergeChangeSetsFixture : IDisposable
{
#if DEBUG
    const int InitialOwnerCount = 7;
    const int AddRangeSize = 5;
#else
    const int InitialOwnerCount = 103;
    const int AddRangeSize = 53;
#endif

    private readonly IList<AnimalOwner> _animalOwners = new List<AnimalOwner>();
    private readonly Faker<AnimalOwner> _animalOwnerFaker;
    private readonly Faker<Animal> _animalFaker;
    private readonly Randomizer _randomizer;

    public MergeChangeSetsFixture()
    {
        _randomizer = new Randomizer(0x10131948);
        _animalFaker = Fakers.Animal.Clone().WithSeed(_randomizer);
        _animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(_randomizer).WithInitialAnimals(_animalFaker, AddRangeSize, AddRangeSize);
        _animalOwners.Add(_animalOwnerFaker.Generate(InitialOwnerCount));
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

        IObservable<IObservable<IChangeSet<Animal>>> CreateStressObservable(int ownerCount, int animalCount, int parallel, ConcurrentBag<AnimalOwner> added, IScheduler scheduler) =>
            Observable.Create<IObservable<IChangeSet<Animal>>>(observer =>
            {
                var shared = _animalOwnerFaker.IntervalGenerate(_randomizer, MaxAddTime, scheduler)
                    .Parallelize(ownerCount, parallel)
                    .Merge(_animalOwners.ToObservable())
                    .Do(owner => added.Add(owner))
                    .Publish();

                var addAnimalsSub = shared.SelectMany(owner => AddRemoveAnimals(owner, animalCount, parallel, scheduler))
                    .Subscribe(
                        onNext: static _ => { },
                        onError: observer.OnError,
                        onCompleted: observer.OnCompleted);

                var changeSetSub = shared.Select(owner => owner.Animals.Connect())
                    .Subscribe(
                        onNext: observer.OnNext,
                        onError: observer.OnError);

                return new CompositeDisposable(addAnimalsSub, changeSetSub, shared.Connect());
            });

        IObservable<Animal> AddRemoveAnimals(AnimalOwner owner, int animalCount, int parallel, IScheduler scheduler) =>
            _animalFaker.IntervalGenerate(_randomizer, MaxAddTime, scheduler)
                .Parallelize(animalCount, parallel, obs => obs.StressAddRemove(owner.Animals, _ => GetRemoveTime(), scheduler))
                .Finally(owner.Animals.Dispose);

        var addedOwners = new ConcurrentBag<AnimalOwner>();
        var addingAnimals = true;
        var observableObservable = CreateStressObservable(ownerCount, animalCount, Environment.ProcessorCount, addedOwners, TaskPoolScheduler.Default)
                .Finally(() => addingAnimals = false)
                .Publish()
                .RefCount();
        var mergedObservable = observableObservable.MergeChangeSets();

        // Start asynchrononously modifying the parent list and the child lists
        using var results = mergedObservable.AsAggregator();

        // Subscribe / unsubscribe over and over while the collections are being modified
        do
        {
            // Ensure items are being added asynchronously before subscribing to the animal changes
            await Task.Yield();

            {
                // Subscribe
                var mergedSub = mergedObservable.Subscribe();

                // Let other threads run
                await Task.Yield();

                // Unsubscribe
                mergedSub.Dispose();
            }
        }
        while (addingAnimals);

        // Verify the results
        await CheckResultContents(addedOwners.ToList(), results);
    }

    [Test]
    public async Task NullChecks()
    {
        // Arrange
        var nullChangeSetObs = (IObservable<IObservable<IChangeSet<int>>>)null!;

        // Act
        var checkParam1 = () => nullChangeSetObs.MergeChangeSets();

        // Assert
        await Assert.That(nullChangeSetObs).IsNull();

        await Assert.That(checkParam1).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ResultContainsAllInitialChildrenObsObs()
    {
        // Arrange
        var obs = GetObservableObservable();

        // Act
        using var results = obs.MergeChangeSets().AsAggregator();

        // Assert
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultContainsAllInitialChildrenEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();

        // Act
        using var results = obs.MergeChangeSets().AsAggregator();

        // Assert
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultEmptyIfSourceIsClearedObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        _animalOwners.ForEach(owner => owner.Animals.Clear());

        // Assert
        await Assert.That(results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ResultEmptyIfSourceIsClearedEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        _animalOwners.ForEach(owner => owner.Animals.Clear());

        // Assert
        await Assert.That(results.Data.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithAddRangeObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = (await ForOwnersAsync(UseAddRange)).SelectMany(list => list).ToList();

        // Assert
        await Assert.That(added.Except(results.Data.Items).Any()).IsFalse();
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithAddRangeEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = (await ForOwnersAsync(UseAddRange)).SelectMany(list => list).ToList();

        // Assert
        await Assert.That(added.Except(results.Data.Items).Any()).IsFalse();
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithAddObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = await ForOwnersAsync(UseAdd);

        // Assert
        await Assert.That(added.Except(results.Data.Items).Any()).IsFalse();
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithAddEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        await ForOwnersAsync(owner => owner.Animals.Add(_animalFaker.Generate()));

        // Assert
        await CheckResultContents(_animalOwners, results);
    }
    [Test]
    public async Task ResultContainsChildrenAddedWithInsertObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = await ForOwnersAsync(UseInsert);

        // Assert
        await Assert.That(added.Except(results.Data.Items).Any()).IsFalse();
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultContainsChildrenAddedWithInsertEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = await ForOwnersAsync(UseInsert);

        // Assert
        await Assert.That(added.Except(results.Data.Items).Any()).IsFalse();
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultContainsCorrectItemsAfterChildReplacementObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var replacements = await ForOwnersAsync(ReplaceAnimal);

        // Assert
        await Assert.That(replacements.Select(r => r.New).Except(results.Data.Items).Any()).IsFalse();
        await Assert.That(replacements.Select(r => r.Old).All(old => !results.Data.Items.Contains(old))).IsTrue();
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultContainsCorrectItemsAfterChildReplacementEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var replacements = await ForOwnersAsync(ReplaceAnimal);

        // Assert
        await Assert.That(replacements.Select(r => r.New).Except(results.Data.Items).Any()).IsFalse();
        await Assert.That(replacements.Select(r => r.Old).All(old => !results.Data.Items.Contains(old))).IsTrue();
        await CheckResultContents(_animalOwners, results);
    }

    [Test]
    public async Task ResultFailsIfSourceFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IObservable<IChangeSet<Animal>>>(expectedError);
        var obs = GetObservableObservable();

        // Act
        using var results = obs.Concat(throwObservable).MergeChangeSets().AsAggregator();

        // Assert
        await Assert.That(results.Exception).IsEqualTo(expectedError);
    }

    [Test]
    public async Task ResultFailsIfAnyChildChangeSetFails()
    {
        // Arrange
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<Animal>>(expectedError);
        var obs = GetEnumerableObservable().Append(throwObservable);

        // Act
        using var results = obs.MergeChangeSets().AsAggregator();

        // Assert
        await Assert.That(results.Exception).IsEqualTo(expectedError);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ResultCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeAll)
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        _animalOwners.Skip(completeAll ? 0 : 1).ForEach(owner => owner.Animals.Dispose());

        // Assert
        await Assert.That(results.IsCompleted).IsEqualTo(completeAll);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task MergedObservableRespectsCompletableFlag(bool completeSource, bool completeChildren)
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets(completable: completeSource).AsAggregator();

        // Act
        _animalOwners.Skip(completeChildren ? 0 : 1).ForEach(owner => owner.Animals.Dispose());

        // Assert
        await Assert.That(results.IsCompleted).IsEqualTo(completeSource && completeChildren);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EnumObservableUsesTheScheduler(bool advance)
    {
        // Arrange
        var scheduler = new TestScheduler();
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets(scheduler: scheduler).AsAggregator();

        // Act
        if (advance)
        {
            scheduler.AdvanceBy(InitialOwnerCount);
        }

        // Assert
        if (advance)
        {
            await CheckResultContents(_animalOwners, results);
        }
        else
        {
            await Assert.That(results.Data.Count).IsEqualTo(0);
            await Assert.That(results.Messages.Count).IsEqualTo(0);
        }
    }

    public void Dispose()
    {
        _animalOwners.ForEach(owner => owner.Dispose());
    }

    private static async Task CheckResultContents(IList<AnimalOwner> expectedOwners, ChangeSetAggregator<Animal> animalResults)
    {
        var expectedAnimals = expectedOwners.SelectMany(owner => owner.Animals.Items).ToList();

        // These should be subsets of each other, so check one subset and the size
        await Assert.That(expectedAnimals.Except(animalResults.Data.Items).Any()).IsFalse();
        await Assert.That(animalResults.Data.Items.Count).IsEqualTo(expectedAnimals.Count);
    }

    Task ForOwnersAsync(Action<AnimalOwner> action) => Task.WhenAll(_animalOwners.Select(owner => Task.Run(() => action(owner))));

    Task<T[]> ForOwnersAsync<T>(Func<AnimalOwner, T> func) => Task.WhenAll(_animalOwners.Select(owner => Task.Run(() => func(owner))));

    private Animal UseAdd(AnimalOwner owner) =>
        _animalFaker.Generate().With(animal => owner.Animals.Add(animal));

    private List<Animal> UseAddRange(AnimalOwner owner) =>
        _animalFaker.Generate(_randomizer.Number(AddRangeSize)).With(animals => owner.Animals.AddRange(animals));

    private (Animal Old, Animal New) ReplaceAnimal(AnimalOwner owner)
    {
        var replaceThis = _randomizer.ListItem(owner.Animals.Items.ToList());
        var withThis = _animalFaker.Generate();
        owner.Animals.Replace(replaceThis, withThis);
        return (replaceThis, withThis);
    }

    private Animal UseInsert(AnimalOwner owner)
    {
        var newAnimal = _animalFaker.Generate();
        owner.Animals.Insert(_randomizer.Number(owner.Animals.Count), newAnimal);
        return newAnimal;
    }

    private IEnumerable<IObservable<IChangeSet<Animal>>> GetEnumerableObservable() => _animalOwners.Select(owner => owner.Animals.Connect());
    private IObservable<IObservable<IChangeSet<Animal>>> GetObservableObservable() => GetEnumerableObservable().ToObservable();
}
