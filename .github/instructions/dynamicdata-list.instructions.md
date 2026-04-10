---
applyTo: "src/DynamicData/**/*.cs"
---
# DynamicData List Operators — Comprehensive Guide

List operators work with **unkeyed, ordered collections**: `IObservable<IChangeSet<T>>`. Items are identified by **index position**, not by key. This is the counterpart to Cache operators.

## SourceList — Where List Changesets Come From

`SourceList<T>` is the entry point. It is a **mutable, observable, ordered collection**.

```csharp
// Create
var list = new SourceList<string>();

// Mutate — all changes inside Edit() produce ONE changeset
list.Edit(inner =>
{
    inner.Add("Alice");
    inner.AddRange(new[] { "Bob", "Charlie" });
    inner.Remove("Alice");
    inner.Move(0, 1);       // move Bob from index 0 to index 1
    inner.RemoveAt(0);
    inner.Insert(0, "Dave");
    inner.Clear();
});
// ^ Produces 1 changeset with all the above changes

// Single-item convenience methods (each produces its own changeset)
list.Add("Eve");
list.Remove("Eve");

// Observe
list.Connect()
    .Subscribe(changes => Console.WriteLine($"Got {changes.Count} changes"));
```

**Key behaviors:**
- `Edit()` batches — all mutations produce **one** changeset
- Single-item methods (`Add`, `Remove`) each produce their own changeset
- `Connect()` immediately emits current contents as an `AddRange` changeset
- List operations preserve index positions — insertions and removals shift subsequent items
- Can be seeded from an `IObservable<IChangeSet<T>>` in the constructor

### IExtendedList — The Edit API

Inside `Edit()`, you receive an `IExtendedList<T>` (extends `IList<T>`):

```csharp
list.Edit(inner =>
{
    inner.Add(item);                    // append
    inner.Insert(index, item);          // insert at position
    inner.AddRange(items);              // append multiple
    inner.InsertRange(items, index);    // insert multiple at position
    inner[index] = newItem;             // replace at index (produces Replace)
    inner.Remove(item);                 // remove first occurrence
    inner.RemoveAt(index);              // remove at position
    inner.RemoveRange(index, count);    // remove range
    inner.Move(from, to);              // move item between positions
    inner.Clear();                      // remove all
});
```

## List Changesets — The Core Data Model

A list changeset (`IChangeSet<T>`) is an `IEnumerable<Change<T>>`. Each change has a different structure than cache changes.

### Change<T>

A list change is either an **item change** (single item) or a **range change** (batch):

```csharp
public sealed class Change<T>
{
    public ListChangeReason Reason { get; }  // Add, AddRange, Replace, Remove, etc.
    public ItemChange<T> Item { get; }       // for single-item changes
    public RangeChange<T> Range { get; }     // for range changes (AddRange, RemoveRange, Clear)
}

public struct ItemChange<T>
{
    public T Current { get; }            // the current item
    public Optional<T> Previous { get; } // previous item (Replace only)
    public int CurrentIndex { get; }     // current position (-1 if unknown)
    public int PreviousIndex { get; }    // previous position (Move, Replace)
}
```

### ListChangeReason

| Reason | Type | Meaning |
|--------|------|---------|
| `Add` | Item | Single item inserted at a position |
| `AddRange` | Range | Multiple items inserted |
| `Replace` | Item | Item at a position replaced with a new item (`Previous` available) |
| `Remove` | Item | Single item removed from a position |
| `RemoveRange` | Range | Multiple items removed |
| `Moved` | Item | Item moved from one position to another |
| `Refresh` | Item | Signal to re-evaluate (no data change) |
| `Clear` | Range | All items removed |

**Key difference from Cache:** List changes are **index-aware**. `Add` has a `CurrentIndex`, `Move` has both `CurrentIndex` and `PreviousIndex`, `Remove` has the index where the item was.

### ChangeAwareList — How List Operators Build Changesets

`ChangeAwareList<T>` is the list equivalent of `ChangeAwareCache<T,K>`. It's a `List<T>` that records every mutation.

