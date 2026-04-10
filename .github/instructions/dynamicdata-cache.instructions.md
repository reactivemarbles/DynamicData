---
applyTo: "src/DynamicData/**/*.cs"
---
# DynamicData Cache Operators — Comprehensive Guide

Cache operators work with **keyed collections**: `IObservable<IChangeSet<TObject, TKey>>`. Every item has a unique key. This is the most commonly used side of DynamicData.

## SourceCache — Where Changesets Come From

`SourceCache<TObject, TKey>` is the entry point. It is a **mutable, observable, keyed collection**. You mutate it, and it emits changesets describing what changed.

```csharp
// Create — provide a key selector (like a primary key)
var cache = new SourceCache<Person, string>(p => p.Name);

// Mutate — all changes inside Edit() produce ONE changeset
cache.Edit(updater =>
{
    updater.AddOrUpdate(new Person("Alice", 30));
    updater.AddOrUpdate(new Person("Bob", 25));
    updater.Remove("Charlie");
});
// ^ This produces 1 changeset with 2 Adds + 1 Remove

// Single-item convenience methods (each produces its own changeset)
cache.AddOrUpdate(new Person("Dave", 40));  // 1 changeset with 1 Add
cache.Remove("Bob");                         // 1 changeset with 1 Remove

// Observe — Connect() returns the changeset stream
cache.Connect()
    .Subscribe(changes => Console.WriteLine($"Got {changes.Count} changes"));
```

**Key behaviors:**
- `Edit()` batches — all mutations inside a single `Edit()` lambda produce **one** changeset
- Single-item methods (`AddOrUpdate`, `Remove`) each produce their own changeset
- `Connect()` immediately emits the current cache contents as the first changeset (adds for all existing items)
- `Connect(predicate)` pre-filters at the source
- Multiple subscribers each get their own initial snapshot — `Connect()` creates a cold observable per subscriber

### ISourceUpdater — The Edit API

Inside `Edit()`, you receive an `ISourceUpdater<TObject, TKey>`:

```csharp
cache.Edit(updater =>
{
    updater.AddOrUpdate(item);                    // add or replace by key
    updater.AddOrUpdate(items);                   // batch add/replace
    updater.Remove(key);                          // remove by key
    updater.Remove(keys);                         // batch remove
    updater.Clear();                              // remove all items
    updater.Refresh(key);                         // emit Refresh for downstream re-evaluation
    updater.Refresh();                            // refresh ALL items
    updater.Lookup(key);                          // returns Optional<TObject>
});
```

## Changesets — The Core Data Model

A changeset (`IChangeSet<TObject, TKey>`) is an `IEnumerable<Change<TObject, TKey>>` — a batch of individual changes.

### Change<TObject, TKey>

```csharp
public readonly struct Change<TObject, TKey>
{
    public ChangeReason Reason { get; }        // Add, Update, Remove, Refresh, Moved
    public TKey Key { get; }                   // the item's key
    public TObject Current { get; }            // the current value
    public Optional<TObject> Previous { get; } // previous value (Update/Remove only)
    public int CurrentIndex { get; }           // position (-1 if unsorted)
    public int PreviousIndex { get; }          // previous position (-1 if unsorted)
}
```

### ChangeReason

| Reason | Meaning | `Previous` populated? |
|--------|---------|----------------------|
| `Add` | New key, first time seen | No |
| `Update` | Existing key, value replaced | Yes — the old value |
| `Remove` | Item removed from cache | Yes — the removed value |
| `Refresh` | No data change — signal to re-evaluate (filter/sort/group) | No |
| `Moved` | Item changed position in sorted collection | No (same item) |

### How Changesets Flow

