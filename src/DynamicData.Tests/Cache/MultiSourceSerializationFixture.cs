using System;
using System.Collections.Generic;
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

using Randomizer = Bogus.Randomizer;

namespace DynamicData.Tests.Cache;

/// <summary>
/// Serialization coverage for the cache operators that take more than one input. A single input is
/// serialized by whatever feeds it, so these are the operators where two producers can reach the
/// same observer at once, and where a missing gate shows up as overlapping delivery rather than as
/// a wrong value. Every pipeline is checked both for overlap and for change set integrity, since a
/// torn read of shared operator state usually surfaces as a structurally invalid change set rather
/// than as a visible race.
/// </summary>
public class MultiSourceSerializationFixture
{
    private const int Seed = 0x1146;

    private static readonly TimeSpan DrainTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task OrDeliversSeriallyWhileBothSourcesAreWritten()
    {
        using var left = new SourceCache<Person, string>(p => p.Name);
        using var right = new SourceCache<Person, string>(p => p.Name);

        await RunAsync(
            left.Connect().Or(right.Connect()),
            writers:
            [
                randomizer => left.AddOrUpdate(new Person("L" + randomizer.Int(1, 20), randomizer.Int(1, 80))),
                randomizer => right.AddOrUpdate(new Person("R" + randomizer.Int(1, 20), randomizer.Int(1, 80))),
                randomizer => left.Remove("L" + randomizer.Int(1, 20)),
            ],
            complete: () =>
            {
                left.Dispose();
                right.Dispose();
            });
    }

    [Fact]
    public async Task SortDeliversSeriallyWhileTheComparerChanges()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var comparers = new Subject<IComparer<Person>>();
        using var resort = new Subject<Unit>();

        var byAge = SortExpressionComparer<Person>.Ascending(p => p.Age);
        var byName = SortExpressionComparer<Person>.Ascending(p => p.Name);