```csharp
var list = new ChangeAwareList<T>();

// Mutations are recorded
list.Add(item);                    // records Add
list.Insert(0, item);             // records Add at index 0
list.AddRange(items);             // records AddRange
list[2] = newItem;                // records Replace
list.Remove(item);                // records Remove
list.RemoveRange(0, 5);          // records RemoveRange
list.Move(1, 3);                 // records Moved
list.Clear();                     // records Clear

// Harvest the changeset
var changes = list.CaptureChanges();
if (changes.Count > 0)
    observer.OnNext(changes);
```

## Operator Reference — Change Reason Handling

---

### Filter (static predicate)

Evaluates a `Func<T, bool>` predicate per item.

| Input | Behavior |
|-------|----------|
| **Add** | If matches → Add at calculated index. If not → nothing. |
| **AddRange** | Filters range, emits AddRange of matching items. |
| **Replace** | Re-evaluates. New matches + old didn't → Add. Both match → Replace. Old matched + new doesn't → Remove. |
| **Remove** | If was downstream → Remove. |
| **RemoveRange** | Removes matching items from downstream. |
| **Refresh** | Re-evaluates predicate. Adds/removes as needed. |
| **Clear** | → Clear. |
| **Moved** | If item is downstream → recalculates downstream index and emits Move. |

### Filter (dynamic predicate observable)

Same as static filter per-item, but when predicate observable fires, **all items** are re-evaluated.

### FilterOnObservable

Per-item `IObservable<bool>` controlling inclusion. Same as cache version but index-aware.

---

### Transform

Applies `Func<T, TDest>` to produce a parallel list of transformed items.

| Input | Behavior |
|-------|----------|
| **Add** | Calls factory → Add at same index. |
| **AddRange** | Calls factory for each → AddRange. |
| **Replace** | Calls factory on new item → Replace. |
| **Remove** | → Remove at same index (no factory call). |
| **RemoveRange** | → RemoveRange. |
| **Refresh** | Re-evaluates transform → Replace (or Refresh if same reference). |
| **Clear** | → Clear. |
| **Moved** | → Moved (same transformed item, new positions). |

### TransformAsync

Async version — `Func<T, Task<TDest>>`.

### TransformMany

Flattens 1:N — each source item produces multiple destination items.

| Input | Behavior |
|-------|----------|
| **Add** | Expands into N items → AddRange. |
| **Replace** | Diff old children vs new children. Remove old, Add new. |
| **Remove** | → RemoveRange of all children. |
| **Clear** | → Clear. |

---

### Sort

Sorts items using `IComparer<T>`. Maintains a sorted `ChangeAwareList<T>`.

| Input | Behavior |
|-------|----------|
| **Add** | Inserts at sorted position → Add with index. |
| **AddRange** | Inserts each at sorted position (or full reset if over threshold). |
| **Replace** | Removes old, inserts new at sorted position → Remove + Add (or Move). |
| **Remove** | → Remove at sorted index. |
| **Clear** | → Clear. |
| **Refresh** | Re-evaluates sort position. If position changed → Move. |
| **Comparer fires** | Full re-sort. Emits Moves/Adds/Removes as needed. |
| **Resort signal** | Reorders in-place using current comparer. |

---

### Or / And / Except / Xor (Set Operations)

Combine multiple list changeset streams using set logic with reference counting.

| Operator | Inclusion rule |
|----------|---------------|
| `Or` | Item in **any** source (union) |
| `And` | Item in **all** sources (intersection) |
| `Except` | Item in first but **not** others (difference) |
| `Xor` | Item in exactly **one** source (symmetric difference) |

For each operator, Add/Remove from any source updates the reference counts and the downstream list is recalculated.

### MergeChangeSets

Merges N list changeset streams into one. All changes are forwarded in order.

All changes from any source are forwarded directly to the merged output stream in the order they arrive.

---

### MergeMany

Subscribes to per-item observables, merges into single `IObservable<TDest>`.

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to per-item observable. |
| **Replace** | Disposes old, subscribes to new. |
| **Remove** | Disposes subscription. |
| **Clear** | Disposes all subscriptions. |

### MergeManyChangeSets (list → list)

Each item produces `IObservable<IChangeSet<TDest>>`. All flattened into one stream.

### MergeManyChangeSets (list → cache)

Each item produces `IObservable<IChangeSet<TDest, TKey>>`. Flattened into one keyed stream.

### SubscribeMany

Creates `IDisposable` per item. Disposes on removal/replacement.