```
SourceCache.Edit()
    │
    ▼
ChangeSet { Add("Alice"), Add("Bob"), Remove("Charlie") }
    │
    ▼  .Filter(p => p.Age >= 18)
ChangeSet { Add("Alice"), Add("Bob") }     ← Charlie was filtered out
    │
    ▼  .Transform(p => new PersonVM(p))
ChangeSet { Add(VM("Alice")), Add(VM("Bob")) }
    │
    ▼  .Sort(comparer)
ISortedChangeSet { sorted items with index positions }
    │
    ▼  .Bind(out collection)
ReadOnlyObservableCollection updated in-place
```

Each operator reconstructs a **new** changeset from its internal state — changesets are not passed through, they are re-emitted.

## ChangeAwareCache — How Operators Build Changesets

Inside operators, `ChangeAwareCache<TObject, TKey>` (a `Dictionary` that records every mutation) tracks state. Call `CaptureChanges()` to harvest the changeset and reset.

```csharp
var cache = new ChangeAwareCache<TDest, TKey>();

foreach (var change in incoming)
{
    switch (change.Reason)
    {
        case ChangeReason.Add:
        case ChangeReason.Update:
            cache.AddOrUpdate(transform(change.Current), change.Key);
            break;
        case ChangeReason.Remove:
            cache.Remove(change.Key);
            break;
        case ChangeReason.Refresh:
            cache.Refresh(change.Key);
            break;
    }
}

var output = cache.CaptureChanges();
if (output.Count > 0)
    observer.OnNext(output);
```

## Operator Reference — Change Reason Handling

Below is every cache operator with its exact handling of each `ChangeReason`. This documents the contract — what the operator emits downstream for each input reason.

Legend:
- **→ Add** = emits an Add downstream
- **→ Update** = emits an Update downstream
- **→ Remove** = emits a Remove downstream
- **→ Refresh** = emits a Refresh downstream
- **→ (nothing)** = swallowed, no downstream emission
- **→ conditional** = depends on state (explained in notes)

---

### Filter (static predicate)

Evaluates a `Func<TObject, bool>` predicate against each item.

| Input | Behavior |
|-------|----------|
| **Add** | If predicate matches → Add. If not → nothing. |
| **Update** | If new value matches → AddOrUpdate (Add if first match, Update if already downstream). If not → Remove (if was downstream). |
| **Remove** | If item was downstream → Remove. If not → nothing. |
| **Refresh** | Re-evaluates predicate. If now matches and wasn't → Add. If still matches → Refresh. If no longer matches → Remove. |

### Filter (dynamic predicate observable)

Like static filter, but when the predicate observable fires, **all items** are re-evaluated against the new predicate.

Per-item handling is the same as static filter. Additionally:

| Event | Behavior |
|-------|----------|
| **Predicate fires** | Full re-evaluation of all items: items newly matching → Add, no longer matching → Remove, still matching → Refresh or Update. |

### FilterOnObservable

Each item gets its own `IObservable<bool>` controlling inclusion.

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to per-item observable. When observable emits `true` → Add downstream. |
| **Update** | Disposes old subscription, subscribes to new item's observable. |
| **Remove** | Disposes subscription. If item was downstream → Remove. |
| **Refresh** | Forwarded as Refresh if item is currently downstream. |
| **Item observable fires** | `true` and not downstream → Add. `false` and downstream → Remove. |

### FilterImmutable

Optimized filter that assumes items never change — Refresh is ignored entirely.

| Input | Behavior |
|-------|----------|
| **Add** | If predicate matches → Add. |
| **Update** | If new value matches → AddOrUpdate. If not → Remove. |
| **Remove** | If downstream → Remove. |
| **Refresh** | **Ignored** — items are assumed immutable. |

### WhereReasonsAre / WhereReasonsAreNot

Passes through only changes with specified reasons.

| Input | Behavior |
|-------|----------|
| **Any reason** | If reason is in the allowed set → pass through. Otherwise → nothing. |

---

### Transform

Applies `Func<TSource, TDest>` to produce a parallel keyed collection of transformed items.

