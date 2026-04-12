---
applyTo: "src/DynamicData.Tests/Cache/**/*.cs"
---
# Testing Cache Operators

This covers testing patterns specific to **cache** (`IChangeSet<TObject, TKey>`) operators. For general testing philosophy and requirements, see the main `copilot-instructions.md`.

## Observation Patterns

Cache tests use two distinct patterns for capturing pipeline output. **Know both — use the right one.**

### Pattern 1: ChangeSetAggregator (legacy, still widely used)

`AsAggregator()` materializes the stream into a `ChangeSetAggregator` that captures every changeset. Defined in the library assembly under `Cache/Tests/`.

```csharp
using var source = new SourceCache<Person, string>(p => p.Key);
using var results = source.Connect()
    .Filter(p => p.Age >= 18)
    .AsAggregator();

source.AddOrUpdate(new Person("Adult", 25));
source.AddOrUpdate(new Person("Child", 10));

results.Data.Count.Should().Be(1);
results.Messages.Count.Should().Be(1, "child was filtered, only 1 changeset emitted");
results.Messages[0].Adds.Should().Be(1);
results.Data.Items[0].Name.Should().Be("Adult");
```

**ChangeSetAggregator properties:**
- `Data` — `IObservableCache<T,K>` materialized view of current state
- `Messages` — `IList<IChangeSet<T,K>>` every changeset received
- `Summary` — `ChangeSummary` aggregated statistics
- `Error` — captured `OnError` exception (if any)
- `IsCompleted` — whether `OnCompleted` was received

**Specialized aggregator variants:**
- **`SortedChangeSetAggregator`** — for `.Sort()`, exposes `Messages[i].SortedItems`
- **`PagedChangeSetAggregator`** — for `.Page()`
- **`VirtualChangeSetAggregator`** — for `.Virtualise()`
- **`GroupChangeSetAggregator`** — for `.Group()`
- **`DistinctChangeSetAggregator`** — for `.DistinctValues()`

### Pattern 2: RecordCacheItems (modern, preferred for new tests)

`RecordCacheItems` creates a `CacheItemRecordingObserver` with keyed + sorted index tracking. Pair it with `.ValidateSynchronization()` and `.ValidateChangeSets()`.

```csharp
using var source = new TestSourceCache<Item, int>(Item.SelectId);

using var subscription = source.Connect()
    .Filter(Item.FilterByIsIncluded)
    .ValidateSynchronization()         // detects concurrent OnNext (Rx violation!)
    .ValidateChangeSets(Item.SelectId) // validates changeset structural integrity
    .RecordCacheItems(out var results);

source.AddOrUpdate(new Item(1) { IsIncluded = true });
source.AddOrUpdate(new Item(2) { IsIncluded = false });

results.RecordedItemsByKey.Should().ContainKey(1);
results.RecordedItemsByKey.Should().NotContainKey(2);
results.RecordedChangeSets.Should().HaveCount(1);
results.Error.Should().BeNull();
results.HasCompleted.Should().BeFalse();
```

**CacheItemRecordingObserver properties:**
- `RecordedItemsByKey` — `IReadOnlyDictionary<TKey, TObject>` current items by key
- `RecordedItemsSorted` — `IReadOnlyList<TObject>` items with sorted index tracking
- `RecordedChangeSets` — `IReadOnlyList<IChangeSet<T,K>>` all changesets
- `Error` — captured exception
- `HasCompleted` — completion flag

**When to use which:**
- **New tests**: Prefer `RecordCacheItems` + `ValidateSynchronization` + `ValidateChangeSets`.
- **Existing tests**: Don't refactor from `AsAggregator` unless asked to do so.
- **Sort/Page/Virtual tests**: The specialized aggregators have no `RecordCacheItems` equivalent yet — use them.

## Asserting Cache Changeset Contents

Cache changesets carry rich metadata — **use it** for precise assertions:

