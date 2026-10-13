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

namespace DynamicData.Tests.List;

/// <summary>
/// Serialization coverage for the list operators that take more than one input. A single input is
/// serialized by whatever feeds it, so these are the operators where two producers can reach the
/// same observer at once, and where a missing gate shows up as overlapping delivery rather than as
/// a wrong value. Every pipeline is checked both for overlap and for change set integrity, since
/// list change sets carry indices and a torn read usually surfaces as a structurally invalid change
/// set rather than as a visible race.
/// </summary>
public class MultiSourceSerializationFixture
{
    private const int Seed = 0x1147;

    private static readonly TimeSpan DrainTimeout = TimeSpan.FromMinutes(2);

    [Fact]
    public async Task OrDeliversSeriallyWhileBothSourcesAreWritten()
    {
        using var left = new SourceList<Person>();
        using var right = new SourceList<Person>();

        await RunAsync(
            left.Connect().Or(right.Connect()),
            writers:
            [
                randomizer => left.Add(new Person("L" + randomizer.Int(1, 500), randomizer.Int(1, 80))),
                randomizer => right.Add(new Person("R" + randomizer.Int(1, 500), randomizer.Int(1, 80))),
                randomizer =>
                {
                    var items = left.Items.ToArray();
                    if (items.Length > 0)
                    {
                        left.Remove(items[randomizer.Int(0, items.Length - 1)]);
                    }
                },
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
        using var source = new SourceList<Person>();
        using var comparers = new Subject<IComparer<Person>>();
        using var resort = new Subject<Unit>();

        var byAge = SortExpressionComparer<Person>.Ascending(p => p.Age);
        var byName = SortExpressionComparer<Person>.Ascending(p => p.Name);

        await RunAsync(
            source.Connect().Sort(comparers, resetThreshold: 25, resort: resort),
            writers:
            [
                randomizer => source.Add(new Person("P" + randomizer.Int(1, 500), randomizer.Int(1, 80))),
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
        using var source = new SourceList<Person>();
        using var regroup = new Subject<Unit>();

        await RunAsync(
            source.Connect().GroupOn(p => p.Age % 5, regroup).Transform(group => new Person(group.GroupKey.ToString(), group.GroupKey)),
            writers:
            [
                randomizer => source.Add(new Person("P" + randomizer.Int(1, 500), randomizer.Int(1, 80))),
                randomizer =>
                {
                    var items = source.Items.ToArray();
                    if (items.Length > 0)
                    {
                        source.Remove(items[randomizer.Int(0, items.Length - 1)]);
                    }
                },
                _ => regroup.OnNext(Unit.Default),
            ],
            complete: () =>
            {
                regroup.OnCompleted();
                source.Dispose();
            });
    }

    [Fact]
    public async Task BufferIfDeliversSeriallyWhilePausingConcurrently()
    {
        using var source = new SourceList<Person>();
        using var pause = new Subject<bool>();

        await RunAsync(
            source.Connect().BufferIf(pause),
            writers:
            [
                randomizer => source.Add(new Person("P" + randomizer.Int(1, 500), randomizer.Int(1, 80))),
                randomizer =>
                {
                    var items = source.Items.ToArray();
                    if (items.Length > 0)
                    {
                        source.Remove(items[randomizer.Int(0, items.Length - 1)]);
                    }
                },
                randomizer => pause.OnNext(randomizer.Bool()),
            ],
            complete: () =>
            {
                // Leave the buffer open, so anything held back has to be flushed on completion.
                pause.OnNext(false);
                pause.OnCompleted();
                source.Dispose();
            });
    }

    [Fact]
    public async Task MergeManyChangeSetsDeliversSeriallyWhileChildrenAreWritten()
    {
        using var owners = new SourceList<AnimalOwner>();
        var created = new List<AnimalOwner>();

        for (var i = 0; i < 5; i++)
        {
            var owner = new AnimalOwner("Owner" + i);
            created.Add(owner);
            owners.Add(owner);
        }

        var nextName = 0;

        await RunAsync(
            owners.Connect().MergeManyChangeSets(o => o.Animals.Connect()).Transform(a => new Person(a.Name, a.Name.Length)),
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
                // The merged result completes only once the parent and every child have.
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
        IObservable<IChangeSet<Person>> pipeline,
        Action<Randomizer>[] writers,
        Action complete,
        Action? seedAction = null)
    {
        var randomizer = new Randomizer(Seed);
        var iterations = randomizer.Int(150, 250);

        var published = pipeline
            .ValidateSynchronization()
            .ValidateChangeSets()

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
