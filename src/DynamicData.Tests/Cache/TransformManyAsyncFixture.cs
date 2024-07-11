using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;

namespace DynamicData.Tests.Cache;

public sealed class TransformManyAsyncFixture : IDisposable
{
#if DEBUG
    const int InitialOwnerCount = 7;
    const int AddCount = 5;
    const int RemoveCount = 3;
#else
    const int InitialOwnerCount = 103;
    const int AddCount = 53;
    const int RemoveCount = 37;
#endif

    const int MinTaskDelay = 10;
    const int MaxTaskDelay = 100;

    private readonly ISourceCache<AnimalOwner, Guid> _animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
    private readonly ChangeSetAggregator<AnimalOwner, Guid> _animalOwnerResults;
    private readonly Faker<AnimalOwner> _animalOwnerFaker;
    private readonly Faker<Animal> _animalFaker;
    private readonly Randomizer _randomizer;

    public TransformManyAsyncFixture()
    {
        unchecked{ _randomizer = new Randomizer((int)0xf7ee_bee7); }

        _animalFaker = Fakers.Animal.Clone().WithSeed(_randomizer);
        _animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(_randomizer).WithInitialAnimals(_animalFaker);

        _animalOwnerResults = _animalOwners.Connect().AsAggregator();
    }

