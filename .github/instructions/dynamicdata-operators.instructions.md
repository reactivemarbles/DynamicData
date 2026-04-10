---
applyTo: "src/DynamicData/**/*.cs"
---
# DynamicData Cache Operators Guide

## How Operators Work

Every cache operator:
1. Receives `IObservable<IChangeSet<TObject, TKey>>` — a stream of incremental changes
2. Processes each changeset: handles Add, Update, Remove, and Refresh reasons
3. Emits a new `IChangeSet` downstream with the transformed/filtered/sorted result
4. Maintains internal state for **incremental** processing — no full re-evaluation per changeset

A changeset is a batch of changes. A single `SourceCache.Edit()` call produces one changeset, regardless of how many items were added/updated/removed inside it.

## Changeset and Change Reasons

```csharp
public interface IChangeSet<TObject, TKey> : IEnumerable<Change<TObject, TKey>>
{
    int Adds { get; }
    int Updates { get; }
    int Removes { get; }
    int Refreshes { get; }
    int Count { get; }  // total changes in this batch
}

public enum ChangeReason
{
    Add,      // new item with a previously-unseen key
    Update,   // existing key, new value (Previous is available)
    Remove,   // item removed by key
    Refresh,  // item unchanged, but downstream should re-evaluate (e.g. property changed)
    Moved,    // item moved position (list only)
}
```

## Operator Categories

### Filtering

```csharp
// Static predicate — re-evaluated per changeset item
cache.Connect()
    .Filter(animal => animal.Family == AnimalFamily.Mammal)

// Dynamic predicate — re-evaluates ALL items when predicate observable fires
var predicate = new BehaviorSubject<Func<Animal, bool>>(a => a.Family == AnimalFamily.Mammal);
cache.Connect()
    .Filter(predicate)

// Per-item observable filter — each item's visibility is controlled by its own observable
cache.Connect()
    .FilterOnObservable(animal => animal.WhenPropertyChanged(a => a.IsVisible)
        .Select(change => change.Value))

// By change reason
cache.Connect()
    .WhereReasonsAre(ChangeReason.Add, ChangeReason.Remove)
```

### Transformation

```csharp
// 1:1 transform — creates a parallel cache of ViewModels
cache.Connect()
    .Transform(model => new ViewModel(model))

// Transform with error handling — errors go to callback, not OnError
cache.Connect()
    .TransformSafe(model => Parse(model), error => _logger.Error(error))

// Async transform — for I/O-bound operations
cache.Connect()
    .TransformAsync(async model => await _api.EnrichAsync(model))

// 1:N flatten — each source item produces multiple destination items
cache.Connect()
    .TransformMany(
        owner => owner.Pets,        // the collection to flatten
        pet => pet.Id)              // key selector for destination items

// Transform via per-item observable — reactive per-item projection
cache.Connect()
    .TransformOnObservable(item => item.LatestState.Select(state => new ViewModel(item, state)))

// Re-key items
cache.Connect()
    .ChangeKey(animal => animal.Name)
```

### Sorting

```csharp
// Static comparer
cache.Connect()
    .Sort(SortExpressionComparer<Animal>.Ascending(a => a.Name))

// Dynamic comparer — re-sorts when the comparer observable fires
var comparer = new BehaviorSubject<IComparer<Animal>>(
    SortExpressionComparer<Animal>.Ascending(a => a.Name));
cache.Connect()
    .Sort(comparer)

// Sort and bind directly to a UI list
var boundList = new List<Animal>();
cache.Connect()
    .SortAndBind(boundList, SortExpressionComparer<Animal>.Ascending(a => a.Name))
    .Subscribe();
// boundList stays sorted and in sync with the cache
```

### Paging & Virtualisation