| Input | Behavior |
|-------|----------|
| **Add** | Calls transform factory → Add transformed item. |
| **Update** | Calls transform factory with current and previous → Update transformed item. |
| **Remove** | → Remove (no factory call). |
| **Refresh** | Default: → Refresh (forwarded, no re-transform). With `transformOnRefresh: true`: calls factory again → Update. |

### TransformSafe

Same as Transform, but catches exceptions in the transform factory and routes them to an error callback instead of `OnError`.

Same as Transform, but catches exceptions in the transform factory and routes them to an error callback instead of `OnError`. The changeset is still emitted — only the failed item is skipped and reported.

### TransformAsync

Async version of Transform — `Func<TSource, Task<TDest>>`.

Same change handling as Transform, but the factory returns `Task<TDest>` and is awaited.

### TransformWithInlineUpdate

On Add: creates new transformed item. On Update: **mutates the existing transformed item** instead of replacing.

| Input | Behavior |
|-------|----------|
| **Add** | Calls transform factory → Add. |
| **Update** | Calls inline update action on existing transformed item → Update (same reference, mutated). |
| **Remove** | → Remove. |
| **Refresh** | Default: → Refresh. With `transformOnRefresh: true`: inline update → Update. |

### TransformImmutable

Optimized transform that skips Refresh handling.

| Input | Behavior |
|-------|----------|
| **Add** | Calls factory → Add. |
| **Update** | Calls factory → Update. |
| **Remove** | → Remove. |
| **Refresh** | **Ignored.** |

### TransformOnObservable

Each item gets a per-item `IObservable<TDest>`. The latest emitted value is the transformed result.

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to per-item observable. First emission → Add downstream. Subsequent → Update. |
| **Update** | Disposes old subscription, subscribes to new observable. |
| **Remove** | Disposes subscription → Remove. |
| **Refresh** | Forwarded if item is downstream. |

### TransformMany

Flattens 1:N — each source item produces multiple destination items with their own keys.

| Input | Behavior |
|-------|----------|
| **Add** | Expands item into N children via selector → Add for each child. If child observable provided, subscribes for live updates. |
| **Update** | Diff old children vs new children → Remove old-only, Add new-only, Update shared keys. |
| **Remove** | → Remove all children of this parent. Dispose child subscription. |
| **Refresh** | Re-expands → diff children (effectively same as Update). |

### ChangeKey

Re-keys items using a new key selector.

| Input | Behavior |
|-------|----------|
| **Add** | → Add with new key. |
| **Update** | If new key same as old → Update. If key changed → Remove(old key) + Add(new key). |
| **Remove** | → Remove with mapped key. |
| **Refresh** | → Refresh with mapped key. |

### TransformToTree

Builds hierarchical tree from flat list using a parent key selector.

| Input | Behavior |
|-------|----------|
| **Add** | Creates tree node, attaches to parent (or root) → Add. |
| **Update** | Updates node. If parent changed → re-parents (Remove from old, Add to new). |
| **Remove** | Removes node and orphans/re-parents children → Remove. |
| **Refresh** | → Refresh on node. |

---

### Sort

Sorts items using `IComparer<T>`. Emits `ISortedChangeSet` with index positions.

| Input | Behavior |
|-------|----------|
| **Add** | Inserts at sorted position → Add with index. May emit Moves for displaced items. |
| **Update** | If sort position unchanged → Update. If position changed → Update + Move. |
| **Remove** | → Remove at index. May emit Moves for displaced items. |
| **Refresh** | Re-evaluates sort position. If unchanged → Refresh. If changed → Move. |
| **Comparer fires** | Full re-sort of all items. Emits Moves for items that changed position. |

### SortAndBind

Combines Sort + Bind into a single operator for efficiency. Maintains a sorted `IList<T>` in-place.

Same change handling as Sort, but directly applies insert/remove/move operations to the bound `IList<T>` instead of emitting a changeset.

### Page

Takes a sorted stream and applies page number + page size windowing.

