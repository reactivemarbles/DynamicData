# DynamicData — AI Instructions

## What is DynamicData?

DynamicData is a reactive collections library for .NET, built on top of [Reactive Extensions (Rx)](https://github.com/dotnet/reactive). It provides `SourceCache<TObject, TKey>` and `SourceList<TObject>` — observable data collections that emit **changesets** when modified. These changesets flow through operator pipelines (Sort, Filter, Transform, Group, Join, etc.) that maintain live, incrementally-updated views of the data.

DynamicData is used in production by thousands of applications. It is the reactive data layer for [ReactiveUI](https://reactiveui.net/), making it foundational infrastructure for the .NET reactive ecosystem.

## Cache vs List — Two Collection Types

DynamicData provides two parallel collection types. **Choose the right one — they are not interchangeable.**

| | **Cache** (`SourceCache<T, TKey>`) | **List** (`SourceList<T>`) |
|---|---|---|
| **Identity** | Items identified by unique key | Items identified by index position |
| **Duplicates** | Not allowed (key must be unique) | Allowed (same item at multiple positions) |
| **Ordering** | Unordered by default (Sort adds ordering) | Inherently ordered (like `List<T>`) |
| **Best for** | Entities with IDs, lookup by key | Ordered sequences, duplicates OK |
| **Change types** | Add, Update, Remove, Refresh, Moved | Add, AddRange, Replace, Remove, RemoveRange, Moved, Refresh, Clear |
| **Changeset** | `IChangeSet<TObject, TKey>` | `IChangeSet<T>` |

**Rule of thumb:** If your items have a natural unique key (ID, name, etc.), use **Cache**. If order matters and/or duplicates are possible, use **List**. Cache is used far more often in practice.

See `.github/instructions/dynamicdata-cache.instructions.md` for the complete cache operator reference.

See `.github/instructions/dynamicdata-list.instructions.md` for the complete list operator reference.

## Why Performance Matters

Every item flowing through a DynamicData pipeline passes through multiple operators. Each operator processes changesets — not individual items — so a single cache edit with 1000 items creates a changeset that flows through every operator in the chain. At library scale:

- **Per-item overhead compounds**: 1 allocation × 10 operators × 1000 items × 100 pipelines = 1M allocations per batch
- **Lock contention is the bottleneck**: operators serialize access to shared state. Minimizing lock hold time is a core design goal.
- **Prefer value types and stack allocation**: use structs, `ref struct`, `Span<T>`, and avoid closures in hot paths where possible

When optimizing, measure allocation rates and lock contention, not just wall-clock time.

## Why Rx Contract Compliance is Critical

DynamicData operators compose — the output of one is the input of the next. If any operator violates the Rx contract (e.g., concurrent `OnNext` calls, calls after `OnCompleted`), every downstream operator can corrupt its internal state. This is not a crash — it's silent data corruption that manifests as wrong results, missing items, or phantom entries. In a reactive UI, this means the user sees stale or incorrect data with no error message.

See `.github/instructions/rx.instructions.md` for comprehensive Rx contract rules, scheduler usage, disposable patterns, and a complete standard Rx operator reference.

## Breaking Changes

DynamicData follows [Semantic Versioning (SemVer)](https://semver.org/). Breaking changes **are possible** in major version bumps, but they are never done lightly. This library has thousands of downstream consumers — every breaking change has a blast radius.

**Rules:**
- Breaking changes require a major version bump. **You MUST explicitly call out any potentially breaking change to the user** before making it — even if you think it's minor. Let the maintainers decide.
- Prefer non-breaking alternatives first: new overloads, new methods, optional parameters with safe defaults.
- When a breaking change is justified, mark the old API with `[Obsolete("Use XYZ instead. This will be removed in vN+1.")]` in the current version and remove it in the next major.
- Behavioral changes (different ordering, different filtering semantics, different error propagation) are breaking even if the signature is unchanged. Call these out.
- Internal types (`internal` visibility) can change freely — they are not part of the public contract.

**What counts as breaking:**
- Changing the signature of a public extension method (parameters, return type, generic constraints)
- Changing observable behavior (emission order, filtering semantics, error/completion propagation)
- Removing or renaming public types, methods, or properties
- Adding required parameters to existing methods
- Changing the default behavior of an existing overload

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

**All new code MUST come with unit tests that prove 100% correctness. All bug fixes MUST include a regression test that reproduces the bug before verifying the fix.** No exceptions. Untested code is broken code — you just don't know it yet.

### Frameworks and Tools

- **xUnit** — test framework (`[Fact]`, `[Theory]`, `[InlineData]`)
- **FluentAssertions** — via the `AwesomeAssertions` NuGet package (`.Should().Be()`, `.Should().BeEquivalentTo()`, etc.)
- **Bogus** — fake data generation via `Faker<T>` in `DynamicData.Tests/Domain/Fakers.cs`
- **TestSourceCache<T,K>** and **TestSourceList<T>** — enhanced source collections in `Tests/Utilities/` that support `.Complete()` and `.SetError()` for testing terminal Rx events

### Test File Naming and Organization

Tests live in `src/DynamicData.Tests/` mirroring the library structure:
- `Cache/` — cache operator tests (one fixture class per operator, e.g., `TransformFixture.cs`)
- `List/` — list operator tests
- `Domain/` — shared domain types (`Person`, `Animal`, `AnimalOwner`, `Market`, etc.) and Bogus fakers
- `Utilities/` — test infrastructure (aggregators, validators, recording observers, stress helpers)

Naming convention: `{OperatorName}Fixture.cs`. For operators with multiple overloads, use partial classes: `FilterFixture.Static.cs`, `FilterFixture.DynamicPredicate.IntegrationTests.cs`, etc.

For cache-specific testing patterns (observation patterns, changeset assertions, stub pattern), see `.github/instructions/testing-cache.instructions.md`.

For list-specific testing patterns, see `.github/instructions/testing-list.instructions.md`.

### Testing the Rx Contract

`.ValidateSynchronization()` detects Rx contract violations. It tracks in-flight notifications with `Interlocked.Exchange` — if two threads enter `OnNext` simultaneously, it throws `UnsynchronizedNotificationException`. It uses raw observer/observable types to bypass Rx's built-in safety guards so violations are surfaced, not masked.

```csharp
source.Connect()
    .Transform(x => new ViewModel(x))
    .ValidateSynchronization()    // THROWS if concurrent delivery detected
    .RecordCacheItems(out var results);
```

### Writing Regression Tests for Bug Fixes

Every bug fix **must** include a test that:
1. **Reproduces the bug** — the test fails without the fix
2. **Verifies the fix** — the test passes with the fix
3. **Is named descriptively** — describes the scenario, not the bug ID

```csharp
// GOOD: describes the scenario that was broken
[Fact]
public void RemoveThenReAddWithSameKey_ShouldNotDuplicate()

// BAD: meaningless to future readers
[Fact]
public void FixBug1234()
```

### Stress Test Principles

- Use `Barrier` for simultaneous start — maximizes contention. Include the main thread in participant count.
- Use deterministic data so failures are reproducible. Use `Bogus.Randomizer` with a fixed seed — **never `System.Random`**.
- Assert the **exact final state**, not just "count > 0".
- Use `Task.WhenAny(completed, Task.Delay(timeout))` to detect deadlocks with a meaningful timeout.
- Include mixed operations: adds, updates, removes, property mutations, dynamic parameter changes.
- Use the `StressAddRemove` extension methods in `Tests/Utilities/` for standard add/remove patterns with timed removal.

### Test Utilities Reference

The `Tests/Utilities/` directory provides powerful helpers — **use them** instead of reinventing:

| Utility | Purpose |
|---------|---------|
| `ValidateSynchronization()` | Detects concurrent `OnNext` — Rx contract violation |
| `ValidateChangeSets(keySelector)` | Validates structural integrity of every changeset |
| `RecordCacheItems(out results)` | Cache recording observer with keyed + sorted tracking |
| `RecordListItems(out results)` | List recording observer |
| `TestSourceCache<T,K>` | SourceCache with `.Complete()` and `.SetError()` support |
| `TestSourceList<T>` | SourceList with `.Complete()` and `.SetError()` support |
| `StressAddRemove` extensions | Add/remove stress patterns with timed automatic removal |
| `ForceFail(count, exception)` | Forces an observable to error after N emissions |
| `Parallelize(count, parallel)` | Creates parallel subscriptions for stress testing |
| `ObservableSpy` | Diagnostic logging for pipeline debugging |
| `FakeScheduler` | Controlled scheduler for time-dependent tests |
| `Fakers.*` | Bogus fakers for `Person`, `Animal`, `AnimalOwner`, `Market` |

### Domain Types

Shared domain types in `Tests/Domain/`:

- **`Person`** — `Name` (key), `Age`, `Gender`, `FavoriteColor`, `PetType`. Implements `INotifyPropertyChanged`.
- **`Animal`** — `Name` (key), `Type`, `Family` (enum: Mammal, Reptile, Fish, Amphibian, Bird)
- **`AnimalOwner`** — `Name` (key), `Animals` (ObservableCollection). Ideal for `TransformMany`/`MergeManyChangeSets` tests.
- **`Market`** / **`MarketPrice`** — financial-style streaming data tests
- **`PersonWithGender`**, **`PersonWithChildren`**, etc. — transform output types

Generate test data with Bogus:
```csharp
var people = Fakers.Person.Generate(100);
var animals = Fakers.Animal.Generate(50);
var owners = Fakers.AnimalOwnerWithAnimals.Generate(10);  // pre-populated with animals
```

### Test Anti-Patterns

**❌ Testing implementation details instead of behavior:**
```csharp
// BAD: message count is an implementation detail — fragile
results.Messages.Count.Should().Be(3);
// GOOD: test the observable behavior and final state
results.Data.Count.Should().Be(expectedCount);
results.Data.Items.Should().BeEquivalentTo(expectedItems);
```

**❌ Using `Thread.Sleep` for timing:**
```csharp
// BAD: flaky and slow
Thread.Sleep(1000);
// GOOD: use test schedulers or deterministic waiting
var scheduler = new TestScheduler();
scheduler.AdvanceBy(TimeSpan.FromSeconds(1).Ticks);
```

**❌ Ignoring disposal:**
```csharp
// BAD: leaks subscriptions, masks errors
var results = source.Connect().Filter(p => true).AsAggregator();
// GOOD: using ensures cleanup even if assertion throws
using var results = source.Connect().Filter(p => true).AsAggregator();
```

**❌ Non-deterministic data without seeds:**
```csharp
// BAD: failures aren't reproducible across runs
var random = new Random();
// GOOD: use Bogus Randomizer with a fixed seed
var randomizer = new Randomizer(42);
var people = Fakers.Person.UseSeed(randomizer.Int()).Generate(100);
```
