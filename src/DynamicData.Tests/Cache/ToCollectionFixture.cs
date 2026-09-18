using System;
using System.Collections.Generic;
using System.Linq;


using DynamicData.Tests.Utilities;

namespace DynamicData.Tests.Cache;

public class ToCollectionFixture
{

    public record TestItem
    {
        public static int SelectId(TestItem item)
            => item.Id;

        public required int Id { get; init; }

        public int Version { get; init; }
    }

    [Test]
    public async Task WhenChangesAreMade_ResultMatchesSourceAndPriorResultsAreNotMutated()
    {
        // Setup
        using var source = new SourceCache<TestItem, int>(TestItem.SelectId);


        // UUT Initialization
        var priorResults = new List<IReadOnlyCollection<TestItem>>();

        using var subscription = source.Connect()
            .ToCollection()
            .RecordValues(out var results);

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.HasCompleted).IsFalse();


        // UUT Action (add items)
        source.AddOrUpdate(new[]
        {
            new TestItem() { Id = 1 },
            new TestItem() { Id = 2 },
            new TestItem() { Id = 3 }
        });

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.RecordedValues.Count - priorResults.Count).IsEqualTo(1);
        await Assert.That(results.RecordedValues[^1]).IsEquivalentTo(source.Items);
        await Assert.That(results.HasCompleted).IsFalse();

        foreach (var (result, priorResult) in results.RecordedValues.Zip(priorResults))
            await Assert.That(result).IsEquivalentTo(priorResult);

        priorResults.Add(results.RecordedValues[^1].ToArray());


        // UUT Action (replace items)
        source.AddOrUpdate(new[]
        {
            new TestItem() { Id = 1, Version = 1 },
            new TestItem() { Id = 2, Version = 1 },
            new TestItem() { Id = 3, Version = 1 }
        });

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.RecordedValues.Count - priorResults.Count).IsEqualTo(1);
        await Assert.That(results.RecordedValues[^1]).IsEquivalentTo(source.Items);
        await Assert.That(results.HasCompleted).IsFalse();

        foreach (var (result, priorResult) in results.RecordedValues.Zip(priorResults))
            await Assert.That(result).IsEquivalentTo(priorResult);

        priorResults.Add(results.RecordedValues[^1].ToArray());


        // UUT Action (remove items)
        source.RemoveKeys(source.Keys.ToArray());

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.RecordedValues.Count - priorResults.Count).IsEqualTo(1);
        await Assert.That(results.RecordedValues[^1]).IsEquivalentTo(source.Items);
        await Assert.That(results.HasCompleted).IsFalse();

        foreach (var (result, priorResult) in results.RecordedValues.Zip(priorResults))
            await Assert.That(result).IsEquivalentTo(priorResult);

        priorResults.Add(results.RecordedValues[^1].ToArray());
    }

    [Test]
    [Arguments(StreamCompletionStrategy.Asynchronous)]
    [Arguments(StreamCompletionStrategy.Immediate)]
    public async Task WhenSourceCompletes_CompletionPropagates(StreamCompletionStrategy completionStrategy)
    {
        // Setup
        using var source = new TestSourceCache<TestItem, int>(TestItem.SelectId);


        // UUT Initialization & Action
        if (completionStrategy is StreamCompletionStrategy.Immediate)
            source.Complete();

        using var subscription = source.Connect(suppressEmptyChangeSets: false)
            .ToCollection()
            .RecordValues(out var results);

        if (completionStrategy is StreamCompletionStrategy.Asynchronous)
            source.Complete();

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.RecordedValues.Count).IsEqualTo(1);
        await Assert.That(results.RecordedValues[^1]).IsEmpty();
        await Assert.That(results.HasCompleted).IsTrue();
    }

    [Test]
    [Arguments(StreamCompletionStrategy.Asynchronous)]
    [Arguments(StreamCompletionStrategy.Immediate)]
    public async Task WhenSourceFails_ErrorPropagates(StreamCompletionStrategy completionStrategy)
    {
        // Setup
        using var source = new TestSourceCache<TestItem, int>(TestItem.SelectId);


        // UUT Initialization & Action
        var error = new Exception("Test");

        if (completionStrategy is StreamCompletionStrategy.Immediate)
            source.SetError(error);

        using var subscription = source.Connect()
            .ToCollection()
            .RecordValues(out var results);

        if (completionStrategy is StreamCompletionStrategy.Asynchronous)
            source.SetError(error);

        await Assert.That(results.Error).IsSameReferenceAs(error);
    }

    [Test]
    public async Task WhenSourceIsNull_ThrowsException()
    {
        // UUT Action
        var exception = await Assert.That(() => ObservableCacheEx.ToCollection<int, int>(null!)).Throws<ArgumentNullException>();
        await Assert.That(exception.ParamName).IsEqualTo("source");
    }

    [Test]
    public async Task WhenSubscriptionIsDisposed_SubscriptionDisposalPropagates()
    {
        // Setup
        using var source = new ReactiveUI.Primitives.Signals.Signal<IChangeSet<Item, int>>();


        // UUT Initialization
        using var subscription = source
            .ToCollection()
            .RecordValues(out var results);

        await Assert.That(results.Error).IsNull();
        await Assert.That(results.HasCompleted).IsFalse();


        // UUT Action
        subscription.Dispose();

        await Assert.That(source.HasObservers).IsFalse();
    }

}
