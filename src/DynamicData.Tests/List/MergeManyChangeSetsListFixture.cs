﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;

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
        CheckResultContents();
    }

    [Fact]
    public void NullChecks()
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
        emptyChangeSetObs.Should().NotBeNull();
        emptySelector.Should().NotBeNull();
        nullChangeSetObs.Should().BeNull();
        nullSelector.Should().BeNull();

        checkParam1.Should().Throw<ArgumentNullException>();
        checkParam2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(1);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenFromParentsAddedWithAddRange()
    {
        // Arrange
        var addThese = _animalOwnerFaker.Generate(AddRangeSize);

        // Act
        _animalOwners.AddRange(addThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + AddRangeSize);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for additional add
        addThese.SelectMany(added => added.Animals.Items).ForEach(added => _animalResults.Data.Items.Should().Contain(added));
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenFromParentsAddedWithAdd()
    {
        // Arrange
        var addThis = _animalOwnerFaker.Generate();

        // Act
        _animalOwners.Add(addThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for additional add
        addThis.Animals.Items.ForEach(added => _animalResults.Data.Items.Should().Contain(added));
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenFromParentsAddedWithInsert()
    {
        // Arrange
        var insertIndex = _randomizer.Number(_animalOwners.Count);
        var insertThis = _animalOwnerFaker.Generate();

        // Act
        _animalOwners.Insert(insertIndex, insertThis);

        // Assert
        _animalOwners.Items.ElementAt(insertIndex).Should().Be(insertThis);
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for additional add
        insertThis.Animals.Items.ForEach(added => _animalResults.Data.Items.Should().Contain(added));
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsRemovedWithRemove()
    {
        // Arrange
        var removeThis = _randomizer.ListItem(_animalOwners.Items.ToList());

        // Act
        _animalOwners.Remove(removeThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - 1);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        removeThis.Animals.Items.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThis.Dispose();
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsRemovedWithRemoveAt()
    {
        // Arrange
        var removeIndex = _randomizer.Number(_animalOwners.Count - 1);
        var removeThis = _animalOwners.Items.ElementAt(removeIndex);

        // Act
        _animalOwners.RemoveAt(removeIndex);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - 1);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        removeThis.Animals.Items.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThis.Dispose();
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsRemovedWithRemoveRange()
    {
        // Arrange
        var removeIndex = _randomizer.Number(_animalOwners.Count - RemoveRangeSize - 1);
        var removeThese = _animalOwners.Items.Skip(removeIndex).Take(RemoveRangeSize);

        // Act
        _animalOwners.RemoveRange(removeIndex, RemoveRangeSize);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - RemoveRangeSize);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        removeThese.SelectMany(owner => owner.Animals.Items).ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThese.ForEach(owner => owner.Dispose());
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsRemovedWithRemoveMany()
    {
        // Arrange
        var removeThese = _randomizer.ListItems(_animalOwners.Items.ToList(), RemoveRangeSize);

        // Act
        _animalOwners.RemoveMany(removeThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - RemoveRangeSize);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        removeThese.SelectMany(owner => owner.Animals.Items).ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThese.ForEach(owner => owner.Dispose());
    }

    [Fact]
    public void ResultContainsCorrectItemsAfterParentReplacement()
    {
        // Arrange
        var replaceThis = _randomizer.ListItem(_animalOwners.Items.ToList());
        var withThis = _animalOwnerFaker.Generate();

        // Act
        _animalOwners.Replace(replaceThis, withThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount); // Owner Count should not change
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        replaceThis.Animals.Items.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        withThis.Animals.Items.ForEach(added => _animalResults.Data.Items.Should().Contain(added));
        CheckResultContents();
        replaceThis.Dispose();
    }

    [Fact]
    public void ResultEmptyIfSourceIsCleared()
    {
        // Arrange
        var items = _animalOwners.Items.ToList();

        // Act
        _animalOwners.Clear();

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(0);
        _animalResults.Data.Count.Should().Be(0);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        CheckResultContents();
        items.ForEach(owner => owner.Dispose());
    }

    [Fact]
    public void ResultContainsChildrenAddedWithAddRange()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var addThese = _animalFaker.Generate(AddRangeSize);
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.AddRange(addThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for additional add
        addThese.ForEach(animal => _animalResults.Data.Items.Should().Contain(animal));
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + AddRangeSize);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenAddedWithAdd()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var addThis = _animalFaker.Generate();
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Add(addThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        _animalResults.Data.Items.Should().Contain(addThis);
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + 1);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenAddedWithInsert()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var insertIndex = _randomizer.Number(randomOwner.Animals.Items.Count);
        var insertThis = _animalFaker.Generate();
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Insert(insertIndex, insertThis);

        // Assert
        randomOwner.Animals.Items.ElementAt(insertIndex).Should().Be(insertThis);
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for additional add
        _animalResults.Data.Items.Should().Contain(insertThis);
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + 1);
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenRemovedWithRemove()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeThis = _randomizer.ListItem(randomOwner.Animals.Items.ToList());
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Remove(removeThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        _animalResults.Data.Items.Should().NotContain(removeThis);
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount - 1);
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenRemovedWithRemoveAt()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeIndex = _randomizer.Number(randomOwner.Animals.Count - 1);
        var removeThis = randomOwner.Animals.Items.ElementAt(removeIndex);
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.RemoveAt(removeIndex);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        _animalResults.Data.Items.Should().NotContain(removeThis);
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount - 1);
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenRemovedWithRemoveRange()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeCount = _randomizer.Number(1, randomOwner.Animals.Count - 1);
        var removeIndex = _randomizer.Number(randomOwner.Animals.Count - removeCount - 1);
        var removeThese = randomOwner.Animals.Items.Skip(removeIndex).Take(removeCount);

        // Act
        randomOwner.Animals.RemoveRange(removeIndex, removeCount);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        removeThese.ForEach(removed => randomOwner.Animals.Items.Should().NotContain(removed));
        CheckResultContents();
    }

    [Fact]
    public void ResultDoesNotContainChildrenRemovedWithRemoveMany()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removeCount = _randomizer.Number(1, randomOwner.Animals.Count - 1);
        var removeThese = _randomizer.ListItems(randomOwner.Animals.Items.ToList(), removeCount);

        // Act
        randomOwner.Animals.RemoveMany(removeThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        removeThese.ForEach(removed => randomOwner.Animals.Items.Should().NotContain(removed));
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsCorrectItemsAfterChildReplacement()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var replaceThis = _randomizer.ListItem(randomOwner.Animals.Items.ToList());
        var withThis = _animalFaker.Generate();

        // Act
        randomOwner.Animals.Replace(replaceThis, withThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for update
        randomOwner.Animals.Items.Should().NotContain(replaceThis);
        randomOwner.Animals.Items.Should().Contain(withThis);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsCorrectItemsAfterChildClear()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var removedAnimals = randomOwner.Animals.Items.ToList();

        // Act
        randomOwner.Animals.Clear();

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(2); // 1 for initial add, 1 for removing
        randomOwner.Animals.Count.Should().Be(0);
        removedAnimals.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ResultCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
    {
        // Arrange

        // Act
        _animalOwners.Items.Skip(completeChildren ? 0 : 1).ForEach(owner => owner.Dispose());
        if (completeSource)
        {
            _animalOwners.Dispose();
        }

        // Assert
        _animalOwnerResults.IsCompleted.Should().Be(completeSource);
        _animalResults.IsCompleted.Should().Be(completeSource && completeChildren);
    }

    [Fact]
    public void ResultFailsIfSourceFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<AnimalOwner>>(expectedError);
        using var results = _animalOwners.Connect().Concat(throwObservable).MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

        // Act
        _animalOwners.Dispose();

        // Assert
        results.Exception.Should().Be(expectedError);
    }

    private void CheckResultContents() => CheckResultContents(_animalOwners.Items, _animalOwnerResults, _animalResults);

    private static void CheckResultContents(IEnumerable<AnimalOwner> owners, ChangeSetAggregator<AnimalOwner> ownerResults, ChangeSetAggregator<Animal> animalResults)
    {
        var expectedOwners = owners.ToList();

        // These should be subsets of each other
        expectedOwners.Should().BeSubsetOf(ownerResults.Data.Items);
        ownerResults.Data.Items.Count.Should().Be(expectedOwners.Count);

        // All owner animals should be in the results
        foreach (var owner in owners)
        {
            owner.Animals.Items.Should().BeSubsetOf(animalResults.Data.Items);
        }

        // Results should not have more than the total number of animals
        animalResults.Data.Count.Should().Be(owners.Sum(owner => owner.Animals.Count));
    }

    public void Dispose()
    {
        _animalOwners.Items.ForEach(owner => owner.Dispose());
        _animalOwnerResults.Dispose();
        _animalResults.Dispose();
        _animalOwners.Dispose();
    }
}
