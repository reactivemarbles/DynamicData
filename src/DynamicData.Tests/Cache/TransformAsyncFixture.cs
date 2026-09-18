using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using DynamicData.Binding;
using DynamicData.Tests.Domain;
using DynamicData.Tests.Utilities;
using FluentAssertions;
using Xunit;

// Aliased rather than importing Bogus wholesale, which would make Person ambiguous against the
// domain type of the same name.
using Randomizer = Bogus.Randomizer;

namespace DynamicData.Tests.Cache;

public class TransformAsyncFixture
{
    [Fact]
    public async Task Add()
    {
        using var stub = new TransformStub();
        var person = new Person("Adult1", 50);
        stub.Source.AddOrUpdate(person);

        stub.Results.Messages.Count.Should().Be(1, "Should be 1 updates");
        stub.Results.Data.Count.Should().Be(1, "Should be 1 item in the cache");

        var firstPerson = await stub.TransformFactory(person);

        stub.Results.Data.Items[0].Should().Be(firstPerson, "Should be same person");
    }

    [Fact]
    public async Task BatchOfUniqueUpdates()
    {
        var people = Enumerable.Range(1, 100).Select(i => new Person("Name" + i, i)).ToArray();
        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(people);

        //     Thread.Sleep(10000);

        stub.Results.Messages.Count.Should().Be(1, "Should be 1 updates");
        stub.Results.Messages[0].Adds.Should().Be(100, "Should return 100 adds");

        var result = await Task.WhenAll(people.Select(stub.TransformFactory));
        var transformed = result.OrderBy(p => p.Age).ToArray();
        stub.Results.Data.Items.OrderBy(p => p.Age).Should().BeEquivalentTo(stub.Results.Data.Items.OrderBy(p => p.Age), "Incorrect transform result");
    }

