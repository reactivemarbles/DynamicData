using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;

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
    private readonly Randomizer _randomizer;

    public MergeManyChangeSetsListFixture()
    {
        Randomizer.Seed = new Random(0x01221948);
        _randomizer = new Randomizer();
        _animalOwners.AddOrUpdate(Fakers.AnimalOwner.Generate(InitialOwnerCount));

        _animalOwnerResults = _animalOwners.Connect().AsAggregator();
        _animalResults = _animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();
    }

    [Fact]
    public void NullChecks()
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
        emptyChangeSetObs.Should().NotBeNull();
        emptyKeySelector.Should().NotBeNull();
        emptySelector.Should().NotBeNull();
        nullChangeSetObs.Should().BeNull();
        nullKeySelector.Should().BeNull();
        nullSelector.Should().BeNull();

        checkParam1.Should().Throw<ArgumentNullException>();
        checkParam2.Should().Throw<ArgumentNullException>();
        checkParam3.Should().Throw<ArgumentNullException>();
        checkParam4.Should().Throw<ArgumentNullException>();
    }

#if false
    class StressTest(int count, IObserver<IChangeSet<Animal>> obs)
    {
        private int _added;
        private int _removed;
        private readonly int _count = count;
        private readonly IObserver<IChangeSet<Animal>> _observer = obs;

        public IDisposable Start(int simultanenous, IScheduler sch) =>
            new CompositeDisposable(Enumerable.Range(0, simultanenous).Select(_ => ScheduleAdd(sch)));

        private IDisposable ScheduleAdd(IScheduler sch)
        {
            var current = Volatile.Read(ref _added);
            var expected = current;

            do
            {
                if (current >= _count)
                {
                    return Disposable.Empty;
                }
                expected = current;
            }
            while ((current = Interlocked.CompareExchange(ref _added, current + 1, current)) != expected);

            return sch.Schedule(this, NextAddTime(), (sch, test) => test.Add(sch));
        }

        private IDisposable Add(IScheduler sch)
        {
            var animal = Fakers.Animal.Generate();
            _observer.OnNext(new ChangeSet<Animal>(new[] { new Change<Animal>(ListChangeReason.Add, animal) }));
            var removeDisposable = sch.Schedule(NextRemoveTime(), () => Remove(animal));

            return new CompositeDisposable(removeDisposable, ScheduleAdd(sch));
        }

        private void Remove(Animal animal)
        {
            _observer.OnNext(new ChangeSet<Animal>(new[] { new Change<Animal>(ListChangeReason.Remove, animal) }));
            if (Interlocked.Increment(ref _removed) == _count)
            {
                _observer.OnCompleted();
            }
        }

        //private TimeSpan NextAddTime() => TimeSpan.FromSeconds(_rand.Double() * MaxAddSeconds);
        //private TimeSpan NextRemoveTime() => TimeSpan.FromSeconds(_rand.Double(MinRemoveSeconds, MaxRemoveSeconds));

        static private TimeSpan NextAddTime() => TimeSpan.Zero;
        static private TimeSpan NextRemoveTime() => TimeSpan.Zero;
    }

    [Theory]
    [InlineData(100, 1_000)]
    [InlineData(1_000, 100)]
    [InlineData(10_000, 10)]
    public void MultiThreadedStressTest(int ownerCount, int animalCount)
    {
        Action test = () =>
        {
            IObservable<AnimalOwner> GenerateOwners() => Observable.Interval(TimeSpan.FromMilliseconds(1), DefaultScheduler.Instance).Select(_ => Fakers.AnimalOwner.Generate()).Take(ownerCount);
            IObservable<Animal> GenerateAnimals() => Observable.Interval(TimeSpan.FromMilliseconds(1), DefaultScheduler.Instance).Select(_ => Fakers.Animal.Generate()).Take(animalCount);
            IDisposable AddMoreAnimals(AnimalOwner owner) => GenerateAnimals().Do(animal => owner.Animals.Add(animal)).Subscribe();
            bool done = false;

            // Arrange
            var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);
            var merged = _animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect());
            var populateOwners = GenerateOwners().Do(owner => _animalOwners.AddOrUpdate(owner), () => done = true);
            var populateAnimals = _animalOwners.Connect().SubscribeMany(AddMoreAnimals);

            // Act
            using var subOwners = populateOwners.Subscribe();
            using var subAnimals = populateAnimals.Subscribe();
            using var subMerged = merged.Subscribe();

            while (!done)
            {
                Thread.Sleep(100);
            }
        };

        test.Should().NotThrow();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 10)]
    [InlineData(5, 7)]
    [InlineData(10, 1_000)]
    [InlineData(100, 1_000)]
    [InlineData(1_000, 10)]
    public void MultiThreadedManualStressTest(int ownerCount, int animalCount)
    {
        IScheduler testingScheduler = TaskPoolScheduler.Default;

        IObservable<IChangeSet<Animal>> AddMoreAnimals(AnimalOwner owner, int count, IScheduler scheduler) =>
            Observable.Create<IChangeSet<Animal>>(observer =>
            {
                var test = new StressTest(count, observer);

                return new CompositeDisposable(owner.Animals.Connect().SubscribeSafe(observer), test.Start(1, scheduler));
            });

        Action test = () =>
        {
            IObservable<AnimalOwner> GenerateOwners() => Observable.Interval(TimeSpan.FromMilliseconds(1), testingScheduler).Delay().Select(_ => Fakers.AnimalOwner.Generate()).Take(ownerCount);

            var merged = _animalOwners
                    .Connect()
                    .MergeManyChangeSets(owner => AddMoreAnimals(owner, animalCount, testingScheduler));
            var populateOwners = GenerateOwners().Do(owner => _animalOwners.AddOrUpdate(owner), () => _animalOwners.Dispose());

            // Act
            using var subOwners = populateOwners.Subscribe();
            using var mergedResults = merged.AsAggregator();

            while (!mergedResults.IsCompleted)
            {
                Thread.Sleep(100);
            }

            // Arrange
            mergedResults.Data.Count.Should().Be(_animalOwners.Items.Sum(owner => owner.Animals.Count));
        };

        test.Should().NotThrow();
    }