```csharp
// Paging: page number + page size
var pageRequest = new BehaviorSubject<IPageRequest>(new PageRequest(1, 25));
cache.Connect()
    .Sort(comparer)
    .Page(pageRequest)  // emits ISortedChangeSet with page context

// Virtualisation: start index + size (sliding window)
var virtualRequest = new BehaviorSubject<IVirtualRequest>(new VirtualRequest(0, 50));
cache.Connect()
    .Sort(comparer)
    .Virtualise(virtualRequest)
```

### Grouping

```csharp
// Group into mutable sub-caches (ManagedGroup)
cache.Connect()
    .Group(animal => animal.Family)
    .Subscribe(groupChanges =>
    {
        foreach (var change in groupChanges)
        {
            // change.Current is IGroup<Animal, int, AnimalFamily>
            // change.Current.Cache is IObservableCache<Animal, int>
        }
    });

// Group with immutable state snapshots (no mutable sub-cache)
cache.Connect()
    .GroupWithImmutableState(animal => animal.Family)

// Dynamic grouping: group key determined by per-item observable
cache.Connect()
    .GroupOnObservable(animal =>
        animal.WhenPropertyChanged(a => a.Category).Select(c => c.Value))
```

### Joining

All joins combine two changeset streams by key:

```csharp
var people = new SourceCache<Person, int>(p => p.Id);
var addresses = new SourceCache<Address, int>(a => a.PersonId);

// Full outer join — all items from both sides
people.Connect()
    .FullJoin(addresses.Connect(),
        address => address.PersonId,  // right key selector (maps to left key)
        (key, person, address) => new PersonWithAddress(
            key,
            person.HasValue ? person.Value : null,
            address.HasValue ? address.Value : null))

// Inner join — only matching keys
people.Connect()
    .InnerJoin(addresses.Connect(),
        address => address.PersonId,
        (keys, person, address) => new PersonWithAddress(person, address))

// Left join — all left items, optional right
people.Connect()
    .LeftJoin(addresses.Connect(),
        address => address.PersonId,
        (key, person, address) => new PersonView(person, address))

// Right join — all right items, optional left
people.Connect()
    .RightJoin(addresses.Connect(),
        address => address.PersonId,
        (key, person, address) => new AddressView(address, person))
```

### Combining (Set Operations)

```csharp
// Union — items present in either cache
cache1.Connect().Or(cache2.Connect())

// Intersection — items present in both
cache1.Connect().And(cache2.Connect())

// Difference — items in cache1 but not cache2
cache1.Connect().Except(cache2.Connect())

// Symmetric difference — items in one but not both
cache1.Connect().Xor(cache2.Connect())

// Merge N changeset streams (with optional conflict resolution)
var sources = new[] { cache1.Connect(), cache2.Connect(), cache3.Connect() };
sources.MergeChangeSets()
sources.MergeChangeSets(comparer)           // resolve conflicts with comparer
sources.MergeChangeSets(equalityComparer)   // resolve conflicts with equality
```

### Aggregation & Querying

```csharp
// Snapshot query on each change — IQuery gives Items, Keys, Count, Lookup
cache.Connect()
    .QueryWhenChanged()
    .Subscribe(query => StatusText = $"{query.Count} items");

// Projected query — emit a computed value on each change
cache.Connect()
    .QueryWhenChanged(query => query.Items.Sum(x => x.Price))
    .Subscribe(total => TotalPrice = total);

// Full collection on each change (less efficient than QueryWhenChanged)
cache.Connect()
    .ToCollection()
    .Subscribe(items => AllItems = items);
```

### Fan-out & Fan-in

```csharp
// MergeMany: subscribe to per-item observables, merge all results into one stream
cache.Connect()
    .MergeMany(animal => Observable.FromEventPattern(animal, nameof(animal.Escaped))
        .Select(_ => animal))
    .Subscribe(escapedAnimal => Alert(escapedAnimal));

// SubscribeMany: create per-item subscriptions (lifecycle managed — disposed on remove)
cache.Connect()
    .SubscribeMany(animal => animal.StartMonitoring())  // returns IDisposable
    .Subscribe();

// MergeManyChangeSets: each item produces its own changeset stream, merged into one
ownerCache.Connect()
    .MergeManyChangeSets(owner => owner.Pets.Connect())  // flattens all owners' pets
    .Subscribe(petChanges => ...);
```

