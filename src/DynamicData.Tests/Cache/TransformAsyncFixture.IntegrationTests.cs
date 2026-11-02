// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Tests.Cache;

public partial class TransformAsyncFixture
{
    /// <summary>
    /// Tests that hold threads at a <see cref="Barrier"/> to force forced transforms and source updates to collide. The
    /// held threads are dedicated and the tests run outside the parallel collection, so a starved thread pool cannot
    /// hold them up.
    /// </summary>
    public sealed class IntegrationTests
        : IntegrationTestFixtureBase
    {
        // The forced-transform chain applies its cache updates when its async transforms finish, which
        // is off any gate unless the operator puts one there, and UnsynchronizedMerge supplies no gate
        // of its own. Rather than race and hope the window opens, this holds one transform outstanding
        // on each chain and releases them together, which is precisely the collision.
        [Fact(Timeout = 10_000)]
        public async Task ForcedTransformCompletingAlongsideSourceUpdate_AreDeliveredSerially()
        {
            using var source = new SourceCache<Person, string>(p => p.Name);
            using var force = new Subject<Func<Person, string, bool>>();
            using var registered = new SemaphoreSlim(0);

            // One release handle per transform invocation, so each can be completed on command.
            var outstanding = new ConcurrentDictionary<string, ConcurrentQueue<TaskCompletionSource>>();

            var published = source.Connect()
                .TransformAsync(
                    async person =>
                    {
                        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        outstanding.GetOrAdd(person.Name, static _ => new ConcurrentQueue<TaskCompletionSource>()).Enqueue(release);
                        registered.Release();

                        await release.Task;

                        return new PersonWithGender(person, person.Age % 2 == 0 ? "M" : "F");
                    },
                    force)
                .ValidateSynchronization()

                // ValidateSynchronization tracks the whole in-flight period of a notification,
                // including downstream work, so holding each one makes an overlapping delivery
                // observable instead of something that has to be caught in a sub-microsecond window.
                .Do(static _ => Thread.Sleep(100))
                .Publish();

            var terminal = published.Materialize().LastAsync().ToTask();
            using var results = published.AsAggregator();
            using var connection = published.Connect();

            // Seed a single item, so that the forced pass has something to re-transform.
            source.AddOrUpdate(new Person("Seed", 2));
            await registered.WaitAsync();
            Release(outstanding, "Seed");
            await results.Data.CountChanged.Where(static count => count == 1).Take(1);

            // Leave one transform outstanding on each chain: the forced pass re-transforms the seed,
            // and the source update introduces a second item through the other chain.
            force.OnNext(static (_, _) => true);
            await registered.WaitAsync();

            source.AddOrUpdate(new Person("Added", 4));
            await registered.WaitAsync();

            // Release both at once. Each chain applies its cache updates and emits on whichever thread
            // completed it, so this is the moment the two can collide.
            using (var barrier = new Barrier(3))
            {
                var forced = DedicatedThread.Run(() =>
                {
                    barrier.SignalAndWait();
                    Release(outstanding, "Seed");
                });

                var added = DedicatedThread.Run(() =>
                {
                    barrier.SignalAndWait();
                    Release(outstanding, "Added");
                });

                barrier.SignalAndWait();
                await Task.WhenAll(forced, added);
            }

            // Both merge inputs have to complete before the merged sequence does.
            force.OnCompleted();
            source.Dispose();

            var lastNotification = await terminal;

            lastNotification.Exception.Should().BeNull("a forced transform and a source update must never be delivered concurrently");
            lastNotification.Kind.Should().Be(NotificationKind.OnCompleted, "the sequence should end by completing, not by faulting");

            static void Release(ConcurrentDictionary<string, ConcurrentQueue<TaskCompletionSource>> outstanding, string name)
            {
                outstanding[name].TryDequeue(out var release).Should().BeTrue($"a transform for {name} should be outstanding");
                release!.SetResult();
            }
        }

        // Serialization has to hold under arbitrary interleaving, not only the one scripted collision
        // above. Several writers drive the source while another drives forced passes, and every
        // notification is checked both for overlap and for structural integrity, since the two chains
        // also share the cache that produces those change sets.
        [Fact(Timeout = 30_000)]
        public async Task ForcedTransformsUnderConcurrentLoad_AreDeliveredSerially()
        {
            const int writerCount = 3;
            const int seedCount = 5;

            var randomizer = new Randomizer(0x1097);
            var iterations = randomizer.Int(150, 250);

            using var source = new SourceCache<Person, string>(p => p.Name);
            using var force = new Subject<Func<Person, string, bool>>();

            var published = source.Connect()
                .TransformAsync(
                    async person =>
                    {
                        await Task.Yield();
                        return new PersonWithGender(person, person.Age % 2 == 0 ? "M" : "F");
                    },
                    force)
                .ValidateSynchronization()
                .ValidateChangeSets(static personWithGender => personWithGender.Name)

                // Each notification is held for a moment so that an overlapping delivery is actually
                // observed, rather than passing through a window too narrow to catch.
                .Do(static _ => Thread.SpinWait(2_000))
                .Publish();

            var terminal = published.Materialize().LastAsync().ToTask();
            using var connection = published.Connect();

            var names = Enumerable.Range(1, seedCount).Select(i => "Name" + i).ToArray();
            source.AddOrUpdate(names.Select((name, i) => new Person(name, i + 1)));

            // The main thread joins the barrier so every writer starts at the same moment.
            using var barrier = new Barrier(writerCount + 2);

            var writers = Enumerable.Range(0, writerCount)
                .Select(writer => DedicatedThread.Run(() =>
                {
                    var writerRandomizer = new Randomizer(0x1097 + writer + 1);
                    barrier.SignalAndWait();

                    for (var i = 0; i < iterations; i++)
                    {
                        source.AddOrUpdate(new Person(writerRandomizer.ArrayElement(names), writerRandomizer.Int(1, 80)));
                    }
                }))
                .ToArray();

            var forcer = DedicatedThread.Run(() =>
            {
                barrier.SignalAndWait();

                for (var i = 0; i < iterations; i++)
                {
                    force.OnNext(static (_, _) => true);
                }
            });

            barrier.SignalAndWait();
            await Task.WhenAll(writers.Append(forcer));

            // Both merge inputs have to complete before the merged sequence does.
            force.OnCompleted();
            source.Dispose();

            var lastNotification = await terminal;

            lastNotification.Exception.Should().BeNull("deliveries must neither overlap nor carry inconsistent change sets, however the writers interleave");
            lastNotification.Kind.Should().Be(NotificationKind.OnCompleted, "the sequence should end by completing, not by faulting");
        }
    }
}