```csharp
// Assert changeset structure (counts by reason)
results.Messages[0].Adds.Should().Be(5);
results.Messages[0].Updates.Should().Be(0);
results.Messages[0].Removes.Should().Be(0);
results.Messages[0].Refreshes.Should().Be(0);

// Assert individual changes
var change = results.Messages[0].First();
change.Reason.Should().Be(ChangeReason.Add);
change.Key.Should().Be("key1");
change.Current.Should().Be(expectedItem);

// For updates, Previous is populated
var update = results.Messages[1].First();
update.Reason.Should().Be(ChangeReason.Update);
update.Previous.HasValue.Should().BeTrue();
update.Previous.Value.Should().Be(previousItem);

// Assert materialized cache state
results.Data.Count.Should().Be(5);
results.Data.Items.Should().BeEquivalentTo(expectedItems);
results.Data.Lookup("key1").HasValue.Should().BeTrue();
```

## Testing Completion and Error Propagation

Use `TestSourceCache<T,K>` to inject terminal events:

```csharp
[Theory]
[InlineData(CompletionStrategy.Asynchronous)]  // complete after subscription
[InlineData(CompletionStrategy.Immediate)]      // complete before subscription
public void SourceCompletes_CompletionPropagates(CompletionStrategy completionStrategy)
{
    using var source = new TestSourceCache<Item, int>(Item.SelectId);

    if (completionStrategy is CompletionStrategy.Immediate)
        source.Complete();

    using var subscription = source.Connect()
        .Filter(Item.FilterByIsIncluded)
        .ValidateSynchronization()
        .RecordCacheItems(out var results);

    if (completionStrategy is CompletionStrategy.Asynchronous)
        source.Complete();

    results.HasCompleted.Should().BeTrue();
    results.Error.Should().BeNull();
}

[Fact]
public void SourceErrors_ErrorPropagates()
{
    using var source = new TestSourceCache<Item, int>(Item.SelectId);
    var testError = new Exception("Test error");

    using var subscription = source.Connect()
        .Transform(x => new ViewModel(x))
        .RecordCacheItems(out var results);

    source.SetError(testError);
    results.Error.Should().BeSameAs(testError);
}
```

## The Stub/Fixture Pattern

Many cache tests use an inner helper class that sets up source, pipeline, and aggregator:

```csharp
private sealed class TransformStub : IDisposable
{
    public SourceCache<Person, string> Source { get; } = new(p => p.Key);
    public Func<Person, PersonWithGender> TransformFactory { get; }
        = p => new PersonWithGender(p, p.Gender == "M" ? "Male" : "Female");
    public ChangeSetAggregator<PersonWithGender, string> Results { get; }

    public TransformStub(IObservable<Unit>? forceTransform = null)
    {
        Results = Source.Connect()
            .Transform(TransformFactory, forceTransform: forceTransform)
            .AsAggregator();
    }

    public void Dispose()
    {
        Source.Dispose();
        Results.Dispose();
    }
}
```

This keeps individual `[Fact]` methods short and focused on the scenario under test.

## Cache Stress Tests

Multi-threaded stress tests prove cache operators are thread-safe:

```csharp
[Fact]
public async Task MultiThreadedStressTest()
{
    const int writerThreads = 8;
    const int itemsPerThread = 500;

    using var source = new SourceCache<Person, string>(p => p.Key);
    using var results = source.Connect()
        .Filter(p => p.Age >= 18)
        .Sort(SortExpressionComparer<Person>.Ascending(p => p.Name))
        .AsAggregator();

    using var barrier = new Barrier(writerThreads + 1);

    var tasks = Enumerable.Range(0, writerThreads).Select(threadId => Task.Run(() =>
    {
        barrier.SignalAndWait();
        for (var i = 0; i < itemsPerThread; i++)
            source.AddOrUpdate(new Person($"Thread{threadId}_Item{i}", 20 + i));
    })).ToArray();

    barrier.SignalAndWait();
    await Task.WhenAll(tasks);

    results.Data.Count.Should().Be(writerThreads * itemsPerThread);
}
```

## What Every Cache Operator Test Must Cover

1. **Single item**: Add, Update, Remove, Refresh individually
2. **Batch**: Multiple items in a single `Edit()` call
3. **Empty changeset**: Operator doesn't emit empty changesets
4. **Error propagation**: Source `OnError` propagates
5. **Completion propagation**: Source `OnCompleted` propagates
6. **Disposal**: Disposing unsubscribes from all sources
7. **Edge cases**: Duplicate keys, boundary values

For operators with dynamic parameters:

8. **Parameter changes**: Predicate/comparer change re-evaluates correctly
9. **Parameter completion**: What happens when parameter observable completes
10. **Parameter error**: What happens when parameter observable errors
