---
applyTo: "src/DynamicData/**/*.cs"
---
# DynamicData Cache Operators Guide

## How Operators Work

Every cache operator:
1. Receives `IObservable<IChangeSet<TObject, TKey>>` (a stream of incremental changes)
2. Processes each changeset (adds, updates, removes, refreshes)
3. Emits a new `IChangeSet` downstream with the transformed/filtered/sorted result
4. Maintains internal state for incremental processing (no full re-evaluation)

## Operator Categories

### Filtering
| Operator | Description | Example |
|----------|-------------|---------|
| `Filter(predicate)` | Static predicate, re-evaluated on each changeset | `.Filter(x => x.IsActive)` |
| `Filter(IObservable<Func<T,bool>>)` | Dynamic predicate, re-evaluates all items when predicate changes | `.Filter(predicateStream)` |
| `FilterOnObservable(factory)` | Per-item observable that controls visibility | `.FilterOnObservable(x => x.WhenChanged(p => p.IsVisible))` |
| `WhereReasonsAre(reasons)` | Filter by change reason | `.WhereReasonsAre(ChangeReason.Add, ChangeReason.Remove)` |
| `WhereReasonsAreNot(reasons)` | Exclude change reasons | `.WhereReasonsAreNot(ChangeReason.Refresh)` |

### Transformation
| Operator | Description | Example |
|----------|-------------|---------|
| `Transform(factory)` | 1:1 transform, maintains cache of transformed items | `.Transform(x => new ViewModel(x))` |
| `TransformSafe(factory, errorHandler)` | Transform with error callback instead of OnError | `.TransformSafe(x => Parse(x), e => Log(e))` |
| `TransformAsync(factory)` | Async 1:1 transform | `.TransformAsync(async x => await FetchDetails(x))` |
| `TransformMany(manySelector, keySelector)` | 1:N flatten | `.TransformMany(x => x.Children, c => c.Id)` |
| `TransformOnObservable(factory)` | Transform via per-item observable | `.TransformOnObservable(x => x.WhenChanged(...))` |
| `TransformWithInlineUpdate(factory, updater)` | Transform with in-place update on change | `.TransformWithInlineUpdate(x => new VM(x), (vm, x) => vm.Update(x))` |
| `Cast<TDest>()` | Type cast each item | `.Cast<DerivedType>()` |
| `ChangeKey(keySelector)` | Re-key items | `.ChangeKey(x => x.AlternateId)` |

### Sorting
| Operator | Description | Example |
|----------|-------------|---------|
| `Sort(comparer)` | Sort with static or dynamic comparer | `.Sort(SortExpressionComparer<T>.Ascending(x => x.Name))` |
| `Sort(IObservable<IComparer<T>>)` | Re-sort when comparer changes | `.Sort(comparerStream)` |
| `SortAndBind(list, comparer)` | Sort and bind to a mutable list (UI binding) | `.SortAndBind(myList, comparer)` |

### Paging & Virtualisation
| Operator | Description | Example |
|----------|-------------|---------|
| `Page(IObservable<IPageRequest>)` | Page the sorted results | `.Sort(c).Page(pageStream)` |
| `Virtualise(IObservable<IVirtualRequest>)` | Virtualise a window into sorted results | `.Sort(c).Virtualise(windowStream)` |

### Grouping
| Operator | Description | Example |
|----------|-------------|---------|
| `Group(keySelector)` | Group into mutable sub-caches | `.Group(x => x.Category)` |
| `GroupWithImmutableState(keySelector)` | Group with immutable snapshots | `.GroupWithImmutableState(x => x.Category)` |
| `GroupOnObservable(factory)` | Dynamic grouping via per-item observable | `.GroupOnObservable(x => x.WhenChanged(p => p.Group))` |

### Joining
| Operator | Description | Example |
|----------|-------------|---------|
| `FullJoin(right, rightKeySelector, resultSelector)` | Full outer join | `.FullJoin(right, r => r.ForeignKey, (k, l, r) => ...)` |
| `InnerJoin(right, rightKeySelector, resultSelector)` | Inner join | `.InnerJoin(right, r => r.ForeignKey, (k, l, r) => ...)` |
| `LeftJoin(right, rightKeySelector, resultSelector)` | Left outer join | `.LeftJoin(right, r => r.ForeignKey, (k, l, r) => ...)` |
| `RightJoin(right, rightKeySelector, resultSelector)` | Right outer join | `.RightJoin(right, r => r.ForeignKey, (k, l, r) => ...)` |

