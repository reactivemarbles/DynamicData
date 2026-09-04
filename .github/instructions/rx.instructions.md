---
applyTo: "**/*.cs"
---
# Reactive Extensions (Rx) — DynamicData Practical Guide

Reference: [ReactiveX Observable Contract](http://reactivex.io/documentation/contract.html) | [Rx.NET GitHub](https://github.com/dotnet/reactive) | [IntroToRx.com](http://introtorx.com/)

**Authoritative reference: [`rx-design-guide.instructions.md`](./rx-design-guide.instructions.md)** is the complete distillation of the Microsoft Rx Design Guidelines (October 2010), with stable `§X.Y` rule IDs. Cite those IDs in PR descriptions, code reviews, and commit messages. This file covers practical, DynamicData-flavored material that complements the design guide: hot vs cold semantics, the modern Rx.NET scheduler/disposable APIs, an operator quick-reference, custom-operator patterns specific to this codebase, and common pitfalls.

## Core Concepts

### Composition

Rx's power comes from composition (per [§6.1](./rx-design-guide.instructions.md#61-implement-new-operators-by-composing-existing-operators)). Every operator returns a new `IObservable<T>`, enabling fluent chaining:

```csharp
source
    .Where(x => x.IsValid)                // filter
    .Select(x => x.Transform())           // project
    .DistinctUntilChanged()                // deduplicate
    .ObserveOn(RxApp.MainThreadScheduler) // marshal to UI thread
    .Subscribe(x => UpdateUI(x));         // consume
```

Each operator is a separate subscription. Disposing the final subscription cascades disposal upstream through the entire chain. This composability is what makes Rx powerful, and what makes contract violations devastating: a bug in any operator corrupts the entire downstream chain.

### Hot vs Cold Observables

Not covered by the PDF. Critical to understand for DynamicData consumers because most cache pipelines are cold and incorrectly assuming hot semantics is a common source of duplicated work.

**Cold**: starts producing items when subscribed to. Each subscriber gets its own sequence. Created with `Observable.Create`, `Observable.Defer`, `Observable.Return`, etc.

```csharp
// Cold: each subscriber triggers a new HTTP call
var cold = Observable.FromAsync(() => httpClient.GetAsync(url));
```

**Hot**: produces items regardless of subscribers. All subscribers share the same sequence. Examples: `Subject<T>`, `Observable.FromEventPattern`, UI events.

```csharp
// Hot: events fire whether or not anyone is listening
var hot = Observable.FromEventPattern<EventArgs>(button, nameof(button.Click));
```

**Converting**: `Publish()` + `Connect()` or `Publish().RefCount()` converts cold to hot (shared, see [§5.10](./rx-design-guide.instructions.md#510-use-the-publish-operator-to-share-side-effects)).

```csharp
var shared = coldSource.Publish().RefCount(); // auto-connect on first sub, auto-disconnect on last unsub
```

## The Observable Contract

The full contract lives in [§4](./rx-design-guide.instructions.md#4-the-rx-contract) of the design guide. Highlights:

- **[§4.1](./rx-design-guide.instructions.md#41-assume-the-rx-grammar)** Grammar: `OnNext* (OnCompleted | OnError)?`. Mutually-exclusive terminals. No notifications after a terminal.
- **[§4.2](./rx-design-guide.instructions.md#42-assume-observer-instances-are-called-in-a-serialized-fashion)** Serialized notifications. The single most-violated rule in practice. Violation produces silent state corruption.
- **[§4.3](./rx-design-guide.instructions.md#43-assume-resources-are-cleaned-up-after-an-onerror-or-oncompleted-message)** Resource cleanup on terminal.
- **[§4.4](./rx-design-guide.instructions.md#44-assume-a-best-effort-to-stop-all-outstanding-work-on-unsubscribe)** Unsubscribe semantics: best-effort stop, in-flight results suppressed.

DynamicData-specific: subscribers that throw inside `OnNext` propagate exceptions to the producing operator. Use `SubscribeSafe` to route subscriber exceptions to `OnError`:

```csharp
// Subscribe: exception in handler crashes the source
source.Subscribe(x => MayThrow(x));  // if MayThrow throws, exception propagates up

// SubscribeSafe: exception in handler routes to OnError
source.SubscribeSafe(Observer.Create<T>(
    onNext: x => MayThrow(x),
    onError: ex => HandleError(ex)));  // MayThrow exception goes here
```

## Schedulers

Schedulers control **when** and **where** work executes; they are Rx's abstraction over threading. The design guide's [§5.4](./rx-design-guide.instructions.md#54-consider-passing-a-specific-scheduler-to-concurrency-introducing-operators), [§5.5](./rx-design-guide.instructions.md#55-call-the-observeon-operator-as-late-and-in-as-few-places-as-possible), [§6.9–6.12](./rx-design-guide.instructions.md#69-parameterize-concurrency-by-providing-a-scheduler-argument) cover the rules. This section is the modern Rx.NET reference for which scheduler to actually use.

### Common Schedulers

| Scheduler | Use | Thread |
|-----------|-----|--------|
| `Scheduler.Default` | CPU-bound work | ThreadPool |
| `Scheduler.CurrentThread` | Trampoline (queue on current thread) | Current |
| `Scheduler.Immediate` | Execute synchronously, inline | Current |
| `TaskPoolScheduler.Default` | Task-based ThreadPool | ThreadPool |
| `NewThreadScheduler.Default` | Dedicated new thread per operation | New thread |
| `EventLoopScheduler` | Single dedicated thread (event loop) | Dedicated |
| `TestScheduler` | Deterministic virtual time (testing) | Test thread |

### Using Schedulers

```csharp
// Time-based operators accept an optional scheduler
Observable.Timer(TimeSpan.FromSeconds(1), scheduler)
Observable.Interval(TimeSpan.FromMilliseconds(100), scheduler)
source.Delay(TimeSpan.FromMilliseconds(500), scheduler)
source.Throttle(TimeSpan.FromMilliseconds(300), scheduler)
source.Buffer(TimeSpan.FromSeconds(1), scheduler)
source.Timeout(TimeSpan.FromSeconds(5), scheduler)
source.Sample(TimeSpan.FromMilliseconds(100), scheduler)

// ObserveOn: deliver notifications on a specific scheduler
source.ObserveOn(RxApp.MainThreadScheduler)  // marshal to UI thread

// SubscribeOn: subscribe (and produce) on a specific scheduler
source.SubscribeOn(TaskPoolScheduler.Default)  // subscribe on background thread
```

### Scheduler Injection for Testability

DynamicData convention (not in the PDF): **always inject schedulers** instead of using defaults. This enables deterministic testing via `TestScheduler`:

```csharp
// WRONG: hardcoded scheduler — untestable time-dependent behavior
public IObservable<T> GetData() =>
    _source.Throttle(TimeSpan.FromMilliseconds(300));

// RIGHT: injectable scheduler — testable
public IObservable<T> GetData(IScheduler? scheduler = null) =>
    _source.Throttle(TimeSpan.FromMilliseconds(300), scheduler ?? Scheduler.Default);

// TEST: use TestScheduler for deterministic time control
var testScheduler = new TestScheduler();
var results = new List<T>();
GetData(testScheduler).Subscribe(results.Add);
testScheduler.AdvanceBy(TimeSpan.FromMilliseconds(300).Ticks);
results.Should().HaveCount(1);
```

## Disposable Helpers

The design guide's [§6.13](./rx-design-guide.instructions.md#613-hand-out-all-disposable-instances-created-inside-the-operator-to-consumers) lists the disposable family and the requirement to hand all internal disposables back to consumers. This section is the practical reference with code samples for each.

### Disposable.Create

Creates a disposable from an action. The action runs exactly once on first disposal.

```csharp
var cleanup = Disposable.Create(() =>
{
    connection.Close();
    Log("Cleaned up");
});
// Later: cleanup.Dispose() runs the action once
```

### Disposable.Empty

A no-op disposable. Useful as a default or placeholder (required by [§6.5](./rx-design-guide.instructions.md#65-subscribe-implementations-should-not-throw) when routing an error from `Subscribe`).

```csharp
public IDisposable Subscribe(IObservable<T> source) =>
    isEnabled ? source.Subscribe(handler) : Disposable.Empty;
```

### CompositeDisposable

Collects multiple disposables and disposes them all at once. **The workhorse of Rx resource management.**

```csharp
var cleanup = new CompositeDisposable();

cleanup.Add(source1.Subscribe(handler1));
cleanup.Add(source2.Subscribe(handler2));
cleanup.Add(Disposable.Create(() => Log("All done")));

// Later: disposes ALL contained disposables
cleanup.Dispose();
```

Use in `Observable.Create` to manage multiple subscriptions:

```csharp
Observable.Create<T>(observer =>
{
    var cleanup = new CompositeDisposable();
    cleanup.Add(source1.Subscribe(observer));
    cleanup.Add(source2.Subscribe(x => observer.OnNext(Transform(x))));
    cleanup.Add(Disposable.Create(() => cache.Clear()));
    return cleanup;
});
```

### SerialDisposable

Holds a single disposable that can be **replaced**. Disposing the previous value when a new one is set. Useful for "switch" patterns. (PDF calls this `MutableDisposable`.)

```csharp
var serial = new SerialDisposable();

// Each assignment disposes the previous
serial.Disposable = source1.Subscribe(handler);  // subscribes to source1
serial.Disposable = source2.Subscribe(handler);  // disposes source1 sub, subscribes to source2
serial.Disposable = Disposable.Empty;             // disposes source2 sub

// Disposing the SerialDisposable disposes the current inner
serial.Dispose();
```

### SingleAssignmentDisposable

Like SerialDisposable but can only be assigned **once**. Throws on second assignment. Useful when a subscription is created asynchronously but disposal might happen before it's ready. (Modern addition, not in the PDF.)

```csharp
var holder = new SingleAssignmentDisposable();

// Start async subscription
Task.Run(() =>
{
    var sub = source.Subscribe(handler);
    holder.Disposable = sub;  // safe even if Dispose was already called
});

// Can dispose before assignment — the subscription will be disposed when assigned
holder.Dispose();
```

### RefCountDisposable

Tracks multiple "dependent" disposables. The underlying resource is only disposed when **all** dependents (plus the primary) are disposed. (Modern addition, not in the PDF.)

```csharp
var primary = new RefCountDisposable(expensiveResource);

var dep1 = primary.GetDisposable();  // increment ref count
var dep2 = primary.GetDisposable();  // increment ref count

dep1.Dispose();    // decrement — resource still alive
primary.Dispose(); // decrement — resource still alive (dep2 still holds)
dep2.Dispose();    // decrement to 0 — resource disposed!
```

### BooleanDisposable / CancellationDisposable

```csharp
// BooleanDisposable: check if disposed
var bd = new BooleanDisposable();
bd.IsDisposed; // false
bd.Dispose();
bd.IsDisposed; // true — useful for cancellation checks

// CancellationDisposable: bridges IDisposable and CancellationToken
var cd = new CancellationDisposable();
cd.Token; // CancellationToken that cancels on Dispose
cd.Dispose(); // triggers cancellation
```

## Standard Rx Operators Reference

Not in the PDF. A quick catalog of modern Rx.NET operators by category. For each operator, the design guide rules in [§5](./rx-design-guide.instructions.md#5-using-rx) and [§6](./rx-design-guide.instructions.md#6-operator-implementations) tell you how to use it correctly.

### Creation

| Operator | Description |
|----------|-------------|
| `Observable.Return(value)` | Emit one value, then complete |
| `Observable.Empty<T>()` | Complete immediately with no values |
| `Observable.Never<T>()` | Never emit, never complete |
| `Observable.Throw<T>(ex)` | Emit error immediately |
| `Observable.Create<T>(subscribe)` | Build a custom observable from a subscribe function |
| `Observable.Defer(factory)` | Defer observable creation until subscription |
| `Observable.Range(start, count)` | Emit a range of integers |
| `Observable.Generate(init, cond, iter, result)` | Iterative generation |
| `Observable.Timer(dueTime)` | Emit one value after a delay |
| `Observable.Interval(period)` | Emit incrementing long values at regular intervals |
| `Observable.FromAsync(asyncFactory)` | Wrap an async method as an observable |
| `Observable.FromEventPattern(add, remove)` | Convert .NET events to observables |
| `Observable.Start(func)` | Run a function asynchronously, emit result |
| `Observable.Using(resourceFactory, obsFactory)` | Create a resource with the subscription, dispose with it |

### Transformation

| Operator | Description |
|----------|-------------|
| `Select(selector)` | Project each item (aka Map) |
| `SelectMany(selector)` | Project and flatten (aka FlatMap) |
| `Scan(accumulator)` | Running aggregate (like Aggregate but emits each step) |
| `Buffer(count)` / `Buffer(timeSpan)` | Collect items into batches |
| `Window(count)` / `Window(timeSpan)` | Split into sub-observables |
| `GroupBy(keySelector)` | Group items by key into sub-observables |
| `Cast<T>()` | Cast items to a type |
| `OfType<T>()` | Filter and cast to a type |
| `Materialize()` | Wrap each notification as a `Notification<T>` value |
| `Dematerialize()` | Unwrap `Notification<T>` values back to notifications |
| `Timestamp()` | Attach timestamp to each item |
| `TimeInterval()` | Attach time interval since previous item |

### Filtering

| Operator | Description |
|----------|-------------|
| `Where(predicate)` | Filter items by predicate |
| `Distinct()` | Remove duplicates (all-time) |
| `DistinctUntilChanged()` | Remove consecutive duplicates |
| `Take(count)` | Take first N items, then complete |
| `TakeLast(count)` | Take last N items (buffers until complete) |
| `TakeWhile(predicate)` | Take while predicate is true |
| `TakeUntil(other)` | Take until another observable emits |
| `Skip(count)` | Skip first N items |
| `SkipLast(count)` | Skip last N items |
| `SkipWhile(predicate)` | Skip while predicate is true |
| `SkipUntil(other)` | Skip until another observable emits |
| `First()` / `FirstOrDefault()` | First item (or default), then complete |
| `Last()` / `LastOrDefault()` | Last item (or default), then complete |
| `Single()` / `SingleOrDefault()` | Exactly one item, error if more/less |
| `ElementAt(index)` | Item at specific index |
| `IgnoreElements()` | Suppress all values, pass through error/completed |
| `Throttle(timeSpan)` | Suppress items followed by another within timespan |
| `Debounce(timeSpan)` | Alias for Throttle |
| `Sample(timeSpan)` | Emit most recent value at regular intervals |

### Combining

| Operator | Description |
|----------|-------------|
| `Merge(other)` | Merge multiple streams into one (interleaved) |
| `Concat(other)` | Append one stream after another completes |
| `Switch()` | Subscribe to latest inner observable, unsubscribe previous |
| `Amb(other)` | Take whichever stream emits first, ignore the other |
| `Zip(other, selector)` | Pair items 1:1 from two streams |
| `CombineLatest(other, selector)` | Combine latest values whenever either emits |
| `WithLatestFrom(other, selector)` | Combine with latest from other (only when source emits) |
| `StartWith(values)` | Prepend values before the source |
| `Append(value)` | Append a value after the source completes |
| `Publish()` | Convert cold to hot via multicast (returns `IConnectableObservable<T>`) |
| `Publish().RefCount()` | Auto-connect on first subscriber, auto-disconnect on last |
| `Replay(bufferSize)` | Multicast with replay buffer |

### Aggregation

| Operator | Description |
|----------|-------------|
| `Aggregate(accumulator)` | Final aggregate (emits one value on complete) |
| `Count()` | Count of items (on complete) |
| `Sum()` / `Min()` / `Max()` / `Average()` | Numeric aggregates (on complete) |
| `ToArray()` | Collect all items into array (on complete) |
| `ToList()` | Collect all items into list (on complete) |
| `ToDictionary(keySelector)` | Collect into dictionary (on complete) |

### Error Handling

| Operator | Description |
|----------|-------------|
| `Catch(handler)` | Handle error by switching to another observable |
| `Catch<TException>(handler)` | Handle specific exception type |
| `Retry()` / `Retry(count)` | Resubscribe on error |
| `OnErrorResumeNext(other)` | Continue with another observable on error or complete |
| `Finally(action)` | Run action on dispose, error, or complete |
| `Do(onNext, onError, onCompleted)` | Side effects without affecting the stream |
| `DoFinally(action)` | Side effect on termination (like Finally but for observation) |

### Scheduling & Threading

| Operator | Description |
|----------|-------------|
| `ObserveOn(scheduler)` | Deliver notifications on specified scheduler |
| `SubscribeOn(scheduler)` | Subscribe (and produce) on specified scheduler |
| `Delay(timeSpan)` | Delay each notification by a time span |
| `Timeout(timeSpan)` | Error if no notification within timeout |
| `Synchronize()` | Serialize notifications with internal gate (per [§5.8](./rx-design-guide.instructions.md#58-use-the-synchronize-operator-only-to-fix-custom-iobservable-implementations) — only for non-conforming sources) |
| `Synchronize(gate)` | Serialize notifications with external gate object (the multi-source pattern from [§6.7](./rx-design-guide.instructions.md#67-serialize-calls-to-iobserver-methods-within-observable-sequence-implementations)) |

### Utility

| Operator | Description |
|----------|-------------|
| `Do(action)` | Perform side effect for each notification (see [§5.7](./rx-design-guide.instructions.md#57-make-side-effects-explicit-using-the-do-operator)) |
| `Publish().RefCount()` | Share a subscription among multiple subscribers |
| `Replay(bufferSize).RefCount()` | Share with replay |
| `AsObservable()` | Hide the implementation type (e.g., Subject → IObservable) |
| `Subscribe(observer)` | Subscribe with an IObserver |
| `Subscribe(onNext, onError, onCompleted)` | Subscribe with callbacks (see [§5.2](./rx-design-guide.instructions.md#52-consider-passing-multiple-arguments-to-subscribe)) |
| `SubscribeSafe(observer)` | Subscribe with exception routing to OnError |
| `ForEachAsync(action)` | Async iteration (returns Task) |
| `Wait()` | Block until complete (avoid on UI thread; see [§6.14](./rx-design-guide.instructions.md#614-operators-should-not-block)) |
| `ToTask()` | Convert to Task (last value) |

## Writing Custom Operators

[§6.1](./rx-design-guide.instructions.md#61-implement-new-operators-by-composing-existing-operators) requires composition over `Observable.Create`. [§6.2](./rx-design-guide.instructions.md#62-implement-custom-operators-using-observablecreate) governs how to use `Observable.Create` when composition isn't enough. This section is the DynamicData-specific elaboration of those rules: the Defer pattern for per-subscription state without `Observable.Create`, the canonical `Observable.Create` skeleton, and a multi-source pattern.

### Composition First — Observable.Create is a Last Resort

**The Rx contracts are axioms, not guidelines.** `Merge` subscribes sequentially. `Defer` evaluates at subscription time. `Do` fires synchronously during delivery. `Concat` subscribes to the second source only after the first completes. These guarantees are unconditional: they hold in every case, on every scheduler, under every threading model. If they didn't, nothing in Rx would work.

**Trust the contracts completely.** When you compose operators, you can reason about ordering, state, and lifecycle *because* the contracts are absolute. The moment you doubt them and add "safety" wrappers, you've abandoned the very thing that makes Rx code correct by construction.

**Before reaching for `Observable.Create`, ask: can this be expressed as a composition of existing operators?** Rx operators already handle subscription lifecycle, error propagation, disposal, and serialization. Manual observer forwarding inside `Observable.Create` reimplements all of that, and introduces bugs that the operators would have prevented.

**The smell:** if you're writing `observer.OnNext(x)` / `observer.OnError(ex)` / `observer.OnCompleted()` inside `Observable.Create`, you're manually reimplementing what `Subscribe` already does. Stop and look for the composition.

```csharp
// WRONG: imperative Observable.Create with manual forwarding
// This reimplements Subscribe, adds boilerplate, and is harder to reason about.
return Observable.Create<Optional<TObject>>(observer =>
{
    var seenValue = false;
    var sub = source.ToObservableOptional(key)
        .Subscribe(
            value =>
            {
                seenValue = true;
                observer.OnNext(value);
            },
            observer.OnError,
            observer.OnCompleted);

    if (!seenValue)
        observer.OnNext(Optional.None<TObject>());

    return sub;
});

// RIGHT: declarative composition using existing operators
// Each operator does one thing. The intent is immediately clear.
return Observable.Defer(() =>
{
    var seenValue = false;
    return source.ToObservableOptional(key)
        .Do(_ => seenValue = true)
        .Merge(Observable.Defer(() => seenValue
            ? Observable.Empty<Optional<TObject>>()
            : Observable.Return(Optional.None<TObject>())));
});
```

**Why the composition wins:**
- `Defer` creates per-subscription state (the `seenValue` bool) — no shared mutable state
- `Do` captures a side effect without altering the stream — no manual forwarding
- `Merge` with inner `Defer` evaluates the condition *after* the synchronous subscription phase — the `Defer` factory runs when `Merge` subscribes to its second source, which happens after the first source's synchronous emissions
- Error propagation, completion, and disposal are all handled by the operators — zero manual wiring

**When Observable.Create IS appropriate:**
- You need to manage non-Rx resources (event handlers, timers, native resources) tied to subscription lifetime
- You're building a genuinely novel source (not transforming existing observables)
- You need fine-grained control over when/how disposal cascades
- The operator maintains complex mutable state that doesn't map to any existing operator's semantics

Even then, prefer `Observable.Using` for resource lifecycle and `Observable.Defer` for per-subscription state before reaching for `Observable.Create`.

### The Defer Pattern — Per-Subscription State Without Observable.Create

`Observable.Defer` is the key to per-subscription mutable state in a purely compositional style:

```csharp
// Per-subscription counter without Observable.Create
public static IObservable<(T Item, int Index)> WithIndex<T>(this IObservable<T> source) =>
    Observable.Defer(() =>
    {
        var index = 0;
        return source.Select(item => (item, index++));
    });

// Per-subscription flag for conditional behavior
public static IObservable<T> EmitDefaultIfEmpty<T>(this IObservable<T> source, T defaultValue) =>
    Observable.Defer(() =>
    {
        var hasEmitted = false;
        return source
            .Do(_ => hasEmitted = true)
            .Concat(Observable.Defer(() => hasEmitted
                ? Observable.Empty<T>()
                : Observable.Return(defaultValue)));
    });
```

Each subscription gets its own `index` / `hasEmitted` — cold observable semantics, no shared state, no locking.

### The Observable.Create Pattern

When you genuinely need `Observable.Create`, follow this structure. Notice the [§6.4](./rx-design-guide.instructions.md#64-protect-calls-to-user-code-from-within-an-operator) try/catch around the user selector and the [§6.16](./rx-design-guide.instructions.md#616-argument-validation-should-occur-outside-observablecreate) argument validation that would be required at the public extension method boundary.

```csharp
public static IObservable<TResult> MyOperator<TSource, TResult>(
    this IObservable<TSource> source,
    Func<TSource, TResult> selector)
{
    return Observable.Create<TResult>(observer =>
    {
        return source.SubscribeSafe(Observer.Create<TSource>(
            onNext: item =>
            {
                try
                {
                    var result = selector(item);
                    observer.OnNext(result);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }
            },
            onError: observer.OnError,
            onCompleted: observer.OnCompleted));
    });
}
```

### Multi-Source Operator Pattern

When combining multiple sources, serialize their notifications through a shared gate (per [§6.7](./rx-design-guide.instructions.md#67-serialize-calls-to-iobserver-methods-within-observable-sequence-implementations)):

```csharp
public static IObservable<T> MyMerge<T>(
    this IObservable<T> source1,
    IObservable<T> source2)
{
    return Observable.Create<T>(observer =>
    {
        var gate = new object();

        var sub1 = source1.Synchronize(gate).SubscribeSafe(observer);
        var sub2 = source2.Synchronize(gate).SubscribeSafe(observer);

        return new CompositeDisposable(sub1, sub2);
    });
}
```

**Note**: `Synchronize(gate)` holds the lock during downstream `OnNext` delivery. This ensures serialization but means the lock is held for the duration of all downstream processing. Keep downstream chains lightweight when using shared gates. DynamicData's `SynchronizeSafe` + `SharedDeliveryQueue` is the in-library alternative that releases the lock before downstream delivery (used to prevent cross-cache deadlocks).

### Operator Checklist

When writing or reviewing an Rx operator, walk this checklist alongside the rule audits in the design guide:

- [ ] **Serialized delivery** ([§4.2](./rx-design-guide.instructions.md#42-assume-observer-instances-are-called-in-a-serialized-fashion) / [§6.7](./rx-design-guide.instructions.md#67-serialize-calls-to-iobserver-methods-within-observable-sequence-implementations)): can `OnNext` be called concurrently? If multiple sources, are they serialized through the same gate?
- [ ] **Terminal semantics** ([§4.1](./rx-design-guide.instructions.md#41-assume-the-rx-grammar) / [§6.6](./rx-design-guide.instructions.md#66-onerror-messages-should-have-abort-semantics)): does `OnError`/`OnCompleted` propagate correctly? No notifications after terminal? No buffer flush on error?
- [ ] **Disposal** ([§4.4](./rx-design-guide.instructions.md#44-assume-a-best-effort-to-stop-all-outstanding-work-on-unsubscribe) / [§6.13](./rx-design-guide.instructions.md#613-hand-out-all-disposable-instances-created-inside-the-operator-to-consumers) / [§6.17](./rx-design-guide.instructions.md#617-unsubscription-should-be-idempotent) / [§6.18](./rx-design-guide.instructions.md#618-unsubscription-should-not-throw)): does disposing the subscription clean up all resources? Are all internal disposables exposed to the subscriber? Idempotent and non-throwing?
- [ ] **User code protection** ([§6.4](./rx-design-guide.instructions.md#64-protect-calls-to-user-code-from-within-an-operator)): are user-provided selectors / predicates / comparers wrapped in try/catch with errors routed to `OnError`?
- [ ] **Subscribe doesn't throw** ([§6.5](./rx-design-guide.instructions.md#65-subscribe-implementations-should-not-throw)): error conditions detected in subscribe go through `observer.OnError(...)` + `return Disposable.Empty;`?
- [ ] **Argument validation** ([§6.16](./rx-design-guide.instructions.md#616-argument-validation-should-occur-outside-observablecreate)): null checks happen before `Observable.Create`, not inside the subscribe lambda?
- [ ] **Back-pressure / buffers** ([§5.6](./rx-design-guide.instructions.md#56-consider-limiting-buffers)): does the operator buffer unboundedly? Could it cause memory issues?
- [ ] **Scheduler** ([§5.4](./rx-design-guide.instructions.md#54-consider-passing-a-specific-scheduler-to-concurrency-introducing-operators) / [§6.9–6.12](./rx-design-guide.instructions.md#69-parameterize-concurrency-by-providing-a-scheduler-argument)): time-dependent operations take a scheduler argument? Default uses `Scheduler.Immediate` where possible?
- [ ] **Cold/Hot**: is the observable cold (deferred via `Observable.Create` / `Observable.Defer`)? If hot, is sharing handled correctly via `Publish().RefCount()` (per [§5.10](./rx-design-guide.instructions.md#510-use-the-publish-operator-to-share-side-effects))?
- [ ] **Thread safety**: is mutable state protected? Are there race conditions between subscribe/dispose/OnNext?

## Common Pitfalls

Practical pitfalls that don't have direct PDF analogues but appear repeatedly in DynamicData code review.

### 1. Subscribing Multiple Times to a Cold Observable

```csharp
// WRONG: two HTTP calls!
var data = Observable.FromAsync(() => httpClient.GetAsync(url));
data.Subscribe(handler1);  // call 1
data.Subscribe(handler2);  // call 2 — probably not intended

// RIGHT: share the result (per §5.10)
var shared = data.Publish().RefCount();
shared.Subscribe(handler1);  // shares
shared.Subscribe(handler2);  // same result
```

### 2. Forgetting to Dispose Subscriptions

```csharp
// WRONG: subscription leaks — handler keeps running forever
source.Subscribe(x => UpdateUI(x));

// RIGHT: track and dispose
_cleanup.Add(source.Subscribe(x => UpdateUI(x)));
// In Dispose: _cleanup.Dispose();
```

### 3. Blocking on Rx (sync-over-async)

```csharp
// WRONG: blocks the thread, can hang on UI thread (see §6.14)
var result = source.FirstAsync().Wait();

// RIGHT: use async/await
var result = await source.FirstAsync();
```

### 4. Using Subject as a Public API

```csharp
// WRONG: exposes mutation to consumers
public Subject<int> Values { get; } = new();

// RIGHT: expose as IObservable, keep Subject private
private readonly Subject<int> _values = new();
public IObservable<int> Values => _values.AsObservable();
```

### 5. Not Handling OnError

```csharp
// WRONG: unhandled OnError crashes the app (routes to DefaultExceptionHandler — see §5.2)
source.Subscribe(x => Process(x));

// RIGHT: always handle errors
source.Subscribe(
    x => Process(x),
    ex => LogError(ex),
    () => LogComplete());
```
