using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;
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

    [Theory]
    [InlineData(5, 7)]
    [InlineData(10, 50)]
#if !DEBUG
    [InlineData(10, 1_000)]
    [InlineData(200, 500)]
    [InlineData(1_000, 10)]
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
        CheckResultContents(addedOwners.ToList(), results);
    }

    [Fact]
    public void NullChecks()
    {
        // Arrange
        var nullChangeSetObs = (IObservable<IObservable<IChangeSet<int>>>)null!;

        // Act
        var checkParam1 = () => nullChangeSetObs.MergeChangeSets();

        // Assert
        nullChangeSetObs.Should().BeNull();

        checkParam1.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResultContainsAllInitialChildrenObsObs()
    {
        // Arrange
        var obs = GetObservableObservable();

        // Act
        using var results = obs.MergeChangeSets().AsAggregator();

        // Assert
        CheckResultContents(_animalOwners, results);
    }

    [Fact]
    public void ResultContainsAllInitialChildrenEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();

        // Act
        using var results = obs.MergeChangeSets().AsAggregator();

        // Assert
        CheckResultContents(_animalOwners, results);
    }

    [Fact]
    public void ResultEmptyIfSourceIsClearedObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        _animalOwners.ForEach(owner => owner.Animals.Clear());

        // Assert
        results.Data.Count.Should().Be(0);
    }

    [Fact]
    public void ResultEmptyIfSourceIsClearedEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        _animalOwners.ForEach(owner => owner.Animals.Clear());

        // Assert
        results.Data.Count.Should().Be(0);
    }

    [Fact]
    public async Task ResultContainsChildrenAddedWithAddRangeObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = (await ForOwnersAsync(UseAddRange)).SelectMany(list => list).ToList();

        // Assert
        added.Should().BeSubsetOf(results.Data.Items);
        CheckResultContents(_animalOwners, results);
    }

    [Fact]
    public async Task ResultContainsChildrenAddedWithAddRangeEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = (await ForOwnersAsync(UseAddRange)).SelectMany(list => list).ToList();

        // Assert
        added.Should().BeSubsetOf(results.Data.Items);
        CheckResultContents(_animalOwners, results);
    }

    [Fact]
    public async Task ResultContainsChildrenAddedWithAddObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = await ForOwnersAsync(UseAdd);

        // Assert
        added.Should().BeSubsetOf(results.Data.Items);
        CheckResultContents(_animalOwners, results);
    }

    [Fact]
    public async Task ResultContainsChildrenAddedWithAddEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        await ForOwnersAsync(owner => owner.Animals.Add(_animalFaker.Generate()));

        // Assert
        CheckResultContents(_animalOwners, results);
    }
    [Fact]
    public async Task ResultContainsChildrenAddedWithInsertObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = await ForOwnersAsync(UseInsert);

        // Assert
        added.Should().BeSubsetOf(results.Data.Items);
        CheckResultContents(_animalOwners, results);
    }

    [Fact]
    public async Task ResultContainsChildrenAddedWithInsertEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var added = await ForOwnersAsync(UseInsert);

        // Assert
        added.Should().BeSubsetOf(results.Data.Items);
        CheckResultContents(_animalOwners, results);
    }


    [Fact]
    public async Task ResultContainsCorrectItemsAfterChildReplacementObs()
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var replacements = await ForOwnersAsync(ReplaceAnimal);

        // Assert
        replacements.Select(r => r.New).Should().BeSubsetOf(results.Data.Items);
        replacements.Select(r => r.Old).ForEach(old => results.Data.Items.Should().NotContain(old));
        CheckResultContents(_animalOwners, results);
    }

    [Fact]
    public async Task ResultContainsCorrectItemsAfterChildReplacementEnum()
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        var replacements = await ForOwnersAsync(ReplaceAnimal);

        // Assert
        replacements.Select(r => r.New).Should().BeSubsetOf(results.Data.Items);
        replacements.Select(r => r.Old).ForEach(old => results.Data.Items.Should().NotContain(old));
        CheckResultContents(_animalOwners, results);
    }

    [Fact]
    public void ResultFailsIfSourceFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IObservable<IChangeSet<Animal>>>(expectedError);
        var obs = GetObservableObservable();

        // Act
        using var results = obs.Concat(throwObservable).MergeChangeSets().AsAggregator();

        // Assert
        results.Exception.Should().Be(expectedError);
    }

    [Fact]
    public void ResultFailsIfAnyChildChangeSetFails()
    {
        // Arrange
        var expectedError = new Exception("Test exception");
        var throwObservable = Observable.Throw<IChangeSet<Animal>>(expectedError);
        var obs = GetEnumerableObservable().Append(throwObservable);

        // Act
        using var results = obs.MergeChangeSets().AsAggregator();

        // Assert
        results.Exception.Should().Be(expectedError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResultCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeAll)
    {
        // Arrange
        var obs = GetObservableObservable();
        using var results = obs.MergeChangeSets().AsAggregator();

        // Act
        _animalOwners.Skip(completeAll ? 0 : 1).ForEach(owner => owner.Animals.Dispose());

        // Assert
        results.IsCompleted.Should().Be(completeAll);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MergedObservableRespectsCompletableFlag(bool completeSource, bool completeChildren)
    {
        // Arrange
        var obs = GetEnumerableObservable();
        using var results = obs.MergeChangeSets(completable: completeSource).AsAggregator();

        // Act
        _animalOwners.Skip(completeChildren ? 0 : 1).ForEach(owner => owner.Animals.Dispose());

        // Assert
        results.IsCompleted.Should().Be(completeSource && completeChildren);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EnumObservableUsesTheScheduler(bool advance)
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
            CheckResultContents(_animalOwners, results);
        }
        else
        {
            results.Data.Count.Should().Be(0);
            results.Messages.Count.Should().Be(0);
        }
    }

    public void Dispose()
    {
        _animalOwners.ForEach(owner => owner.Dispose());
    }

    private static void CheckResultContents(IList<AnimalOwner> expectedOwners, ChangeSetAggregator<Animal> animalResults)
    {
        var expectedAnimals = expectedOwners.SelectMany(owner => owner.Animals.Items).ToList();

        // These should be subsets of each other, so check one subset and the size
        expectedAnimals.Should().BeSubsetOf(animalResults.Data.Items);
        animalResults.Data.Items.Count.Should().Be(expectedAnimals.Count);
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