### Combining
| Operator | Description | Example |
|----------|-------------|---------|
| `Or(other)` | Union of two caches | `.Or(otherCache.Connect())` |
| `And(other)` | Intersection | `.And(otherCache.Connect())` |
| `Except(other)` | Set difference | `.Except(otherCache.Connect())` |
| `Xor(other)` | Symmetric difference | `.Xor(otherCache.Connect())` |
| `MergeChangeSets(sources)` | Merge N changeset streams with conflict resolution | `sources.MergeChangeSets()` |

### Aggregation & Querying
| Operator | Description | Example |
|----------|-------------|---------|
| `QueryWhenChanged()` | Emit a snapshot query on each change | `.QueryWhenChanged()` |
| `QueryWhenChanged(selector)` | Emit a projected value on each change | `.QueryWhenChanged(q => q.Count)` |
| `ToCollection()` | Emit full collection on each change | `.ToCollection()` |
| `Count()` | Emit count on each change | `.Count()` |

### Fan-out & Fan-in
| Operator | Description | Example |
|----------|-------------|---------|
| `MergeMany(selector)` | Subscribe to per-item observables, merge results | `.MergeMany(x => x.PropertyChanges)` |
| `SubscribeMany(factory)` | Create per-item subscriptions (lifecycle managed) | `.SubscribeMany(x => x.Initialize())` |
| `MergeManyChangeSets(selector)` | Merge per-item changeset streams | `.MergeManyChangeSets(x => x.Children.Connect())` |

### Lifecycle
| Operator | Description | Example |
|----------|-------------|---------|
| `DisposeMany()` | Dispose items on removal/update | `.DisposeMany()` |
| `OnItemAdded(action)` | Side effect on add | `.OnItemAdded(x => Log(x))` |
| `OnItemRemoved(action)` | Side effect on remove | `.OnItemRemoved(x => Cleanup(x))` |
| `OnItemUpdated(action)` | Side effect on update | `.OnItemUpdated((curr, prev) => ...)` |
| `OnItemRefreshed(action)` | Side effect on refresh | `.OnItemRefreshed(x => ...)` |

### Refresh & Re-evaluation
| Operator | Description | Example |
|----------|-------------|---------|
| `AutoRefresh(propertySelector)` | Emit Refresh when property changes (INPC) | `.AutoRefresh(x => x.Status)` |
| `AutoRefreshOnObservable(factory)` | Emit Refresh when per-item observable fires | `.AutoRefreshOnObservable(x => x.Changed)` |

### Buffering
| Operator | Description | Example |
|----------|-------------|---------|
| `BatchIf(pauseObservable)` | Buffer changesets while paused, flush on resume | `.BatchIf(isPaused)` |
| `BufferInitial(TimeSpan)` | Buffer initial burst, then pass through | `.BufferInitial(TimeSpan.FromMilliseconds(250))` |
| `Batch(TimeSpan)` | Time-based batching | `.Batch(TimeSpan.FromMilliseconds(100))` |

### Binding
| Operator | Description | Example |
|----------|-------------|---------|
| `Bind(collection)` | Bind to an `ObservableCollectionExtended<T>` | `.Bind(out var list)` |
| `SortAndBind(list, comparer)` | Sort and bind to a plain `IList<T>` | `.SortAndBind(myList, comparer)` |

### Utilities
| Operator | Description |
|----------|-------------|
| `PopulateInto(cache)` | Write changesets into another SourceCache |
| `AsObservableCache()` | Materialize as a read-only ObservableCache |
| `Switch()` | Switch between changeset streams |
| `DeferUntilLoaded()` | Defer subscription until first changeset |
| `SkipInitial()` | Skip the first changeset (initial load) |
| `NotEmpty()` | Filter out empty changesets |
| `RefCount()` | Reference-counted sharing |
| `StartWithEmpty()` | Emit an empty changeset immediately |
| `DistinctValues(selector)` | Track distinct values of a projected property |
| `ExpireAfter(timeSelector)` | Auto-remove items after a timeout |
| `LimitSizeTo(count)` | Remove oldest items when size exceeds limit |

## Writing a New Operator

1. **Extension method** in `ObservableCacheEx.cs` — thin wrapper
2. **Internal class** in `Cache/Internal/` with a `Run()` method
3. Inside `Run()`, use `Observable.Create<IChangeSet<T,K>>`
4. If multiple sources share mutable state: use `SharedDeliveryQueue`
5. Handle all change reasons: Add, Update, Remove, Refresh
6. Use `ChangeAwareCache<T,K>` for incremental state management
7. Call `CaptureChanges()` to create the output changeset (immutable snapshot)
8. Write tests covering: single item, batch, concurrent, error propagation, disposal