| Input | Behavior |
|-------|----------|
| **Add/Update/Remove/Refresh** | From Sort output, applies page window. Items outside page → filtered out. |
| **Page request fires** | Recalculates page window → Add items now in page, Remove items now outside. |

### Virtualise

Takes a sorted stream and applies start index + size windowing (sliding window).

Same as Page but uses absolute start index + size instead of page number + page size.

### Top

Takes the first N items from a sorted stream.

| Input | Behavior |
|-------|----------|
| **Add** | If within top N → Add. Bumps Nth item out → Remove. |
| **Remove** | If was in top N → Remove. Next item enters → Add. |
| **Other** | Maintains top N invariant after each change. |

---

### Group (GroupOn)

Groups items by a key selector. Emits `IChangeSet<IGroup<TObject, TKey, TGroupKey>>`.

| Input | Behavior |
|-------|----------|
| **Add** | Determines group → adds item to that group's sub-cache. If new group → Add(group). |
| **Update** | If group unchanged → Update within group. If group changed → Remove from old group, Add to new group. Empty old group → Remove(group). |
| **Remove** | Removes from group. If group now empty → Remove(group). |
| **Refresh** | Re-evaluates group key. If same → Refresh within group. If changed → moves between groups. |

### GroupWithImmutableState

Same grouping as Group, but emits immutable snapshots instead of live sub-caches.

Same grouping logic as Group, but emits immutable snapshots instead of live sub-caches. Each affected group emits a new immutable snapshot on every change.

### GroupOnObservable

Group key is determined by a per-item `IObservable<TGroupKey>`. Items can move between groups reactively.

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to group key observable. On first emission → Add to group. |
| **Update** | Disposes old subscription, subscribes to new. |
| **Remove** | Disposes subscription → Remove from current group. |
| **Item observable fires** | If new group key ≠ current → Remove from old group, Add to new group. |

---

### InnerJoin

Combines two keyed streams. Only emits for keys present in **both** left and right.

| Input (left) | Behavior |
|-------|----------|
| **Add** | If matching right exists → Add joined result. |
| **Update** | If matching right exists → Update joined result. |
| **Remove** | → Remove joined result (if was downstream). |
| **Refresh** | → Refresh joined result (if downstream). |

| Input (right) | Behavior |
|-------|----------|
| **Add** | If matching left exists → Add joined result. |
| **Update** | → Update joined result. |
| **Remove** | → Remove joined result. |

### LeftJoin

All left items, optional right. Right side is `Optional<TRight>`.

| Input (left) | Behavior |
|-------|----------|
| **Add** | → Add (with right if exists, `Optional.None` otherwise). |
| **Update** | → Update. |
| **Remove** | → Remove. |
| **Refresh** | → Refresh. |

| Input (right) | Behavior |
|-------|----------|
| **Add** | If matching left exists → Update (left now has a right). |
| **Update** | → Update. |
| **Remove** | If matching left exists → Update (right becomes None). |

### RightJoin

Mirror of LeftJoin — all right items, optional left.

### FullJoin

All items from both sides. Both sides are `Optional<T>`.

| Input (either side) | Behavior |
|-------|----------|
| **Add** | → Add (or Update if other side already has entry). |
| **Update** | → Update. |
| **Remove** | If other side still has entry → Update (this side becomes None). If neither → Remove. |

### *JoinMany variants

Same join semantics, but the non-primary side is grouped — produces `IGrouping` instead of single items.

---

### Or (Union)

Items present in **any** source.

| Input | Behavior |
|-------|----------|
| **Add** (from any source) | If key not yet downstream → Add. If already present (from another source) → reference count incremented, no emission. |
| **Remove** (from any source) | Decrement reference count. If count reaches 0 → Remove. Otherwise → nothing. |
| **Update** | → Update if downstream. |

### And (Intersection)

Items present in **all** sources.

| Input | Behavior |
|-------|----------|
| **Add** | If key is now present in all sources → Add. Otherwise → nothing. |
| **Remove** | If was present in all → Remove. |
| **Update** | → Update if still in all sources. |

