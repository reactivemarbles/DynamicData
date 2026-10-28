namespace DynamicData.Tests.Cache;

public static partial class MergeManyChangeSetsFixture
{
    public static partial class ForListChangesets
    {
        public sealed class IntegrationTests
            : IntegrationTestFixtureBase
        {
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

                using var animalOwners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
                
                var randomizer = new Randomizer(0x01221948);
                var animalFaker = Fakers.Animal.Clone().WithSeed(randomizer);
                var animalOwnerFaker = Fakers.AnimalOwner.Clone().WithSeed(randomizer).WithInitialAnimals(animalFaker);

                animalOwners.AddOrUpdate(animalOwnerFaker.Generate(InitialOwnerCount));

                using var animalOwnerResults = animalOwners.Connect().AsAggregator();

                var mergeAnimals = animalOwners.Connect().MergeManyChangeSets(owner => owner.Animals.Connect()).Publish();
                var addingAnimals = true;
                var cacheCompleted = mergeAnimals.LastOrDefaultAsync().ToTask();
                using var animalResults = mergeAnimals.AsAggregator();
                using var connect = mergeAnimals.Connect();

                // Start asynchronously modifying the parent list and the child lists
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

                // Wait for the source cache to finish delivering all notifications.
                await cacheCompleted;

                // Verify the results against the aggregator wired into the same Publish chain
                // that cacheCompleted observes.
                CheckResultContents(animalOwners.Items, animalOwnerResults, animalResults);


                TimeSpan? GetRemoveTime() => randomizer.Bool() ? randomizer.TimeSpan(MaxRemoveTime) : null;

                IObservable<Unit> AddRemoveAnimalsStress(int ownerCount, int animalCount, int parallel, IScheduler scheduler) =>
                    Observable.Create<Unit>(observer => new CompositeDisposable
                    (
                        AddRemoveOwners(ownerCount, parallel, scheduler)
                            .Subscribe(
                                onNext: static _ => { },
                                onError: observer.OnError),

                        animalOwners.Connect()
                            .MergeMany(owner => AddRemoveAnimals(owner, animalCount, parallel, scheduler))
                            .Subscribe(
                                onNext: static _ => { },
                                onError: observer.OnError,
                                onCompleted: observer.OnCompleted)
                    ));

                IObservable<AnimalOwner> AddRemoveOwners(int ownerCount, int parallel, IScheduler scheduler) =>
                    animalOwnerFaker.IntervalGenerate(randomizer, MaxAddTime, scheduler)
                        .Parallelize(ownerCount, parallel, obs => obs.StressAddRemove(animalOwners, _ => GetRemoveTime(), scheduler))
                        .Finally(animalOwners.Dispose);

                IObservable<Animal> AddRemoveAnimals(AnimalOwner owner, int animalCount, int parallel, IScheduler scheduler) =>
                    animalFaker.IntervalGenerate(randomizer, MaxAddTime, scheduler)
                        .Parallelize(animalCount, parallel, obs => obs.StressAddRemove(owner.Animals, _ => GetRemoveTime(), scheduler))
                        .Finally(owner.Animals.Dispose);
            }
        }
    }
}
