using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Bogus;

using FluentAssertions;

using Xunit;

using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public static partial class MergeManyChangeSetsFixture
{
    public static partial class ForListChangesets
    {
        public class UnitTests
        {
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

            [Theory]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public void ResultCompletesOnlyWhenSourceAndAllChildrenComplete(bool completeSource, bool completeChildren)
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                // Act
                animalOwners.Items.Skip(completeChildren ? 0 : 1).ForEach(owner => owner.Dispose());
                if (completeSource)
                {
                    animalOwners.Dispose();
                }

                // Assert
                animalOwnerResults.IsCompleted.Should().Be(completeSource);
                animalResults.IsCompleted.Should().Be(completeSource && completeChildren);
            }

            [Fact]
            public void ResultContainsAllInitialChildren()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                // Act

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(1);
                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultContainsChildrenFromAddedParents()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var addThis = animalOwnerFaker.Generate();

                // Act
                animalOwners.AddOrUpdate(addThis);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount + 1);
                animalResults.Messages.Count.Should().Be(2);
                addThis.Animals.Items.ForEach(added => animalResults.Data.Items.Should().Contain(added));

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultContainsChildrenAddedWithAddRange()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var initialCount = animalOwners.Items.Sum(owner => owner.Animals.Count);
                var totalAdded = new List<Animal>();

                // Act
                animalOwners.Items.ForEach(owner => owner.Animals.AddRange(animalFaker.Generate(AddRangeSize).With(added => totalAdded.AddRange(added))));

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(1 + InitialOwnerCount); // Initial + 1 for each Range Added
                totalAdded.ForEach(animal => animalResults.Data.Items.Should().Contain(animal));
                animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + totalAdded.Count);

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultContainsChildrenAddedWithInsert()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var randomOwner = randomizer.ListItem(animalOwners.Items.ToList());
                var insertIndex = randomizer.Number(randomOwner.Animals.Items.Count);
                var insertThis = animalFaker.Generate();
                var initialCount = animalOwners.Items.Sum(owner => owner.Animals.Count);

                // Act
                randomOwner.Animals.Insert(insertIndex, insertThis);

                // Assert
                randomOwner.Animals.Items.ElementAt(insertIndex).Should().Be(insertThis);
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(2);
                animalResults.Data.Items.Should().Contain(insertThis);
                animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount + 1);
                
                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultContainsCorrectItemsAfterChildClear()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var randomOwner = randomizer.ListItem(animalOwners.Items.ToList());
                var removedAnimals = randomOwner.Animals.Items.ToList();

                // Act
                randomOwner.Animals.Clear();

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(2);
                randomOwner.Animals.Count.Should().Be(0);
                removedAnimals.ForEach(removed => animalResults.Data.Items.Should().NotContain(removed));

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultContainsCorrectItemsAfterChildReplacement()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var randomOwner = randomizer.ListItem(animalOwners.Items.ToList());
                var replaceThis = randomizer.ListItem(randomOwner.Animals.Items.ToList());
                var withThis = animalFaker.Generate();

                // Act
                randomOwner.Animals.Replace(replaceThis, withThis);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(2);
                randomOwner.Animals.Items.Should().NotContain(replaceThis);
                randomOwner.Animals.Items.Should().Contain(withThis);

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultContainsCorrectItemsAfterParentUpdate()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var replaceThis = randomizer.ListItem(animalOwners.Items.ToList());
                var withThis = CreateWithSameId(animalOwnerFaker, replaceThis);

                // Act
                animalOwners.AddOrUpdate(withThis);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount); // Owner Count should not change
                animalResults.Messages.Count.Should().Be(2); // 2 = Initial Add and one changeset with remove old items / add new items
                replaceThis.Animals.Items.ForEach(removed => animalResults.Data.Items.Should().NotContain(removed));
                withThis.Animals.Items.ForEach(added => animalResults.Data.Items.Should().Contain(added));

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);

                replaceThis.Dispose();
            }

            [Fact]
            public void ResultDoesNotContainChildrenFromParentsBatchRemoved()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var removeThese = randomizer.ListItems(animalOwners.Items.ToList(), RemoveRangeSize);

                // Act
                animalOwners.Remove(removeThese);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - RemoveRangeSize);
                animalResults.Messages.Count.Should().Be(2);
                removeThese.SelectMany(owner => owner.Animals.Items).ForEach(removed => animalResults.Data.Items.Should().NotContain(removed));
                
                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
                
                removeThese.ForEach(owner => owner.Dispose());
            }

            [Fact(Timeout = 10_000)]
            public async Task ResultDoesNotContainChildrenFromParentAddedAndRemovedWhileAnotherThreadIsDelivering()
            {
                // Arrange
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);

                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                using var deliveringOwner = animalOwnerFaker.Generate();
                using var transientOwner = animalOwnerFaker.Generate().AddAnimals(animalFaker, 1, AddRangeSize);
                animalOwners.AddOrUpdate(deliveringOwner);

                var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var release = new ManualResetEventSlim();
                var parkNextDelivery = false;
                using var animalResults = animalOwners.Connect()
                    .MergeManyChangeSets(owner => owner.Animals.Connect())
                    .Do(_ =>
                    {
                        if (parkNextDelivery)
                        {
                            parkNextDelivery = false;
                            parked.SetResult();
                            release.Wait();
                        }
                    })
                    .AsAggregator();

                // Park a delivery on another thread, so that both parent changes queue up behind it. The transient
                // owner's children are subscribed while its removal is already queued.
                parkNextDelivery = true;
                var delivering = Task.Run(() => deliveringOwner.Animals.Add(animalFaker.Generate()));
                await parked.Task;
                animalOwners.AddOrUpdate(transientOwner);
                animalOwners.Remove(transientOwner);

                // Act
                release.Set();
                await delivering;

                // Assert
                animalResults.Data.Items.Should().BeEquivalentTo(deliveringOwner.Animals.Items);
            }

            [Fact]
            public void ResultDoesNotContainChildrenFromParentsRemovedWithRemove()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var removeThis = randomizer.ListItem(animalOwners.Items.ToList());

                // Act
                animalOwners.Remove(removeThis);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount - 1);
                animalResults.Messages.Count.Should().Be(2);
                removeThis.Animals.Items.ForEach(removed => animalResults.Data.Items.Should().NotContain(removed));

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);

                removeThis.Dispose();
            }

            [Fact]
            public void ResultDoesNotContainChildrenRemovedWithRemove()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var randomOwner = randomizer.ListItem(animalOwners.Items.ToList());
                var removeThis = randomizer.ListItem(randomOwner.Animals.Items.ToList());
                var initialCount = animalOwners.Items.Sum(owner => owner.Animals.Count);

                // Act
                randomOwner.Animals.Remove(removeThis);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(2);
                animalResults.Data.Items.Should().NotContain(removeThis);
                animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount - 1);

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultDoesNotContainChildrenRemovedWithRemoveAt()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var randomOwner = randomizer.ListItem(animalOwners.Items.ToList());
                var removeIndex = randomizer.Number(randomOwner.Animals.Count - 1);
                var removeThis = randomOwner.Animals.Items.ElementAt(removeIndex);
                var initialCount = animalOwners.Items.Sum(owner => owner.Animals.Count);

                // Act
                randomOwner.Animals.RemoveAt(removeIndex);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(2);
                animalResults.Data.Items.Should().NotContain(removeThis);
                animalOwners.Items.Sum(owner => owner.Animals.Count).Should().Be(initialCount - 1);

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultDoesNotContainChildrenRemovedWithRemoveMany()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var randomOwner = randomizer.ListItem(animalOwners.Items.ToList());
                var removeCount = randomizer.Number(1, randomOwner.Animals.Count - 1);
                var removeThese = randomizer.ListItems(randomOwner.Animals.Items.ToList(), removeCount);

                // Act
                randomOwner.Animals.RemoveMany(removeThese);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(2);
                removeThese.ForEach(removed => randomOwner.Animals.Items.Should().NotContain(removed));

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultDoesNotContainChildrenRemovedWithRemoveRange()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var randomOwner = randomizer.ListItem(animalOwners.Items.ToList());
                var removeCount = randomizer.Number(1, randomOwner.Animals.Count - 1);
                var removeIndex = randomizer.Number(randomOwner.Animals.Count - removeCount - 1);
                var removeThese = randomOwner.Animals.Items.Skip(removeIndex).Take(removeCount);

                // Act
                randomOwner.Animals.RemoveRange(removeIndex, removeCount);

                // Assert
                animalOwnerResults.Data.Count.Should().Be(InitialOwnerCount);
                animalResults.Messages.Count.Should().Be(2);
                removeThese.ForEach(removed => randomOwner.Animals.Items.Should().NotContain(removed));

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);
            }

            [Fact]
            public void ResultEmptyIfSourceIsCleared()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();
                using var animalResults = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                var items = animalOwners.Items.ToList();

                // Act
                animalOwners.Clear();

                // Assert
                animalOwnerResults.Data.Count.Should().Be(0);
                animalResults.Data.Count.Should().Be(0);

                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);

                items.ForEach(owner => owner.Dispose());
            }

            [Fact]
            public void ResultFailsIfSourceFails()
            {
                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                // Arrange
                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                var expectedError = new Exception("Expected");
                var throwObservable = Observable.Throw<IChangeSet<AnimalOwner, Guid>>(expectedError);
                using var results = animalOwners.Connect().Concat(throwObservable).MergeManyChangeSets(owner => owner.Animals.Connect()).AsAggregator();

                // Act
                animalOwners.Dispose();

                // Assert
                results.Exception.Should().Be(expectedError);
            }

            private static AnimalOwner CreateWithSameId(
                Faker<AnimalOwner> animalOwnerFaker,
                AnimalOwner original)
            {
                var newOwner = animalOwnerFaker.Generate();
                var sameId = new AnimalOwner(newOwner.Name, original.Id);
                sameId.Animals.AddRange(newOwner.Animals.Items);
                return sameId;
            }
        }
    }
}