### Except (Difference)

Items in first source but **not** in any other source.

| Input | Behavior |
|-------|----------|
| **Add** (first source) | If not in any other source → Add. |
| **Add** (other source) | If key was downstream → Remove. |
| **Remove** (other source) | If key is in first source and now absent from all others → Add. |

### Xor (Symmetric Difference)

Items present in exactly **one** source.

| Input | Behavior |
|-------|----------|
| **Add** | If key is now in exactly 1 source → Add. If now in 2+ → Remove. |
| **Remove** | If key is now in exactly 1 → Add. If now in 0 → Remove. |

### MergeChangeSets

Merges N changeset streams into one. Last-writer-wins by default, or use comparer/equalityComparer for conflict resolution.

| Input (from any source) | Behavior |
|-------|----------|
| **Add** | → Add (or Update if key already from another source, resolved by comparer). |
| **Update** | → Update (resolved by comparer if configured). |
| **Remove** | → Remove (unless another source still has the key). |
| **Refresh** | → Refresh. |

---

### AutoRefresh

Monitors `INotifyPropertyChanged` on items and emits Refresh when a specified property changes.

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to PropertyChanged on item → passes through as Add. |
| **Update** | Disposes old subscription, subscribes to new item → passes through as Update. |
| **Remove** | Disposes subscription → passes through as Remove. |
| **Refresh** | → passes through as Refresh. |
| **Property changes** | Emits new changeset with `ChangeReason.Refresh` for that key. |

### AutoRefreshOnObservable

Like AutoRefresh, but uses a per-item `IObservable<TAny>` instead of `INotifyPropertyChanged`.

Same as AutoRefresh but uses a per-item `IObservable<TAny>` to trigger Refresh instead of `INotifyPropertyChanged`.

### SuppressRefresh

Strips all Refresh changes from the stream.

| Input | Behavior |
|-------|----------|
| **Add/Update/Remove** | → passes through unchanged. |
| **Refresh** | **Dropped.** |

---

### MergeMany

Subscribes to a per-item `IObservable<T>` for each item, merges all into a single `IObservable<T>` (not a changeset stream).

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to per-item observable. Emissions → merged output. |
| **Update** | Disposes old subscription, subscribes to new item's observable. |
| **Remove** | Disposes subscription. |
| **Refresh** | No effect on subscriptions. |

### MergeManyChangeSets

Each item produces its own `IObservable<IChangeSet>`. All are merged into a single flattened changeset stream.

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to child changeset stream. Child changes → merged into output. |
| **Update** | Disposes old child subscription, subscribes to new. |
| **Remove** | Disposes child subscription. Emits Remove for all child items. |
| **Refresh** | No effect on child subscriptions. |

### MergeManyItems

Like MergeMany but wraps each value with its parent item.

Same as MergeMany, but wraps each emission as `ItemWithValue<TObject, TValue>` — pairing the parent item with its emitted value.

### SubscribeMany

Creates an `IDisposable` subscription per item. Disposes on removal/update.

| Input | Behavior |
|-------|----------|
| **Add** | Calls subscription factory → stores IDisposable. Passes through Add. |
| **Update** | Disposes old subscription, creates new. Passes through Update. |
| **Remove** | Disposes subscription. Passes through Remove. |
| **Refresh** | Passes through. No subscription change. |

---

### DisposeMany / AsyncDisposeMany

Calls `Dispose()` (or `DisposeAsync()`) on items when they are removed or replaced.

| Input | Behavior |
|-------|----------|
| **Add** | Passes through. Tracks item for future disposal. |
| **Update** | Disposes **previous** item. Passes through Update. |
| **Remove** | Disposes item. Passes through Remove. |
| **Refresh** | Passes through. No disposal. |
| **Subscription disposed** | Disposes **all** tracked items. |

### OnItemAdded / OnItemUpdated / OnItemRemoved / OnItemRefreshed

