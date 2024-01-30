﻿using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Bogus;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

using FluentAssertions;

using Xunit;

namespace DynamicData.Tests.Cache;

public class TransformOnObservableFixture : IDisposable
{
#if DEBUG
    const int InitialCount = 7;
    const int AddCount = 5;
    const int RemoveCount = 3;
#else
    const int InitialCount = 103;
    const int AddCount = 53;
    const int RemoveCount = 37;
#endif

    private readonly ISourceCache<Animal, int> _animalCache = new SourceCache<Animal, int>(a => a.Id);
    private readonly ChangeSetAggregator<Animal, int> _animalResults;
    private readonly Faker<Animal> _animalFaker;
    private readonly Randomizer _randomizer = new (0x2112_2112);

    public TransformOnObservableFixture()
    {
        _animalFaker = Fakers.Animal.Clone().WithSeed(_randomizer);
        _animalCache.AddOrUpdate(_animalFaker.Generate(InitialCount));
        _animalResults = _animalCache.Connect().AsAggregator();
    }

    [Fact]
    public void ResultContainsAllInitialChildren()
    {
        // Arrange

        // Act
        using var results = _animalCache.Connect().TransformOnObservable((ani, id) => Observable.Return(ani.Name)).AsAggregator();

        // Assert
        _animalResults.Data.Count.Should().Be(InitialCount);
        results.Data.Count.Should().Be(InitialCount);
        results.Messages.Count.Should().Be(1);
    }

    [Fact]
    public void ResultContainsAddedValues()
    {
        // Arrange
        using var results = _animalCache.Connect().TransformOnObservable((ani, id) => Observable.Return(ani.Name)).AsAggregator();

        // Act
        _animalCache.AddOrUpdate(_animalFaker.Generate(AddCount));

        // Assert
        _animalResults.Data.Count.Should().Be(InitialCount + AddCount);
        results.Data.Count.Should().Be(_animalResults.Data.Count);
        results.Messages.Count.Should().Be(2);
    }

    [Fact]
    public void ResultDoesNotContainRemovedValues()
    {
        // Arrange
        using var results = _animalCache.Connect().TransformOnObservable((ani, id) => Observable.Return(ani.Name)).AsAggregator();

        // Act
        _animalCache.RemoveKeys(_randomizer.ListItems(_animalCache.Items.ToList(), RemoveCount).Select(a => a.Id));

        // Assert
        _animalResults.Data.Count.Should().Be(InitialCount - RemoveCount);
        results.Data.Count.Should().Be(_animalResults.Data.Count);
        results.Messages.Count.Should().Be(2);
    }

    [Fact]
    public async Task ResultUpdatesOnFutureValues()
    {
        IObservable<string> CreateChildObs(Animal a, int id) =>
            Observable.Return(string.Empty).Concat(Observable.Timer(TimeSpan.FromMilliseconds(100)).Select(_ => a.Name));

        // Arrange
        var shared = _animalCache.Connect().TransformOnObservable(CreateChildObs).Publish();
        using var results = shared.AsAggregator();
        var task = Task.Run(async () => await shared);
        using var cleanup = shared.Connect();
        _animalCache.Dispose();

        // Act
        await task;

        // Assert
        _animalResults.Data.Count.Should().Be(InitialCount);
        results.Data.Count.Should().Be(_animalResults.Data.Count);
        results.Summary.Overall.Adds.Should().Be(InitialCount);
        results.Summary.Overall.Updates.Should().Be(InitialCount);
        results.Messages.Count.Should().BeGreaterThan(1);
        _animalCache.Items.ForEach(animal => results.Data.Lookup(animal.Id).Should().Be(animal.Name));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ResultCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
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
        _animalResults.IsCompleted.Should().Be(completeSource);
        results.IsCompleted.Should().Be(completeSource && completeChildren);
    }

    [Fact]
    public void ResultFailsIfSourceFails()
    {
        // Arrange
        var expectedError = new Exception("Expected");
        var throwObservable = Observable.Throw<IChangeSet<Animal, int>>(expectedError);
        using var results = _animalCache.Connect().Concat(throwObservable).TransformOnObservable(animal => Observable.Return(animal)).AsAggregator();

        // Act
        _animalCache.Dispose();

        // Assert
        results.Error.Should().Be(expectedError);
    }

    public void Dispose()
    {
        _animalCache.Dispose();
        _animalResults.Dispose();
    }
}