| Input | Behavior |
|-------|----------|
| **Add** | Creates subscription → passes through. |
| **Replace** | Disposes old subscription, creates new → passes through. |
| **Remove** | Disposes → passes through. |
| **Clear** | Disposes all → passes through. |

---

### AutoRefresh

Monitors `INotifyPropertyChanged` and emits Refresh when specified property changes.

| Input | Behavior |
|-------|----------|
| **Add** | Subscribes to PropertyChanged → passes through. |
| **Replace** | Re-subscribes → passes through. |
| **Remove** | Disposes subscription → passes through. |
| **Property fires** | Emits Refresh changeset for that item. |

### AutoRefreshOnObservable

Per-item `IObservable<TAny>` triggers Refresh. Same pattern as AutoRefresh.

### SuppressRefresh

Strips Refresh changes from the stream.

---

### Page

Applies page number + page size windowing.

| Input | Behavior |
|-------|----------|
| **Any change** | Recalculates page window. Items entering page → Add. Leaving page → Remove. |
| **Page request fires** | Full recalculation of page contents. |

### Virtualise

Start index + size sliding window.

Same as Page but uses absolute start index + size instead of page number.

### Top

Takes first N items.

---

### Bind

Materializes list changes into an `ObservableCollectionExtended<T>` or `ReadOnlyObservableCollection<T>`.

| Input | Behavior |
|-------|----------|
| **Add** | Insert into bound collection at index. |
| **AddRange** | InsertRange or Reset (based on threshold). |
| **Replace** | Replace at index. |
| **Remove** | Remove at index. |
| **RemoveRange** | RemoveRange. |
| **Moved** | Move in collection. |
| **Clear** | Clear collection. |

### Clone

Applies changes to any `IList<T>`. Lower-level than Bind.

---

### GroupOn

Groups by a key selector. Emits `IChangeSet<IGroup<T, TGroupKey>>`.

| Input | Behavior |
|-------|----------|
| **Add** | Determines group → adds to that group. New group → Add(group). |
| **Replace** | If group changed → move between groups. |
| **Remove** | Removes from group. Empty group → Remove(group). |
| **Clear** | Clears all groups. |

### GroupWithImmutableState

Same logic, but emits immutable snapshots.

---

### DistinctValues

Tracks distinct values of a property with reference counting.

| Input | Behavior |
|-------|----------|
| **Add** | If value first seen → Add. Otherwise count++. |
| **Replace** | If value changed → old count--, new count++. |
| **Remove** | Count--. If reaches 0 → Remove distinct value. |
| **Clear** | Removes all distinct values. |

### QueryWhenChanged

Emits `IReadOnlyCollection<T>` on each change.

### ToCollection

Same as QueryWhenChanged — emits the full collection snapshot.

### ToSortedCollection

Emits a sorted `IReadOnlyCollection<T>`.

---

### DisposeMany

Disposes items on removal/replacement.

| Input | Behavior |
|-------|----------|
| **Add/AddRange** | Tracks items → passes through. |
| **Replace** | Disposes previous → passes through. |
| **Remove/RemoveRange** | Disposes removed items → passes through. |
| **Clear** | Disposes all → passes through. |
| **Subscription disposed** | Disposes all tracked items. |

### OnItemAdded / OnItemRemoved / OnItemRefreshed

Side-effect callbacks for specific lifecycle events.

| Operator | Fires on |
|----------|----------|
| `OnItemAdded` | Add, AddRange |
| `OnItemRemoved` | Remove, RemoveRange, Clear. Also fires for **all items** on disposal. |
| `OnItemRefreshed` | Refresh |

### ForEachChange / ForEachItemChange

Side effect per change. `ForEachChange` sees range changes too; `ForEachItemChange` only item-level.

---

### BufferIf

Buffers changes while condition is true, flushes when false.

| Input | Behavior |
|-------|----------|
| **Any (while paused)** | Accumulated into buffer. |
| **Condition → false** | Flushes all buffered changes. |
| **Any (while active)** | Passes through immediately. |

### BufferInitial

Buffers the initial burst for a time window.

---

### ExpireAfter

Auto-removes items after a timeout.

| Input | Behavior |
|-------|----------|
| **Add** | Schedules removal → passes through. |
| **Replace** | Resets timer → passes through. |
| **Remove** | Cancels timer → passes through. |
| **Timer fires** | Emits Remove. |

