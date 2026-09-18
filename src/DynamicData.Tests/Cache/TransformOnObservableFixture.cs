using Bogus;
#if REACTIVE_TESTS
using DynamicData.Reactive.Kernel;
#else
using DynamicData.Kernel;
#endif
using DynamicData.Tests.Domain;

namespace DynamicData.Tests.Cache;

public class TransformOnObservableFixture : IDisposable
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
    private const int UpdateCount = 31;
#endif
    private static readonly TimeSpan UpdateTime = TimeSpan.FromMilliseconds(50);

    private readonly ISourceCache<Animal, int> _animalCache = new SourceCache<Animal, int>(a => a.Id);
    private readonly ChangeSetAggregator<Animal, int> _animalResults;
    private readonly Faker<Animal> _animalFaker;
    private readonly Randomizer _randomizer = new(0x2112_2112);

    public TransformOnObservableFixture()
    {
        _animalFaker = Fakers.Animal.Clone().WithSeed(_randomizer);
        _animalCache.AddOrUpdate(_animalFaker.Generate(InitialCount));
        _animalResults = _animalCache.Connect().AsAggregator();
    }

    [Test]
    public async Task ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act
        using var results = _animalCache.Connect().TransformOnObservable((ani, id) => Observable.Return(ani.Name)).AsAggregator();

        // Assert
        await Assert.That(_animalResults.Data.Count).IsEqualTo(InitialCount);
        await Assert.That(results.Data.Count).IsEqualTo(InitialCount);
        await Assert.That(results.Messages.Count).IsEqualTo(1).Because("The child observables fire on subscription so everything should appear as a single changeset");
    }

    [Test]
    public async Task ResultContainsAddedValues()
    {
        // Arrange
        using var results = _animalCache.Connect().TransformOnObservable((ani, id) => Observable.Return(ani.Name)).AsAggregator();

        // Act
        _animalCache.AddOrUpdate(_animalFaker.Generate(AddCount));

        // Assert
        await Assert.That(_animalResults.Data.Count).IsEqualTo(InitialCount + AddCount);
        await Assert.That(results.Data.Count).IsEqualTo(_animalResults.Data.Count);
        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("Initial Adds and then the subsequent Additions should each be a single message");
    }

    [Test]
    public async Task ResultDoesNotContainRemovedValues()
    {
        // Arrange
        using var results = _animalCache.Connect().TransformOnObservable((ani, id) => Observable.Return(ani.Name)).AsAggregator();

        // Act
        _animalCache.RemoveKeys(_randomizer.ListItems(_animalCache.Items.ToList(), RemoveCount).Select(a => a.Id));

        // Assert
        await Assert.That(_animalResults.Data.Count).IsEqualTo(InitialCount - RemoveCount);
        await Assert.That(results.Data.Count).IsEqualTo(_animalResults.Data.Count);
        await Assert.That(results.Messages.Count).IsEqualTo(2).Because("1 for Adds and 1 for Removes");
    }

    [Test]
    public async Task ResultUpdatesOnFutureValues()
    {
        // Create an observable that fires a wrong value on an interval a fixed number of times
        // then fires the expected value before completing
        IObservable<string> CreateChildObs(Animal a, int id) =>
            Observable.Interval(UpdateTime)
                .Select(n => $"{a.Name}-{id}-{n}")
                .Take(UpdateCount)
                .Concat(Observable.Return(a.Name));

        // Arrange
        var shared = _animalCache.Connect().TransformOnObservable(CreateChildObs).Publish();
        using var results = shared.AsAggregator();
        var task = Task.Run(async () => await shared);
        using var cleanup = shared.Connect();
        _animalCache.Dispose();

        // Act
        await task;

        // Assert
        await Assert.That(_animalResults.Data.Count).IsEqualTo(InitialCount);
        await Assert.That(results.Data.Count).IsEqualTo(_animalResults.Data.Count);
        await Assert.That(results.Summary.Overall.Adds).IsEqualTo(InitialCount);
        await Assert.That(results.Summary.Overall.Updates).IsEqualTo(InitialCount * UpdateCount).Because($"Each item should update {UpdateCount} times");
        await Assert.That(results.Messages.Count).IsGreaterThanOrEqualTo(1).Because("The delay may cause the messages to appear as multiple changesets");
        foreach (var animal in _animalCache.Items) { await Assert.That(results.Data.Lookup(animal.Id)).IsEqualTo(ReactiveUI.Primitives.Optional.Some(animal.Name)); }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task ResultCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
    {
        IObservable<string> CreateChildObs(Animal a, int id) =>
            completeChildren
                ? Observable.Return(a.Name)
                : Observable.Return(a.Name).Concat(Observable.Never<string>());

        // Arrange
        using var results = _animalCache.Connect().TransformOnObservable(CreateChildObs).AsAggregator();

        // Act
        if (completeSource)
        {
            _animalCache.Dispose();
        }

        // Assert
        await Assert.That(_animalResults.IsCompleted).IsEqualTo(completeSource);
        await Assert.That(results.IsCompleted).IsEqualTo(completeSource && completeChildren);
    }

    [Test]
    public async Task ResultFailsIfChildFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<Animal, int>>(expectedError);

        // Act
        using var results = _animalCache.Connect().TransformOnObservable(_ => throwObservable).AsAggregator();

        // Assert
        await Assert.That(results.Error).IsEqualTo(expectedError);
    }

    [Test]
    public async Task ResultFailsIfSourceFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<Animal, int>>(expectedError);
        using var results = _animalCache.Connect().Concat(throwObservable).TransformOnObservable(Observable.Return).AsAggregator();

        // Act
        _animalCache.Dispose();

        // Assert
        await Assert.That(results.Error).IsEqualTo(expectedError);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task OrderOfChangesIsPreserved(bool removeFirst)
    {
        // Arrange
        using var results = _animalCache.Connect().TransformOnObservable(Observable.Return).AsAggregator();
        (var firstReason, var nextReason, var expectedChanges) = removeFirst
            ? (ChangeReason.Remove, ChangeReason.Add, InitialCount * 2)
            : (ChangeReason.Add, ChangeReason.Remove, InitialCount * 3);

        // Act
        _animalCache.Edit(updater =>
        {
            if (removeFirst)
            {
                updater.Clear();
                updater.AddOrUpdate(_animalFaker.Generate(InitialCount));
            }
            else
            {
                updater.AddOrUpdate(_animalFaker.Generate(InitialCount));
                updater.Clear();
            }
        });

        // Assert
        await Assert.That(results.Messages.Count).IsEqualTo(2);
        await Assert.That(results.Messages[1].Count).IsEqualTo(expectedChanges);
        await Assert.That(results.Messages[1].Take(InitialCount).All(change => change.Reason == firstReason)).IsTrue();
        await Assert.That(results.Messages[1].Skip(InitialCount).All(change => change.Reason == nextReason)).IsTrue();
    }

    public void Dispose()
    {
        _animalCache.Dispose();
        _animalResults.Dispose();
    }
}
