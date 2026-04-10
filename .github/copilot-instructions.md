# DynamicData — AI Instructions

## What is DynamicData?

DynamicData is a reactive collections library for .NET, built on top of [Reactive Extensions (Rx)](https://github.com/dotnet/reactive). It provides `SourceCache<TObject, TKey>` and `SourceList<TObject>` — observable data collections that emit changesets when modified. These changesets flow through operator pipelines (Sort, Filter, Transform, Group, Join, etc.) that maintain live, incrementally-updated views of the data.

DynamicData is used in production by thousands of applications including large-scale enterprise software. It is the reactive data layer for [ReactiveUI](https://reactiveui.net/), making it foundational infrastructure for the .NET reactive ecosystem.

## Why Performance Matters

Every item flowing through a DynamicData pipeline passes through multiple operators. Each operator processes changesets — not individual items — so a single cache edit with 1000 items creates a changeset that flows through every operator in the chain. At library scale:

- **Per-item overhead compounds**: 1 allocation × 10 operators × 1000 items × 100 pipelines = 1M allocations per batch
- **Lock contention is the bottleneck**: operators serialize access to shared state. The drain pattern (enqueue under lock, deliver outside lock) is specifically designed to minimize lock hold time
- **Allocation-free hot paths**: the `Notification<T>` struct, `DeliveryQueue<T>`, and `SharedDeliveryQueue` are all designed for zero per-item heap allocation on the OnNext path

When optimizing, measure allocation rates and lock contention, not just wall-clock time.

## Why Rx Contract Compliance is Critical

DynamicData operators compose — the output of one is the input of the next. If any operator violates the Rx contract (e.g., concurrent `OnNext` calls, calls after `OnCompleted`), every downstream operator can corrupt its internal state. This is not a crash — it's silent data corruption that manifests as wrong results, missing items, or phantom entries. In a reactive UI, this means the user sees stale or incorrect data with no error.

See `.github/instructions/rx-contracts.instructions.md` for the complete Rx contract rules.

## Repository Structure

```
src/
├── DynamicData/                    # The library
│   ├── Cache/                      # Cache (keyed collection) operators
│   │   ├── Internal/               # Operator implementations
│   │   ├── ObservableCache.cs      # Core cache with DeliveryQueue drain
│   │   └── ObservableCacheEx.cs    # Extension methods (public API surface)
│   ├── List/                       # List (ordered collection) operators
│   │   └── Internal/               # Operator implementations
│   ├── Binding/                    # UI binding operators (SortAndBind)
│   ├── Internal/                   # Shared infrastructure
│   │   ├── DeliveryQueue.cs        # Queue-drain pattern for ObservableCache
│   │   ├── SharedDeliveryQueue.cs  # Multi-source queue-drain for operators
│   │   ├── Notification.cs         # Zero-alloc notification struct
│   │   └── CacheParentSubscription.cs # Base class for child-sub operators
│   └── Kernel/                     # Low-level utilities
├── DynamicData.Tests/              # Tests
│   ├── Cache/                      # Cache operator tests
│   ├── List/                       # List operator tests
│   ├── Domain/                     # Test domain types (Animal, Person, etc.)
│   └── Internal/                   # Infrastructure tests
```

## Operator Architecture Pattern

Most operators follow the same pattern:

```csharp
// Public API: extension method in ObservableCacheEx.cs
public static IObservable<IChangeSet<TDest, TKey>> Transform<TSource, TKey, TDest>(
    this IObservable<IChangeSet<TSource, TKey>> source,
    Func<TSource, TDest> transformFactory)
{
    return new Transform<TDest, TSource, TKey>(source, transformFactory).Run();
}

// Internal: class in Cache/Internal/ with a Run() method
internal sealed class Transform<TDest, TSource, TKey>
{
    public IObservable<IChangeSet<TDest, TKey>> Run() =>
        Observable.Create<IChangeSet<TDest, TKey>>(observer =>
        {
            // Subscribe to source, process changesets, emit results
        });
}
```

**Key points:**
- Extension method is the public API — thin wrapper
- Internal class holds parameters and implements `Run()`
- `Run()` returns `Observable.Create<T>` which defers subscription
- Inside `Create`, operators subscribe to sources and wire up changeset processing

## Thread Safety: The SharedDeliveryQueue Pattern

Operators that synchronize multiple sources use `SharedDeliveryQueue`:

```csharp
var locker = InternalEx.NewLock();
var queue = new SharedDeliveryQueue(locker);

source1.SynchronizeSafe(queue)  // enqueues items under lock, delivers outside
source2.SynchronizeSafe(queue)  // shares the same queue — serialized delivery
```

This replaces the old `Synchronize(lock)` pattern which held the lock during downstream delivery, causing cross-cache deadlocks.

## Breaking Changes

DynamicData is a library with thousands of downstream consumers. **Never**:
- Change the signature of a public extension method
- Change the behavior of an operator (ordering, filtering, error propagation)
- Add required parameters to existing methods
- Remove or rename public types

When adding new behavior, use new overloads or new methods. Mark deprecated methods with `[Obsolete]` and provide migration guidance.