    [Fact]
    public void EnumerableResultContainsAllInitialChildrenInSingleChangeSet()
    {
        // Arrange
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Act
        using var animalResults = CreateEnumerableChangeSet().AsAggregator();

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        animalResults.Messages.Count.Should().Be(1);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Fact]
    public void ResultContainsAllInitialChildren()
    {
        // Arrange
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Act
        using var animalResults = CreateObservableCollectionChangeSet().AsAggregator();

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        animalResults.Messages.Count.Should().Be(1);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Fact]
    public void ResultContainsChildrenFromAddedParents()
    {
        // Arrange
        using var animalResults = CreateObservableCollectionChangeSet().AsAggregator();

        // Act
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        animalResults.Messages.Count.Should().Be(1);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Fact]
    public async Task ResultContainsChildrenFromAddedParentsAsync()
    {
        // Arrange
        var taskTracker = new TaskTracker(FakeDelay);
        var shared = CreateObservableCollectionChangeSet(taskTracker.Create).Replay();
        var animalResults = shared.AsAggregator();
        using var connect = shared.Connect();
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Act
        await shared.Take(1);

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        animalResults.Messages.Count.Should().BeGreaterThan(0);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Fact]
    public void ResultContainsAddedChildrenFromExistingParents()
    {
        // Arrange
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));
        using var animalResults = CreateObservableCollectionChangeSet().AsAggregator();

        // Act
        _animalOwners.Items.ForEach(owner => owner.AddAnimals(_animalFaker, 1, AddCount));

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Fact]
    public async Task ResultDoesNotContainChildrenFromRemovedParentsAsync()
    {
        // Arrange
        var taskTracker = new TaskTracker(FakeDelay);
        var animalResults = CreateObservableCollectionChangeSet(taskTracker.Create).AsAggregator();
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));
        var removedOwners = _randomizer.ListItems(_animalOwners.Items.ToList(), RemoveCount);
        _ = taskTracker.Add(() => _animalOwners.Remove(removedOwners));

        // Act
        await taskTracker.WhenAll();

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - RemoveCount);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Fact]
    public void ResultsWorkWithComparer()
    {
        // Arrange
        using var animalResults = CreateObservableCollectionChangeSet(FamilyKey, comparer: Animal.NameComparer).AsAggregator();

        // Act
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults, FamilyKey, Animal.NameComparer);
    }

    [Fact]
    public async Task ResultsWithObservableCacheChangesAsync()
    {
        // Arrange
        var taskTracker = new TaskTracker(FakeDelay);
        using var animalResults = CreateObservableCacheChangeSet(taskTracker.Create).AsAggregator();
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));
        var ownerAddCount = _randomizer.Number(1, AddCount);
        taskTracker.Add(() => _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate()), ownerAddCount);
        taskTracker.Add(() => _randomizer.ListItem(_animalOwners.Items.ToList()).AddAnimals(_animalFaker, 1, AddCount), AddCount);

        // Act
        await taskTracker.WhenAll();

        // Assert
        _animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + ownerAddCount);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResultCompletesOnlyWhenSourceCompletes(bool completeSource)
    {
        // Arrange
        using var animalResults = CreateObservableCollectionChangeSet().AsAggregator();
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Act
        if (completeSource)
        {
            _animalOwners.Dispose();
        }

        // Assert
        _animalOwnerResults.IsCompleted.Should().Be(completeSource);
        CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
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

    public void Dispose()
    {
        _animalOwners.Items.ForEach(owner => owner.Dispose());
        _animalOwnerResults.Dispose();
        _animalOwners.Dispose();
    }

    private AnimalOwner CreateWithSameId(AnimalOwner original)
    {
        var newOwner = _animalOwnerFaker.Generate();
        var sameId = new AnimalOwner(newOwner.Name, original.Id);
        sameId.Animals.AddRange(newOwner.Animals.Items);
        return sameId;
    }
 
    private static void CheckResultContents<T>(IEnumerable<AnimalOwner> owners, ChangeSetAggregator<AnimalOwner, Guid> ownerResults, ChangeSetAggregator<Animal, T> animalResults, Func<Animal, T> keySelector, IComparer<Animal> comparer)
        where T : notnull
    {
        var expectedOwners = owners.ToList();

        // These should be subsets of each other
        expectedOwners.Should().BeSubsetOf(ownerResults.Data.Items);
        ownerResults.Data.Items.Count.Should().Be(expectedOwners.Count);

        var allAnimals = expectedOwners.SelectMany(owner => owner.Animals.Items).ToList();
        var expectedAnimals = allAnimals.GroupBy(keySelector).Select(group => group.OrderBy(a => a, comparer).First()).ToList();

        expectedAnimals.Should().BeSubsetOf(animalResults.Data.Items);
        animalResults.Data.Count.Should().Be(expectedAnimals.Count);
    }

    private static void CheckResultContents<T>(IEnumerable<AnimalOwner> owners, ChangeSetAggregator<AnimalOwner, Guid> ownerResults, ChangeSetAggregator<Animal, T> animalResults)
        where T : notnull
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

    private Func<Task> RandomDelay => () => Task.Delay(_randomizer.Number(MinTaskDelay, MaxTaskDelay));

    private static Func<Task> FakeDelay => () => Task.CompletedTask;

    private static int IdKey(Animal a) => a.Id;
    private static AnimalFamily FamilyKey(Animal a) => a.Family;

    private static Func<AnimalOwner, Guid, Task<ReadOnlyObservableCollection<Animal>>> SelectObservableCollection(Func<Task>? delayFactory = null) =>
        CreateSelector(static owner => owner.ObservableCollection, delayFactory);

    private static Func<AnimalOwner, Guid, Task<IObservableCache<Animal, int>>> SelectObservableCache(Func<Task>? delayFactory = null) =>
        CreateSelector(static owner => owner.ObservableCache, delayFactory);

    private static Func<AnimalOwner, Guid, Task<IEnumerable<Animal>>> SelectEnumerable(Func<Task>? delayFactory = null) =>
        CreateSelector(static owner => owner.Animals.Items.AsEnumerable(), delayFactory);

    private static Func<AnimalOwner, Guid, Task<T>> CreateSelector<T>(Func<AnimalOwner, T> selector, Func<Task>? delayFactory = null) =>
        (delayFactory != null)
            // If a delay factory is given, make it async
            ? (async (owner, guid) =>
            {
                await delayFactory().ConfigureAwait(false);
                return selector(owner);
            })

            // Otherwise make it not async
            : (owner, guid) => Task.FromResult(selector(owner));

    private IObservable<IChangeSet<Animal, int>> CreateObservableCollectionChangeSet(Func<Task>? delayFactory = null, IEqualityComparer<Animal>? equalityComparer = null, IComparer<Animal>? comparer = null) =>
        CreateObservableCollectionChangeSet(IdKey, delayFactory, equalityComparer, comparer);

    private IObservable<IChangeSet<Animal, TKey>> CreateObservableCollectionChangeSet<TKey>(Func<Animal, TKey> keySelector, Func<Task>? delayFactory = null, IEqualityComparer<Animal>? equalityComparer = null, IComparer<Animal>? comparer = null)
        where TKey : notnull
        => _animalOwners.Connect().TransformManyAsync(SelectObservableCollection(delayFactory), keySelector, equalityComparer, comparer);

    private IObservable<IChangeSet<Animal, int>> CreateEnumerableChangeSet(Func<Task>? delayFactory = null, IEqualityComparer<Animal>? equalityComparer = null, IComparer<Animal>? comparer = null) =>
        CreateEnumerableChangeSet(IdKey, delayFactory, equalityComparer, comparer);

    private IObservable<IChangeSet<Animal, TKey>> CreateEnumerableChangeSet<TKey>(Func<Animal, TKey> keySelector, Func<Task>? delayFactory = null, IEqualityComparer<Animal>? equalityComparer = null, IComparer<Animal>? comparer = null)
        where TKey : notnull
        => _animalOwners.Connect().TransformManyAsync(SelectEnumerable(delayFactory), keySelector, equalityComparer, comparer);

    private IObservable<IChangeSet<Animal, int>> CreateObservableCacheChangeSet(Func<Task>? delayFactory = null)
        => _animalOwners.Connect().TransformManyAsync(SelectObservableCache(delayFactory));

    private class TaskTracker(Func<Task> delayFactory)
    {
        private readonly object _lock = new();
        private readonly List<Task> _tasks = [];

        public Task Create() => Add(delayFactory());

        public Task Add(Task task) => task.With(t => { lock (_lock) _tasks.Add(task); } );

        public IEnumerable<Task> Add(IEnumerable<Task> tasks) => tasks.With(ts => ts.ForEach(t => Add(t)));

        public void Add(Action action, int count) => Add(Task.WhenAll(Enumerable.Range(0, count).Select(_ => FromAction(action))));

        public Task Add(Action action) => Add(FromAction(action));

        public Task<T> Add<T>(Func<T> func)
        {
            var task = Task.Run(async () =>
            {
                await delayFactory();
                return func();
            });

            Add(task);
            return task;
        }

        public async Task WhenAll()
        {
            // Wait on all tasks until no more are being added
            var list = GetList();
            while (list.Count > 0)
            {
                await Task.WhenAll(list);
                list = GetList();
            }

            // Wait a little extra
            await delayFactory();
        }

        private Task FromAction(Action action) =>
            Task.Run(async () =>
            {
                await delayFactory();
                action();
            });

        private List<Task> GetList()
        {
            lock(_lock)
            {
                var result = _tasks.ToList();
                _tasks.Clear();
                return result;
            }
        }
    }
}