Side-effect callbacks for specific lifecycle events. The changeset is forwarded unchanged.

| Operator | Fires on |
|----------|----------|
| `OnItemAdded` | Add only |
| `OnItemUpdated` | Update only (receives current + previous) |
| `OnItemRemoved` | Remove only. Also fires for **all items** on subscription disposal. |
| `OnItemRefreshed` | Refresh only |

### ForEachChange

Invokes an `Action<Change<T,K>>` for every individual change. All change reasons trigger the action. The changeset is forwarded unchanged.

---

### QueryWhenChanged

Materializes the cache on each changeset and emits a snapshot or projected value.

| Input | Behavior |
|-------|----------|
| **Any change** | Updates internal cache. Emits `IQuery<T,K>` snapshot (or projected value). |

### ToCollection

Emits `IReadOnlyCollection<TObject>` on every changeset.

| Input | Behavior |
|-------|----------|
| **Any change** | Rebuilds full collection from internal state → emits. |

### ToSortedCollection

Same as ToCollection but sorted.

### DistinctValues

Tracks distinct values of a property across all items. Emits `DistinctChangeSet<TValue>`.

| Input | Behavior |
|-------|----------|
| **Add** | Extracts value. If value first seen → Add. If already tracked → reference count++. |
| **Update** | If value changed: old value count--, new value count++. Add/Remove distinct values accordingly. |
| **Remove** | Decrements count. If count reaches 0 → Remove distinct value. |
| **Refresh** | Re-evaluates value. Same as Update logic. |

### TrueForAll / TrueForAny

Emits `bool` based on whether all/any items match a condition (via per-item observable).

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to per-item observable. Recalculates aggregate → emits bool. |
| **Update** | Re-subscribes. Recalculates. |
| **Remove** | Disposes subscription. Recalculates. |

### Watch / WatchValue

Filters the stream to a single key.

| Input | Behavior |
|-------|----------|
| **Add/Update/Remove/Refresh for target key** | Emits the Change (Watch) or just the value (WatchValue). |
| **Other keys** | Ignored. |

### ToObservableOptional

Watches a single key and emits `Optional<TObject>` — `Some` when present, `None` when removed.

---

### BatchIf

Buffers changesets while a condition is true, flushes as a single combined changeset when condition becomes false.

| Input | Behavior |
|-------|----------|
| **Any (while paused)** | Buffered — combined into internal changeset list. |
| **Any (while active)** | Passed through immediately. |
| **Condition becomes false** | Flushes all buffered changesets as a batch. |

### Batch (time-based)

Standard Rx `Buffer` applied to changeset streams.

### BufferInitial

Buffers the initial burst of changesets for a time window, then passes through.

---

### Bind

Materializes a sorted changeset stream into a `ReadOnlyObservableCollection<T>`.

| Input | Behavior |
|-------|----------|
| **Add** | Insert into collection at correct index. |
| **Update** | Replace item at index. |
| **Remove** | Remove from collection at index. |
| **Moved** | Move item in collection. |
| **Refresh** | Depends on binding adaptor. |

### PopulateInto

Writes changesets into another `SourceCache`.

| Input | Behavior |
|-------|----------|
| **Add** | → `AddOrUpdate` on target cache. |
| **Update** | → `AddOrUpdate` on target cache. |
| **Remove** | → `Remove` on target cache. |
| **Refresh** | → `Refresh` on target cache. |

---

### AsObservableCache

Materializes the stream into a read-only `IObservableCache<T,K>`.

### DeferUntilLoaded

Suppresses emissions until the first non-empty changeset, then passes all through.

### SkipInitial

Skips the first changeset (typically the initial snapshot from Connect()).

### NotEmpty

Filters out empty changesets.

### StartWithEmpty / StartWithItem

Emits an empty changeset (or a single Add) immediately on subscription.

### ExpireAfter

Auto-removes items after a timeout.