    [Fact]
    public void Clear()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 100).Select(l => new Person("Name" + l, l)).ToArray();

        stub.Source.AddOrUpdate(people);
        stub.Source.Clear();

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(100, "Should be 80 adds");
        stub.Results.Messages[1].Removes.Should().Be(100, "Should be 80 removes");
        stub.Results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public void HandleError()
    {
        using var stub = new TransformStub(p => throw new Exception("Broken"));
        stub.Source.AddOrUpdate(new Person("Name1", 1));

        stub.Results.Error.Should().NotBeNull();
    }

    [Fact]
    public void Remove()
    {
        const string key = "Adult1";
        var person = new Person(key, 50);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(person);
        stub.Source.Remove(key);

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(1, "Should be 80 addes");
        stub.Results.Messages[1].Removes.Should().Be(1, "Should be 80 removes");
        stub.Results.Data.Count.Should().Be(0, "Should be nothing cached");
    }

    [Fact]
    public async Task RemoveFlowsToTheEnd()
    {
        var count = 100;
        ReadOnlyObservableCollection<Person> collection;

        var cache = new SourceCache<Person, string>(p => p.Name);
        var people = Enumerable.Range(1, count).Select(l => new Person("Name" + l, l)).ToArray();

        cache.Connect()
            .TransformAsync(async person =>
            {
                await Task.Delay(Random.Shared.Next(1, 12));
                return person;
            })
            .Bind(out collection)
            .Subscribe();

        foreach (var p in people)
        {
            cache.AddOrUpdate(p);
            cache.RemoveKey(p.Name);
        }

        // Add one event as an initial empty change set is sent
        // NOTE TO SELF: How did this test previously work !
       var changes = await collection.ToObservableChangeSet().Take(count * 2 + 1).ToList();

       changes.Count.Should().Be(201);
        collection.Count.Should().Be(0);
    }

    [Fact]
    public void ReTransformAll()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
        var forceTransform = new Subject<Unit>();

        using var stub = new TransformStub(forceTransform);
        stub.Source.AddOrUpdate(people);
        forceTransform.OnNext(Unit.Default);

        stub.Results.Messages.Count.Should().Be(2);
        stub.Results.Messages[1].Updates.Should().Be(10);

        for (var i = 1; i <= 10; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;

            updated.Should().Be(original);
            ReferenceEquals(original, updated).Should().BeFalse();
        }
    }

    [Fact]
    public void ReTransformSelected()
    {
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name" + i, i)).ToArray();
        var forceTransform = new Subject<Func<Person, bool>>();

        using var stub = new TransformStub(forceTransform);
        stub.Source.AddOrUpdate(people);
        forceTransform.OnNext(person => person.Age <= 5);

        stub.Results.Messages.Count.Should().Be(2);
        stub.Results.Messages[1].Updates.Should().Be(5);

        for (var i = 1; i <= 5; i++)
        {
            var original = stub.Results.Messages[0].ElementAt(i - 1).Current;
            var updated = stub.Results.Messages[1].ElementAt(i - 1).Current;
            updated.Should().Be(original);
            ReferenceEquals(original, updated).Should().BeFalse();
        }
    }

    [Fact]
    public async Task SameKeyChanges()
    {
        using var stub = new TransformStub();
        var people = Enumerable.Range(1, 10).Select(i => new Person("Name", i)).ToArray();

        stub.Source.AddOrUpdate(people);

        stub.Results.Messages.Count.Should().Be(1, "Should be 1 updates");
        stub.Results.Messages[0].Adds.Should().Be(1, "Should return 1 adds");
        stub.Results.Messages[0].Updates.Should().Be(9, "Should return 9 adds");
        stub.Results.Data.Count.Should().Be(1, "Should result in 1 record");

        var lastTransformed = await stub.TransformFactory(people.Last());
        var onlyItemInCache = stub.Results.Data.Items[0];

        onlyItemInCache.Should().Be(lastTransformed, "Incorrect transform result");
    }

    [Fact]
    public void Update()
    {
        const string key = "Adult1";
        var newperson = new Person(key, 50);
        var updated = new Person(key, 51);

        using var stub = new TransformStub();
        stub.Source.AddOrUpdate(newperson);
        stub.Source.AddOrUpdate(updated);

        stub.Results.Messages.Count.Should().Be(2, "Should be 2 updates");
        stub.Results.Messages[0].Adds.Should().Be(1, "Should be 1 adds");
        stub.Results.Messages[1].Updates.Should().Be(1, "Should be 1 update");
    }




    [Theory, InlineData(true), InlineData(false)]
    public void TransformOnRefresh(bool transformOnRefresh)
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var results = source.Connect()
            .AutoRefresh()
            .TransformAsync((p, key) => Task.FromResult(new PersonWithAgeGroup(p, p.Age < 18  ? "Child" : "Adult")), TransformAsyncOptions.Default with { TransformOnRefresh = transformOnRefresh }).AsAggregator();

        var person = new Person("SomeOne", 16);
        source.AddOrUpdate(person);

        results.Data.Count.Should().Be(1);
        results.Data.Lookup("SomeOne").Value.AgeGroup.Should().Be("Child");

        person.Age = 21;


        results.Data.Count.Should().Be(1);
        results.Data.Lookup("SomeOne").Value.AgeGroup.Should().Be(transformOnRefresh ? "Adult": "Child");

    }

    [Fact]
    public void TransformAsyncCancelsTokenOnUnSubscribe()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        var tcs = new TaskCompletionSource<Person>();
        using var sub = source.Connect()
            .TransformAsync(async (c, p, key, cancel) =>
            {
                using (cancel.Register(() => tcs.SetCanceled(), useSynchronizationContext: false))
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            })
            .Subscribe();

        source.AddOrUpdate(new Person());

        sub.Dispose();
        Assert.True(tcs.Task.IsCanceled);
    }


    [Theory, InlineData(10), InlineData(100)]

    public async Task WithMaxConcurrency(int maxConcurrency)
    {
        /* We need to test whether the max concurrency has any effect.

             If  maxConcurrency == 100, this test takes a little more than 100 ms
             If maxConcurrency = 10, this test takes a little more than 1s

            So it works, but how can it be tested in a scientific way ??
        */


        const int transformCount = 100;

        using var source = new SourceCache<Person, string>(p => p.Name);
        using var results = source.Connect()
            .TransformAsync(async (p, key) =>
            {
                await Task.Delay(100);

                return new PersonWithAgeGroup(p, p.Age < 18 ? "Child" : "Adult");
            },  TransformAsyncOptions.Default with { MaximumConcurrency = maxConcurrency }).AsAggregator();

        source.AddOrUpdate(Enumerable.Range(1, transformCount).Select(l => new Person("Person" + l, l)));

        await results.Data.CountChanged.Where(c => c == transformCount).Take(1);
    }

    // The forced-transform chain applies its cache updates when its async transforms finish, which
    // is off any gate unless the operator puts one there, and UnsynchronizedMerge supplies no gate
    // of its own. Rather than race and hope the window opens, this holds one transform outstanding
    // on each chain and releases them together, which is precisely the collision.
    [Fact]
    public async Task ForcedTransformCompletingAlongsideSourceUpdate_AreDeliveredSerially()
    {
        var timeout = TimeSpan.FromSeconds(30);

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
        (await registered.WaitAsync(timeout)).Should().BeTrue("the seed transform should have started");
        Release(outstanding, "Seed");
        await results.Data.CountChanged.Where(static count => count == 1).Take(1);

        // Leave one transform outstanding on each chain: the forced pass re-transforms the seed,
        // and the source update introduces a second item through the other chain.
        force.OnNext(static (_, _) => true);
        (await registered.WaitAsync(timeout)).Should().BeTrue("the forced transform should have started");

        source.AddOrUpdate(new Person("Added", 4));
        (await registered.WaitAsync(timeout)).Should().BeTrue("the source transform should have started");

        // Release both at once. Each chain applies its cache updates and emits on whichever thread
        // completed it, so this is the moment the two can collide.
        using (var barrier = new Barrier(3))
        {
            var forced = Task.Run(() =>
            {
                barrier.SignalAndWait();
                Release(outstanding, "Seed");
            });

            var added = Task.Run(() =>
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
    [Fact]
    public async Task ForcedTransformsUnderConcurrentLoad_AreDeliveredSerially()
    {
        const int writerCount = 3;
        const int seedCount = 5;

        var randomizer = new Randomizer(0x1097);
        var iterations = randomizer.Int(150, 250);
        var timeout = TimeSpan.FromMinutes(2);

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
            .Select(writer => Task.Run(() =>
            {
                var writerRandomizer = new Randomizer(0x1097 + writer + 1);
                barrier.SignalAndWait();

                for (var i = 0; i < iterations; i++)
                {
                    source.AddOrUpdate(new Person(writerRandomizer.ArrayElement(names), writerRandomizer.Int(1, 80)));
                }
            }))
            .ToArray();

        var forcer = Task.Run(() =>
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

        (await Task.WhenAny(terminal, Task.Delay(timeout))).Should().BeSameAs(terminal, "the pipeline should drain rather than deadlock");

        var lastNotification = await terminal;

        lastNotification.Exception.Should().BeNull("deliveries must neither overlap nor carry inconsistent change sets, however the writers interleave");
        lastNotification.Kind.Should().Be(NotificationKind.OnCompleted, "the sequence should end by completing, not by faulting");
    }

    private class TransformStub : IDisposable
    {
        public TransformStub()
        {
            TransformFactory = (p) =>
            {
                var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().TransformAsync(TransformFactory));
        }

        public TransformStub(Func<Person, PersonWithGender> factory)
        {
            TransformFactory = (p) =>
            {
                var result = factory(p);
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(Source.Connect().TransformAsync(TransformFactory));
        }

        public TransformStub(IObservable<Unit> retransformer)
        {
            TransformFactory = (p) =>
            {
                var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(
                Source.Connect().TransformAsync(
                    TransformFactory,
                    retransformer.Select(
                        x =>
                        {
                            Func<Person, string, bool> transformer = (p, key) => true;
                            return transformer;
                        })));
        }

        public TransformStub(IObservable<Func<Person, bool>> retransformer)
        {
            TransformFactory = (p) =>
            {
                var result = new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");
                return Task.FromResult(result);
            };

            Results = new ChangeSetAggregator<PersonWithGender, string>(
                Source.Connect().TransformAsync(
                    TransformFactory,
                    retransformer.Select(
                        selector =>
                        {
                            Func<Person, string, bool> transformed = (p, key) => selector(p);
                            return transformed;
                        })));
        }

        public ChangeSetAggregator<PersonWithGender, string> Results { get; }

        public ISourceCache<Person, string> Source { get; } = new SourceCache<Person, string>(p => p.Name);

        public Func<Person, Task<PersonWithGender>> TransformFactory { get; }

        public void Dispose()
        {
            Source.Dispose();
            Results.Dispose();
        }
    }
}