### LimitSizeTo

FIFO eviction when list exceeds size limit.

---

### Conversion & Utilities

```csharp
list.Connect()
    .PopulateInto(targetList)       // pipe changes into another SourceList
    .AsObservableList()             // materialize as read-only IObservableList
    .DeferUntilLoaded()             // suppress until first non-empty changeset
    .SkipInitial()                  // skip the initial snapshot
    .NotEmpty()                     // filter out empty changesets
    .StartWithEmpty()               // emit empty changeset on subscribe
    .RefCount()                     // share subscription with ref counting
    .Switch()                       // IObservable<IObservable<IChangeSet<T>>> → latest
    .Cast<TDest>()                 // cast items
    .RemoveIndex()                 // strip index information
    .Reverse()                      // reverse the collection order
    .WhereReasonsAre(reasons)      // only pass specific change reasons
    .WhereReasonsAreNot(reasons)   // exclude specific change reasons
    .FlattenBufferResult()         // flatten IChangeSet<IChangeSet<T>> to IChangeSet<T>
```

### ToObservableChangeSet

Converts a regular `IObservable<T>` or `IObservable<IEnumerable<T>>` into a list changeset stream. This is the **bridge from standard Rx into DynamicData**.

```csharp
// From a regular observable — each emission becomes an Add
myObservable.ToObservableChangeSet()

// With size limit
myObservable.ToObservableChangeSet(limitSizeTo: 100)

// With expiration
myObservable.ToObservableChangeSet(expireAfter: item => TimeSpan.FromMinutes(5))
```

---

### Property Observation

```csharp
// Observe a property on all items (requires INotifyPropertyChanged)
list.Connect()
    .WhenValueChanged(p => p.Age)
    .Subscribe(age => ...);

// Observe any property change
list.Connect()
    .WhenAnyPropertyChanged()
    .Subscribe(item => ...);
```

---

## Converting Between List and Cache

```csharp
// List → Cache: add a key
list.Connect()
    .Transform(item => item)               // optional
    .AddKey(item => item.Id)               // IChangeSet<T> → IChangeSet<T, TKey>
    // or use:
    .ToObservableChangeSet(item => item.Id)

// Cache → List: remove the key
cache.Connect()
    .RemoveKey()                           // IChangeSet<T, TKey> → IChangeSet<T>
```

---

## Writing a New List Operator

Same two-part pattern as cache operators:

1. **Extension method** in `ObservableListEx.cs`
2. **Internal class** in `List/Internal/` with `Run()` method

Use `ChangeAwareList<T>` instead of `ChangeAwareCache<T,K>`:

```csharp
internal sealed class MyListOperator<T>(IObservable<IChangeSet<T>> source)
    where T : notnull
{
    public IObservable<IChangeSet<T>> Run() =>
        Observable.Create<IChangeSet<T>>(observer =>
        {
            var list = new ChangeAwareList<T>();

            return source.SubscribeSafe(Observer.Create<IChangeSet<T>>(
                onNext: changes =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                                var item = change.Item;
                                if (ShouldInclude(item.Current))
                                    list.Add(item.Current);
                                break;
                            case ListChangeReason.AddRange:
                                list.AddRange(change.Range.Where(ShouldInclude));
                                break;
                            case ListChangeReason.Replace:
                                // handle replacement
                                break;
                            case ListChangeReason.Remove:
                                list.Remove(change.Item.Current);
                                break;
                            case ListChangeReason.RemoveRange:
                            case ListChangeReason.Clear:
                                // handle range removal
                                break;
                            case ListChangeReason.Moved:
                                // handle move
                                break;
                            case ListChangeReason.Refresh:
                                // handle refresh
                                break;
                        }
                    }

                    var output = list.CaptureChanges();
                    if (output.Count > 0)
                        observer.OnNext(output);
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted));
        });
}
```

### Checklist

1. Handle **all eight change reasons**: Add, AddRange, Replace, Remove, RemoveRange, Moved, Refresh, Clear
2. Use `ChangeAwareList<T>` for state management
3. Pay attention to **index positions** — list changes are index-aware
4. Never emit empty changesets
5. Propagate `OnError` and `OnCompleted`
6. Multiple sources → serialize with `Synchronize(gate)`
7. Write tests (see Testing section in main instructions)