        await RunAsync(
            source.Connect().Sort(comparers, resort).Transform(static person => person),
            writers:
            [
                randomizer => source.AddOrUpdate(new Person("P" + randomizer.Int(1, 20), randomizer.Int(1, 80))),
                randomizer => comparers.OnNext(randomizer.Bool() ? byAge : byName),
                _ => resort.OnNext(Unit.Default),
            ],
            complete: () =>
            {
                comparers.OnCompleted();
                resort.OnCompleted();
                source.Dispose();
            },
            seedAction: () => comparers.OnNext(byAge));
    }

    [Fact]
    public async Task GroupOnDeliversSeriallyWhileRegroupingConcurrently()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var regroup = new Subject<Unit>();

        await RunAsync(
            source.Connect().Group(p => p.Age % 5, regroup).Transform(group => new Person(group.Key.ToString(), group.Key)).ChangeKey(static person => person.Name),
            writers:
            [
                randomizer => source.AddOrUpdate(new Person("P" + randomizer.Int(1, 20), randomizer.Int(1, 80))),
                randomizer => source.Remove("P" + randomizer.Int(1, 20)),
                _ => regroup.OnNext(Unit.Default),
            ],
            complete: () =>
            {
                regroup.OnCompleted();
                source.Dispose();
            });
    }

    [Fact]
    public async Task BatchIfDeliversSeriallyWhilePausingConcurrently()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var pause = new Subject<bool>();

        await RunAsync(
            source.Connect().BatchIf(pause, initialPauseState: false, timeOut: null),
            writers:
            [
                randomizer => source.AddOrUpdate(new Person("P" + randomizer.Int(1, 20), randomizer.Int(1, 80))),
                randomizer => source.Remove("P" + randomizer.Int(1, 20)),
                randomizer => pause.OnNext(randomizer.Bool()),
            ],
            complete: () =>
            {
                // Leave the batch open, so anything held back has to be flushed on completion.
                pause.OnNext(false);
                pause.OnCompleted();
                source.Dispose();
            });
    }

    [Fact]
    public async Task TransformWithForcedTransformDeliversSeriallyWhileForcing()
    {
        using var source = new SourceCache<Person, string>(p => p.Name);
        using var force = new Subject<Func<Person, bool>>();

        await RunAsync(
            source.Connect().Transform(p => new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F"), forceTransform: force).Transform(p => new Person(p.Name, p.Age)),
            writers:
            [
                randomizer => source.AddOrUpdate(new Person("P" + randomizer.Int(1, 20), randomizer.Int(1, 80))),
                randomizer => source.Remove("P" + randomizer.Int(1, 20)),
                _ => force.OnNext(static _ => true),
            ],
            complete: () =>
            {
                force.OnCompleted();
                source.Dispose();
            });
    }

    [Fact]
    public async Task DynamicOrDeliversSeriallyWhileBothSourcesAreWritten()
    {
        using var left = new SourceCache<Person, string>(p => p.Name);
        using var right = new SourceCache<Person, string>(p => p.Name);
        using var sources = new SourceList<IObservable<IChangeSet<Person, string>>>();

        sources.Add(left.Connect());
        sources.Add(right.Connect());

        await RunAsync(
            sources.Or(),
            writers:
            [
                randomizer => left.AddOrUpdate(new Person("L" + randomizer.Int(1, 20), randomizer.Int(1, 80))),
                randomizer => right.AddOrUpdate(new Person("R" + randomizer.Int(1, 20), randomizer.Int(1, 80))),
                randomizer => left.Remove("L" + randomizer.Int(1, 20)),
            ],
            complete: () =>
            {
                // Children first, then the list of sources: the combined result ends only once the
                // sources themselves and the list feeding them have all finished.
                left.Dispose();
                right.Dispose();
                sources.Dispose();
            });
    }

    [Fact]
    public async Task MergeManyChangeSetsDeliversSeriallyWhileChildrenAreWritten()
    {
        using var owners = new SourceCache<AnimalOwner, Guid>(o => o.Id);
        var created = new List<AnimalOwner>();

        for (var i = 0; i < 5; i++)
        {
            var owner = new AnimalOwner("Owner" + i);
            created.Add(owner);
            owners.AddOrUpdate(owner);
        }

        // The child collections are lists, which allow duplicates, so names are handed out from a
        // counter. Otherwise keying the merged result collides for reasons that have nothing to do
        // with how the writers interleave.
        var nextName = 0;

        await RunAsync(
            owners.Connect().MergeManyChangeSets(o => o.Animals.Connect()).Transform(a => new Person(a.Name, a.Name.Length)).AddKey(static person => person.Name),
            writers:
            [
                randomizer => created[randomizer.Int(0, created.Count - 1)].Animals.Add(new Animal("A" + Interlocked.Increment(ref nextName), "Type", AnimalFamily.Mammal)),
                randomizer =>
                {
                    var owner = created[randomizer.Int(0, created.Count - 1)];
                    var animals = owner.Animals.Items.ToArray();
                    if (animals.Length > 0)
                    {
                        owner.Animals.Remove(animals[randomizer.Int(0, animals.Length - 1)]);
                    }
                },
                randomizer => created[randomizer.Int(0, created.Count - 1)].Animals.Add(new Animal("B" + Interlocked.Increment(ref nextName), "Type", AnimalFamily.Bird)),
            ],
            complete: () =>
            {
                // The merged result completes only once the parent and every child have, so the child
                // lists have to be disposed as well as the owner cache.
                foreach (var owner in created)
                {
                    owner.Dispose();
                }

                owners.Dispose();
            });
    }

    /// <summary>
    /// Drives every writer from its own thread against a shared barrier, then waits for the pipeline
    /// to drain and asserts that it ended cleanly. A serialization failure surfaces as an
    /// <c>UnsynchronizedNotificationException</c> on the terminal notification rather than as a
    /// wrong count, so the assertion is on how the sequence ended.
    /// </summary>
    private static async Task RunAsync(
        IObservable<IChangeSet<Person, string>> pipeline,
        Action<Randomizer>[] writers,
        Action complete,
        Action? seedAction = null)
    {
        var randomizer = new Randomizer(Seed);
        var iterations = randomizer.Int(150, 250);

        var published = pipeline
            .ValidateSynchronization()
            .ValidateChangeSets(static person => person.Name)

            // Holding each notification briefly makes an overlapping delivery observable instead of
            // something that has to be caught inside a window of a few instructions.
            .Do(static _ => Thread.SpinWait(500))
            .Publish();

        var terminal = published.Materialize().LastAsync().ToTask();
        using var subscription = published.Connect();

        seedAction?.Invoke();

        using var barrier = new Barrier(writers.Length + 1);

        var tasks = writers.Select((writer, index) => Task.Run(() =>
        {
            var threadRandomizer = new Randomizer(Seed + index + 1);
            barrier.SignalAndWait();

            for (var i = 0; i < iterations; i++)
            {
                writer(threadRandomizer);
            }
        })).ToArray();

        barrier.SignalAndWait();
        await Task.WhenAll(tasks);

        complete();

        (await Task.WhenAny(terminal, Task.Delay(DrainTimeout))).Should().BeSameAs(terminal, "the pipeline should drain rather than deadlock");

        var lastNotification = await terminal;

        lastNotification.Exception.Should().BeNull("notifications must not overlap, and every change set must be structurally valid, however the writers interleave");
    }
}