#endif

    [Fact]
    public void ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenFromAddedParents()
    {
        // Arrange
        var addThis = Fakers.AnimalOwner.Generate();

        // Act
        _animalOwners.AddOrUpdate(addThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + 1);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        addThis.Animals.Items.ForEach(added => _animalResults.Data.Items.Should().Contain(added));
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
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        removeThis.Animals.Items.ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThis.Dispose();
    }

    [Fact]
    public void ResultDoesNotContainChildrenFromParentsBatchRemoved()
    {
        // Arrange
        var removeThese = _randomizer.ListItems(_animalOwners.Items.ToList(), RemoveRangeSize);

        // Act
        _animalOwners.Remove(removeThese);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - RemoveRangeSize);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + RemoveRangeSize);
        removeThese.SelectMany(owner => owner.Animals.Items).ForEach(removed => _animalResults.Data.Items.Should().NotContain(removed));
        CheckResultContents();
        removeThese.ForEach(owner => owner.Dispose());
    }

    [Fact]
    public void ResultContainsCorrectItemsAfterParentUpdate()
    {
        // Arrange
        var replaceThis = _randomizer.ListItem(_animalOwners.Items.ToList());
        var withThis = CreateWithSameId(replaceThis);

        // Act
        _animalOwners.AddOrUpdate(withThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount); // Owner Count should not change
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 2); // +2 = 1 Message removing animals from old value, +1 message adding from new value
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
        CheckResultContents();
        items.ForEach(owner => owner.Dispose());
    }

    [Fact]
    public void ResultContainsChildrenAddedWithAddRange()
    {
        // Arrange
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);
        List<Animal> totalAdded = new List<Animal>();

        // Act
        _animalOwners.Items.ForEach(owner => owner.Animals.AddRange(Fakers.Animal.Generate(AddRangeSize).With(added => totalAdded.AddRange(added))));

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount * 2);
        totalAdded.ForEach(animal => _animalResults.Data.Items.Should().Contain(animal));
        _animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + totalAdded.Count);
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsChildrenAddedWithInsert()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var insertIndex = _randomizer.Number(randomOwner.Animals.Items.Count());
        var insertThis = Fakers.Animal.Generate();
        var initialCount = _animalOwners.Items.Sum(owner => owner.Animals.Count);

        // Act
        randomOwner.Animals.Insert(insertIndex, insertThis);

        // Assert
        randomOwner.Animals.Items.ElementAt(insertIndex).Should().Be(insertThis);
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
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
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
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
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
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
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
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
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
        removeThese.ForEach(removed => randomOwner.Animals.Items.Should().NotContain(removed));
        CheckResultContents();
    }

    [Fact]
    public void ResultContainsCorrectItemsAfterChildReplacement()
    {
        // Arrange
        var randomOwner = _randomizer.ListItem(_animalOwners.Items.ToList());
        var replaceThis = _randomizer.ListItem(randomOwner.Animals.Items.ToList());
        var withThis = Fakers.Animal.Generate();

        // Act
        randomOwner.Animals.Replace(replaceThis, withThis);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
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
        _animalResults.Messages.Count.Should().Be(InitialOwnerCount + 1);
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
        var throwObservable = Observable.Throw<IChangeSet<AnimalOwner, Guid>>(expectedError);
        using var results = _animalOwners.Connect().Concat(throwObservable).MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

        // Act
        _animalOwners.Dispose();

        // Assert
        results.Exception.Should().Be(expectedError);
    }

    private static AnimalOwner CreateWithSameId(AnimalOwner original)
    {
        var newOwner = Fakers.AnimalOwner.Generate();
        var sameId = new AnimalOwner(newOwner.Name, original.Id);
        sameId.Animals.AddRange(newOwner.Animals.Items);
        return sameId;
    }

    private void CheckResultContents()
    {
        var expectedOwners = _animalOwners.Items.ToList();
        var expectedAnimals = expectedOwners.SelectMany(owner => owner.Animals.Items).ToList();

        // These should be subsets of each other, so check one subset and the size
        expectedOwners.Should().BeSubsetOf(_animalOwnerResults.Data.Items);
        _animalOwnerResults.Data.Items.Count().Should().Be(expectedOwners.Count);

        // These should be subsets of each other, so check one subset and the size
        expectedAnimals.Should().BeSubsetOf(_animalResults.Data.Items);
        _animalResults.Data.Items.Count().Should().Be(expectedAnimals.Count);
    }

    public void Dispose()
    {
        _animalOwners.Items.ForEach(owner => owner.Dispose());
        _animalOwnerResults.Dispose();
        _animalResults.Dispose();
        _animalOwners.Dispose();
    }
}