### Refresh & Re-evaluation

```csharp
// AutoRefresh: when a property changes (INotifyPropertyChanged), emit a Refresh
// This causes downstream Filter/Sort/Group to re-evaluate that item
cache.Connect()
    .AutoRefresh(animal => animal.IncludeInResults)
    .Filter(animal => animal.IncludeInResults)  // re-evaluates when property changes

// AutoRefreshOnObservable: emit Refresh when a per-item observable fires
cache.Connect()
    .AutoRefreshOnObservable(animal => animal.StatusChanged)
```

### Lifecycle

```csharp
// DisposeMany: automatically dispose items that implement IDisposable on removal
cache.Connect()
    .Transform(model => new DisposableViewModel(model))
    .DisposeMany()

// Side effects on lifecycle events
cache.Connect()
    .OnItemAdded(item => Log($"Added: {item}"))
    .OnItemUpdated((current, previous) => Log($"Updated: {previous} -> {current}"))
    .OnItemRemoved(item => Log($"Removed: {item}"))
    .OnItemRefreshed(item => Log($"Refreshed: {item}"))
    .Subscribe();
```

### Buffering & Batching

```csharp
// BatchIf: buffer changesets while a condition is true, flush when false
var isPaused = new BehaviorSubject<bool>(false);
cache.Connect()
    .BatchIf(isPaused)
    .Subscribe(changes => UpdateUI(changes));

// Batch by time window
cache.Connect()
    .Batch(TimeSpan.FromMilliseconds(250))
    .Subscribe(changes => BatchUpdateUI(changes));

// Buffer the initial burst, then pass through
cache.Connect()
    .BufferInitial(TimeSpan.FromMilliseconds(100))
    .Subscribe(changes => ...);
```

### Binding

```csharp
// Bind to ObservableCollectionExtended (WPF/Avalonia)
cache.Connect()
    .Sort(comparer)
    .Bind(out ReadOnlyObservableCollection<Animal> collection)
    .Subscribe();
// collection updates automatically as cache changes

// SortAndBind to a plain List<T> (more efficient, no collection change events)
var list = new List<Animal>();
cache.Connect()
    .SortAndBind(list, comparer)
    .Subscribe();
```

### Utilities

```csharp
cache.Connect()
    .PopulateInto(targetCache)     // write changesets into another SourceCache
    .AsObservableCache()           // materialize as read-only IObservableCache
    .DeferUntilLoaded()            // defer until first non-empty changeset
    .SkipInitial()                 // skip the first changeset
    .NotEmpty()                    // filter out empty changesets
    .StartWithEmpty()              // emit empty changeset immediately
    .DistinctValues(x => x.Type)  // track distinct values of a property
    .ExpireAfter(x => TimeSpan.FromMinutes(5))  // auto-remove after timeout
    .LimitSizeTo(1000)            // FIFO eviction when size exceeds limit
    .Switch()                      // switch between changeset streams
```

## Writing a New Operator

1. **Extension method** in `ObservableCacheEx.cs` — validate arguments, delegate to internal class
2. **Internal sealed class** in `Cache/Internal/` with constructor + `Run()` method
3. `Run()` returns `Observable.Create<IChangeSet<T,K>>(observer => { ... })`
4. Inside `Create`: subscribe to source(s), process each changeset incrementally
5. Use `ChangeAwareCache<T,K>` for state management — call `CaptureChanges()` for output
6. Handle **all four change reasons**: Add, Update, Remove, Refresh
7. If multiple sources: serialize them (Synchronize with shared gate, or queue-drain pattern)
8. Wire up `OnError` and `OnCompleted` propagation
9. Return `CompositeDisposable` with all subscriptions and cleanup
10. Write tests: single item, batch, concurrent, error propagation, disposal, empty changeset
