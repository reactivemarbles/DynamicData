---
applyTo: "src/DynamicData.Tests/List/**/*.cs"
---
# Testing List Operators

This covers testing patterns specific to **list** (`IChangeSet<T>`) operators. For general testing philosophy and requirements, see the main `copilot-instructions.md`.

## Observation Patterns

### ChangeSetAggregator<T> (primary pattern)

The list `ChangeSetAggregator<T>` (single type parameter, no key) captures every list changeset. Defined in `List/Tests/`.

```csharp
using var source = new SourceList<Person>();
using var results = new ChangeSetAggregator<Person>(
    source.Connect().Filter(p => p.Age >= 18));

source.Add(new Person("Adult", 25));
source.Add(new Person("Child", 10));

results.Data.Count.Should().Be(1);
results.Messages.Count.Should().Be(1);
results.Messages[0].Adds.Should().Be(1);
results.Data.Items[0].Name.Should().Be("Adult");
```

**Properties:**
- `Data` — `IObservableList<T>` materialized view
- `Messages` — `IList<IChangeSet<T>>` all changesets
- `Exception` — captured error (note: `Exception`, not `Error` like cache)
- `IsCompleted` — completion flag

### RecordListItems (modern)

`RecordListItems` creates a `ListItemRecordingObserver` — the list parallel to `RecordCacheItems`.

```csharp
using var source = new TestSourceList<Item>();

using var subscription = source.Connect()
    .Filter(item => item.IsIncluded)
    .ValidateSynchronization()
    .ValidateChangeSets()
    .RecordListItems(out var results);

source.Add(new Item { IsIncluded = true });
source.Add(new Item { IsIncluded = false });

results.RecordedItems.Should().HaveCount(1);
results.Error.Should().BeNull();
```

## Asserting List Changeset Contents

List changesets differ from cache — changes can be item-level or range-level:

```csharp
// Assert changeset structure
results.Messages[0].Adds.Should().Be(5);
results.Messages[0].Removes.Should().Be(0);
results.Messages[0].Replaced.Should().Be(0);
results.Messages[0].Refreshes.Should().Be(0);

// Item-level change
var change = results.Messages[0].First();
change.Reason.Should().Be(ListChangeReason.Add);
change.Item.Current.Should().Be(expectedItem);
change.Item.CurrentIndex.Should().BeGreaterOrEqualTo(0);

// Range-level change (AddRange, RemoveRange, Clear)
var rangeChange = results.Messages[0].First(c => c.Reason == ListChangeReason.AddRange);
rangeChange.Range.Should().HaveCount(10);
rangeChange.Range.Index.Should().Be(0);

// Replace (list equivalent of Update)
var replace = results.Messages[1].First();
replace.Reason.Should().Be(ListChangeReason.Replace);
replace.Item.Previous.HasValue.Should().BeTrue();
replace.Item.Previous.Value.Should().Be(oldItem);

// Assert materialized list state
results.Data.Count.Should().Be(5);
results.Data.Items.Should().BeEquivalentTo(expectedItems);
```

## Key Differences from Cache Testing

| Aspect | Cache | List |
|--------|-------|------|
| **Aggregator type** | `ChangeSetAggregator<T, TKey>` | `ChangeSetAggregator<T>` |
| **Data property** | `IObservableCache<T,K>` (has `Lookup(key)`) | `IObservableList<T>` (index-based) |
| **Add assertion** | `change.Key` available | `change.Item.CurrentIndex` available |
| **Update vs Replace** | `ChangeReason.Update` with `Previous` | `ListChangeReason.Replace` with `Previous` |
| **Batch add** | Each item is a separate Add change | `AddRange` is a single range change |
| **Clear** | Individual Remove per item | Single `Clear` range change |
| **Fixture setup** | `new SourceCache<T,K>(keySelector)` | `new SourceList<T>()` |
| **Connect** | `cache.Connect()` | `list.Connect()` |
| **Mutate** | `cache.Edit(u => u.AddOrUpdate(...))` | `list.Edit(l => l.Add(...))` |

## The List Fixture Pattern

List test fixtures follow the same pattern as cache but with `SourceList`:

```csharp
public class TransformFixture : IDisposable
{
    private readonly ChangeSetAggregator<PersonWithGender> _results;
    private readonly ISourceList<Person> _source;
    private readonly Func<Person, PersonWithGender> _transformFactory =
        p => new PersonWithGender(p, p.Age % 2 == 0 ? "M" : "F");

    public TransformFixture()
    {
        _source = new SourceList<Person>();
        _results = new ChangeSetAggregator<PersonWithGender>(
            _source.Connect().Transform(_transformFactory));
    }

    [Fact]
    public void Add()
    {
        var person = new Person("Adult1", 50);
        _source.Add(person);

        _results.Messages.Count.Should().Be(1);
        _results.Data.Count.Should().Be(1);
        _results.Data.Items[0].Should().Be(_transformFactory(person));
    }

    public void Dispose()
    {
        _source.Dispose();
        _results.Dispose();
    }
}
```

## List Stress Tests

List stress tests are similar to cache but use `SourceList` APIs:

```csharp
[Fact]
public async Task MultiThreadedStressTest()
{
    const int writerThreads = 8;
    const int itemsPerThread = 500;

    using var source = new SourceList<Person>();
    using var results = new ChangeSetAggregator<Person>(
        source.Connect().Filter(p => p.Age >= 18));

    using var barrier = new Barrier(writerThreads + 1);

    var tasks = Enumerable.Range(0, writerThreads).Select(threadId => Task.Run(() =>
    {
        barrier.SignalAndWait();
        for (var i = 0; i < itemsPerThread; i++)
            source.Add(new Person($"Thread{threadId}_Item{i}", 20 + i));
    })).ToArray();

    barrier.SignalAndWait();
    await Task.WhenAll(tasks);

    // List allows duplicates — total is threads × items
    results.Data.Count.Should().Be(writerThreads * itemsPerThread);
}
```

## What Every List Operator Test Must Cover

1. **Single item**: Add, Remove, Replace, Refresh, Move individually
2. **Range operations**: AddRange, RemoveRange, Clear
3. **Index correctness**: Verify CurrentIndex and PreviousIndex are correct
4. **Empty changeset**: Operator doesn't emit empty changesets
5. **Error propagation**: Source `OnError` propagates
6. **Completion propagation**: Source `OnCompleted` propagates
7. **Disposal**: Disposing unsubscribes from all sources
8. **Edge cases**: Empty list, single item, boundary indices

For operators with dynamic parameters:

9. **Parameter changes**: Re-evaluates correctly
10. **Parameter completion/error**: Proper handling