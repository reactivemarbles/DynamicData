using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

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
        unchecked { _randomizer = new Randomizer((int)0xf7ee_bee7); }

        _animalFaker = Fakers.Animal.Clone().WithSeed(_randomizer);
        _animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(_randomizer).WithInitialAnimals(_animalFaker);

        _animalOwnerResults = _animalOwners.Connect().AsAggregator();
    }

    [Test]
    public async Task EnumerableResultContainsAllInitialChildrenInSingleChangeSet()
    {
        // Arrange
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Act
        using var animalResults = CreateEnumerableChangeSet().AsAggregator();

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(animalResults.Messages.Count).IsEqualTo(1);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Test]
    public async Task ResultContainsAllInitialChildren()
    {
        // Arrange
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Act
        using var animalResults = CreateObservableCollectionChangeSet().AsAggregator();

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(animalResults.Messages.Count).IsEqualTo(1);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Test]
    public async Task ResultContainsChildrenFromAddedParents()
    {
        // Arrange
        using var animalResults = CreateObservableCollectionChangeSet().AsAggregator();

        // Act
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(animalResults.Messages.Count).IsEqualTo(1);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Test]
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
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await Assert.That(animalResults.Messages.Count).IsGreaterThan(0);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Test]
    public async Task ResultContainsAddedChildrenFromExistingParents()
    {
        // Arrange
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));
        using var animalResults = CreateObservableCollectionChangeSet().AsAggregator();

        // Act
        _animalOwners.Items.ForEach(owner => owner.AddAnimals(_animalFaker, 1, AddCount));

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Test]
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
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount - RemoveCount);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Test]
    public async Task ResultsWorkWithComparer()
    {
        // Arrange
        using var animalResults = CreateObservableCollectionChangeSet(FamilyKey, comparer: Animal.NameComparer).AsAggregator();

        // Act
        _animalOwners.AddOrUpdate(_animalOwnerFaker.Generate(InitialOwnerCount));

        // Assert
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults, FamilyKey, Animal.NameComparer);
    }

    [Test]
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
        await Assert.That(_animalOwnerResults.Data.Count).IsEqualTo(InitialOwnerCount + ownerAddCount);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ResultCompletesOnlyWhenSourceCompletes(bool completeSource)
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
        await Assert.That(_animalOwnerResults.IsCompleted).IsEqualTo(completeSource);
        await CheckResultContents(_animalOwners.Items, _animalOwnerResults, animalResults);
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
        _animalOwners.Dispose();
    }

    private AnimalOwner CreateWithSameId(AnimalOwner original)
    {
        var newOwner = _animalOwnerFaker.Generate();
        var sameId = new AnimalOwner(newOwner.Name, original.Id);
        sameId.Animals.AddRange(newOwner.Animals.Items);
        return sameId;
    }

    private static async Task CheckResultContents<T>(IEnumerable<AnimalOwner> owners, ChangeSetAggregator<AnimalOwner, Guid> ownerResults, ChangeSetAggregator<Animal, T> animalResults, Func<Animal, T> keySelector, IComparer<Animal> comparer)
        where T : notnull
    {
        var expectedOwners = owners.ToList();

        // These should be subsets of each other
        await Assert.That(expectedOwners.Except(ownerResults.Data.Items).Any()).IsFalse();
        await Assert.That(ownerResults.Data.Items.Count).IsEqualTo(expectedOwners.Count);

        var allAnimals = expectedOwners.SelectMany(owner => owner.Animals.Items).ToList();
        var expectedAnimals = allAnimals.GroupBy(keySelector).Select(group => group.OrderBy(a => a, comparer).First()).ToList();

        await Assert.That(expectedAnimals.Except(animalResults.Data.Items).Any()).IsFalse();
        await Assert.That(animalResults.Data.Count).IsEqualTo(expectedAnimals.Count);
    }

    private static async Task CheckResultContents<T>(IEnumerable<AnimalOwner> owners, ChangeSetAggregator<AnimalOwner, Guid> ownerResults, ChangeSetAggregator<Animal, T> animalResults)
        where T : notnull
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

        public Task Add(Task task) => task.With(t => { lock (_lock) _tasks.Add(task); });

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
            lock (_lock)
            {
                var result = _tasks.ToList();
                _tasks.Clear();
                return result;
            }
        }
    }
}
