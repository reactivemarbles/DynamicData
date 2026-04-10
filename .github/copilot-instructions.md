# DynamicData — AI Instructions

## What is DynamicData?

DynamicData is a reactive collections library for .NET, built on top of [Reactive Extensions (Rx)](https://github.com/dotnet/reactive). It provides `SourceCache<TObject, TKey>` and `SourceList<TObject>` — observable data collections that emit **changesets** when modified. These changesets flow through operator pipelines (Sort, Filter, Transform, Group, Join, etc.) that maintain live, incrementally-updated views of the data.

DynamicData is used in production by thousands of applications. It is the reactive data layer for [ReactiveUI](https://reactiveui.net/), making it foundational infrastructure for the .NET reactive ecosystem.

## Why Performance Matters

Every item flowing through a DynamicData pipeline passes through multiple operators. Each operator processes changesets — not individual items — so a single cache edit with 1000 items creates a changeset that flows through every operator in the chain. At library scale:

- **Per-item overhead compounds**: 1 allocation × 10 operators × 1000 items × 100 pipelines = 1M allocations per batch
- **Lock contention is the bottleneck**: operators serialize access to shared state. Minimizing lock hold time is a core design goal.
- **Prefer value types and stack allocation**: use structs, `ref struct`, `Span<T>`, and avoid closures in hot paths where possible

When optimizing, measure allocation rates and lock contention, not just wall-clock time.

## Why Rx Contract Compliance is Critical

DynamicData operators compose — the output of one is the input of the next. If any operator violates the Rx contract (e.g., concurrent `OnNext` calls, calls after `OnCompleted`), every downstream operator can corrupt its internal state. This is not a crash — it's silent data corruption that manifests as wrong results, missing items, or phantom entries. In a reactive UI, this means the user sees stale or incorrect data with no error message.

See `.github/instructions/rx.instructions.md` for comprehensive Rx contract rules, scheduler usage, disposable patterns, and a complete standard Rx operator reference.

See `.github/instructions/dynamicdata-operators.instructions.md` for the full DynamicData operator catalog with usage examples and guidance on writing new operators.

## Breaking Changes

DynamicData is a library with thousands of downstream consumers. **Never**:
- Change the signature of a public extension method
- Change the behavior of an operator (ordering, filtering, error propagation)
- Add required parameters to existing methods
- Remove or rename public types

When adding new behavior, use new overloads or new methods. Mark deprecated methods with `[Obsolete]` and provide migration guidance.

## Repository Structure

```
src/
├── DynamicData/                    # The library
│   ├── Cache/                      # Cache (keyed collection) operators
│   │   ├── Internal/               # Operator implementations (private)
│   │   ├── ObservableCache.cs      # Core observable cache implementation
│   │   └── ObservableCacheEx.cs    # Public API: extension methods for cache operators
│   ├── List/                       # List (ordered collection) operators
│   │   ├── Internal/               # Operator implementations (private)
│   │   └── ObservableListEx.cs     # Public API: extension methods for list operators
│   ├── Binding/                    # UI binding operators (SortAndBind, etc.)
│   ├── Internal/                   # Shared internal infrastructure
│   └── Kernel/                     # Low-level types (Optional<T>, Error<T>, etc.)
├── DynamicData.Tests/              # Tests (xUnit + FluentAssertions)
│   ├── Cache/                      # Cache operator tests
│   ├── List/                       # List operator tests
│   └── Domain/                     # Test domain types using Bogus fakers
```

## Operator Architecture Pattern

Most operators follow the same two-part pattern:

```csharp
// 1. Public API: extension method in ObservableCacheEx.cs (thin wrapper)
public static IObservable<IChangeSet<TDest, TKey>> Transform<TSource, TKey, TDest>(
    this IObservable<IChangeSet<TSource, TKey>> source,
    Func<TSource, TDest> transformFactory)
{
    return new Transform<TDest, TSource, TKey>(source, transformFactory).Run();
}

// 2. Internal: sealed class in Cache/Internal/ with a Run() method
internal sealed class Transform<TDest, TSource, TKey>
{
    public IObservable<IChangeSet<TDest, TKey>> Run() =>
        Observable.Create<IChangeSet<TDest, TKey>>(observer =>
        {
            // Subscribe to source, process changesets, emit results
            // Use ChangeAwareCache<T,K> for incremental state
            // Call CaptureChanges() to produce the output changeset
        });
}
```

**Key points:**
- The extension method is the **public API surface** — keep it thin
- The internal class holds constructor parameters and implements `Run()`
- `Run()` returns `Observable.Create<T>` which **defers subscription** (cold observable)
- Inside `Create`, operators subscribe to sources and wire up changeset processing
- `ChangeAwareCache<T,K>` tracks incremental changes and produces immutable snapshots via `CaptureChanges()`
- Operators must handle all change reasons: `Add`, `Update`, `Remove`, `Refresh`

## Thread Safety in Operators

When an operator has multiple input sources that share mutable state:
- All sources must be serialized through a shared lock
- Use `Synchronize(gate)` with a shared lock object to serialize multiple sources
- Keep lock hold times as short as practical

When operators use `Synchronize(lock)` from Rx:
- The lock is held during the **entire** downstream delivery chain
- This ensures serialized delivery across multiple sources sharing a lock
- Always use a private lock object — never expose it to external consumers

## Testing

Tests use xUnit with FluentAssertions (via the AwesomeAssertions package). Domain types are generated using Bogus fakers in `DynamicData.Tests/Domain/Fakers.cs`.

Key test patterns:
- **`.AsAggregator()`** — captures all changesets for assertion
- **Stress tests** — multi-threaded tests that exercise concurrent access
- **Rx contract validation** — tests that verify serialized delivery, proper completion/error propagation