| Input | Behavior |
|-------|----------|
| **Add** | Schedules removal after timeout → passes through Add. |
| **Update** | Resets timer → passes through Update. |
| **Remove** | Cancels timer → passes through Remove. |
| **Timer fires** | Emits Remove for expired item. |

### LimitSizeTo

FIFO eviction when cache exceeds a size limit.

| Input | Behavior |
|-------|----------|
| **Add** | If cache exceeds limit → Remove oldest items. |
| **Other** | Passed through. |

### Switch

`IObservable<IObservable<IChangeSet<T,K>>>` → subscribes to the latest inner observable, disposing previous.

### RefCount

Shares the upstream subscription with reference counting.

### Cast / OfType

Cast items to a different type or filter by type.

### Flatten

Converts `IChangeSet<T,K>` into `IObservable<Change<T,K>>` — one emission per individual change.

### RemoveKey

Converts `IChangeSet<T,K>` to `IChangeSet<T>` — drops the key to produce a list changeset.

### EnsureUniqueKeys

Validates that all keys in each changeset are unique. Throws if duplicates detected.

### IgnoreSameReferenceUpdate / IgnoreUpdateWhen / IncludeUpdateWhen

Filters Update changes based on reference equality or a custom predicate. If filtered out, the Update is silently dropped.

---

### Property Observation

| Operator | Behavior |
|----------|----------|
| `WhenPropertyChanged(expr)` | Emits `PropertyValue<T, TProp>` (item + value) when the specified property changes on any item. Subscribes per-item on Add, disposes on Remove. |
| `WhenValueChanged(expr)` | Like above but emits just the property value (no sender). |
| `WhenAnyPropertyChanged()` | Emits the item when **any** property changes (no specific property). |

---

## Writing a New Cache Operator

### Step 1: Extension Method (Public API)

In `ObservableCacheEx.cs`:

```csharp
public static IObservable<IChangeSet<TDest, TKey>> MyOperator<TSource, TKey, TDest>(
    this IObservable<IChangeSet<TSource, TKey>> source,
    Func<TSource, TDest> selector)
    where TSource : notnull
    where TKey : notnull
    where TDest : notnull
{
    source.ThrowArgumentNullExceptionIfNull(nameof(source));
    selector.ThrowArgumentNullExceptionIfNull(nameof(selector));
    return new MyOperator<TSource, TKey, TDest>(source, selector).Run();
}
```

### Step 2: Internal Class

In `Cache/Internal/MyOperator.cs`:

```csharp
internal sealed class MyOperator<TSource, TKey, TDest>(
    IObservable<IChangeSet<TSource, TKey>> source,
    Func<TSource, TDest> selector)
    where TSource : notnull
    where TKey : notnull
    where TDest : notnull
{
    public IObservable<IChangeSet<TDest, TKey>> Run() =>
        Observable.Create<IChangeSet<TDest, TKey>>(observer =>
        {
            var cache = new ChangeAwareCache<TDest, TKey>();

            return source.SubscribeSafe(Observer.Create<IChangeSet<TSource, TKey>>(
                onNext: changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                                cache.AddOrUpdate(selector(change.Current), change.Key);
                                break;
                            case ChangeReason.Remove:
                                cache.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                                cache.Refresh(change.Key);
                                break;
                        }
                    }

                    var output = cache.CaptureChanges();
                    if (output.Count > 0)
                        observer.OnNext(output);
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted));
        });
}
```

### Checklist

1. Handle **all four change reasons**: Add, Update, Remove, Refresh
2. Use `ChangeAwareCache<T,K>` for state — call `CaptureChanges()` for output
3. Never emit empty changesets
4. Propagate `OnError` and `OnCompleted`
5. Multiple sources → serialize with `Synchronize(gate)` using a shared lock
6. Return proper `IDisposable` (`CompositeDisposable` if multiple subscriptions)
7. Write tests covering all scenarios (see Testing section in main instructions)