---
applyTo: "**/*.cs"
---
# Reactive Extensions (Rx) — Comprehensive Guide

Reference: [ReactiveX Observable Contract](http://reactivex.io/documentation/contract.html) | [Rx.NET GitHub](https://github.com/dotnet/reactive) | [IntroToRx.com](http://introtorx.com/)

## Core Concepts

### Observables are Composable

Rx's power comes from composition. Every operator returns a new `IObservable<T>`, enabling fluent chaining:

```csharp
source
    .Where(x => x.IsValid)           // filter
    .Select(x => x.Transform())      // project
    .DistinctUntilChanged()           // deduplicate
    .ObserveOn(RxApp.MainThreadScheduler) // marshal to UI thread
    .Subscribe(x => UpdateUI(x));     // consume
```

Each operator in the chain is a separate subscription. Disposing the final subscription cascades disposal upstream through the entire chain. This composability is what makes Rx powerful — and what makes contract violations devastating, since a bug in any operator corrupts the entire downstream chain.

### Hot vs Cold Observables

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

**Converting**: `Publish()` + `Connect()` or `Publish().RefCount()` converts cold to hot (shared).

```csharp
var shared = coldSource.Publish().RefCount(); // auto-connect on first sub, auto-disconnect on last unsub
```

## The Observable Contract

### 1. Serialized Notifications (THE critical rule)

`OnNext`, `OnError`, and `OnCompleted` calls MUST be serialized — they must never execute concurrently. This is the most commonly violated rule and causes the most insidious bugs.

```csharp
// WRONG: two sources can call OnNext concurrently
source1.Subscribe(x => observer.OnNext(Process(x)));  // thread A
source2.Subscribe(x => observer.OnNext(Process(x)));  // thread B — RACE!

// RIGHT: use Synchronize to serialize
source1.Synchronize(gate).Subscribe(observer);
source2.Synchronize(gate).Subscribe(observer);

// RIGHT: use Merge (serializes internally)
source1.Merge(source2).Subscribe(observer);

// RIGHT: use Subject (serializes OnNext calls via Synchronize)
var subject = new Subject<T>();
source1.Subscribe(subject);  // Subject.OnNext is NOT thread-safe by default!
// Use Subject with Synchronize if multiple threads call OnNext
```

**Why it matters**: operators maintain mutable internal state (caches, dictionaries, counters). Concurrent `OnNext` calls corrupt this state silently — no exception, just wrong data.

### 2. Terminal Notifications

```
Grammar: OnNext* (OnError | OnCompleted)?
```

- Zero or more `OnNext`, followed by at most one terminal notification
- `OnError` and `OnCompleted` are **mutually exclusive** — emit one or neither, never both
- After a terminal notification, **no further notifications** of any kind
- Operators receiving a terminal notification should release resources

### 3. Subscription Lifecycle

- `Subscribe` returns `IDisposable` — disposing it **unsubscribes**
- After disposal, no further notifications should be delivered
- Disposal must be **idempotent** (safe to call multiple times) and **thread-safe**
- Operators should stop producing when their subscription is disposed

### 4. Error Handling

- Exceptions thrown inside `OnNext` handlers propagate synchronously to the producing operator
- Use `SubscribeSafe` instead of `Subscribe` to route subscriber exceptions to `OnError`:

```csharp
// Subscribe: exception in handler crashes the source
source.Subscribe(x => MayThrow(x));  // if MayThrow throws, exception propagates up

// SubscribeSafe: exception in handler routes to OnError
source.SubscribeSafe(Observer.Create<T>(
    onNext: x => MayThrow(x),
    onError: ex => HandleError(ex)));  // MayThrow exception goes here
```

## Schedulers

Schedulers control **when** and **where** work executes. They are Rx's abstraction over threading.

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

**Always inject schedulers** instead of using defaults. This enables deterministic testing:

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

Rx provides several `IDisposable` implementations for managing subscription lifecycles:

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

A no-op disposable. Useful as a default or placeholder.

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

Holds a single disposable that can be **replaced**. Disposing the previous value when a new one is set. Useful for "switch" patterns.

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

Like SerialDisposable but can only be assigned **once**. Throws on second assignment. Useful when a subscription is created asynchronously but disposal might happen before it's ready.

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

Tracks multiple "dependent" disposables. The underlying resource is only disposed when **all** dependents (plus the primary) are disposed.

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
| `Synchronize()` | Serialize notifications with internal gate |
| `Synchronize(gate)` | Serialize notifications with external gate object |

### Utility

| Operator | Description |
|----------|-------------|
| `Do(action)` | Perform side effect for each notification |
| `Publish().RefCount()` | Share a subscription among multiple subscribers |
| `Replay(bufferSize).RefCount()` | Share with replay |
| `AsObservable()` | Hide the implementation type (e.g., Subject → IObservable) |
| `Subscribe(observer)` | Subscribe with an IObserver |
| `Subscribe(onNext, onError, onCompleted)` | Subscribe with callbacks |
| `SubscribeSafe(observer)` | Subscribe with exception routing to OnError |
| `ForEachAsync(action)` | Async iteration (returns Task) |
| `Wait()` | Block until complete (avoid on UI thread) |
| `ToTask()` | Convert to Task (last value) |

## Writing Custom Operators

### The Observable.Create Pattern

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

When combining multiple sources, serialize their notifications:

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

**Note**: `Synchronize(gate)` holds the lock during downstream `OnNext` delivery. This ensures serialization but means the lock is held for the duration of all downstream processing. Keep downstream chains lightweight when using shared gates.

### Operator Checklist

When writing or reviewing an Rx operator:

- [ ] **Serialized delivery**: can `OnNext` be called concurrently? If multiple sources, are they serialized?
- [ ] **Terminal semantics**: does `OnError`/`OnCompleted` propagate correctly? No notifications after terminal?
- [ ] **Disposal**: does disposing the subscription clean up all resources? Is it idempotent?
- [ ] **Error handling**: does `SubscribeSafe` catch subscriber exceptions? Are errors propagated, not swallowed?
- [ ] **Back-pressure**: does the operator buffer unboundedly? Could it cause memory issues?
- [ ] **Scheduler**: are time-dependent operations using an injectable scheduler?
- [ ] **Cold/Hot**: is the observable cold (deferred via `Observable.Create`)? If hot, is sharing handled correctly?
- [ ] **Thread safety**: is mutable state protected? Are there race conditions between subscribe/dispose/OnNext?

## Common Pitfalls

### 1. Subscribing Multiple Times to a Cold Observable

```csharp
// WRONG: two HTTP calls!
var data = Observable.FromAsync(() => httpClient.GetAsync(url));
data.Subscribe(handler1);  // call 1
data.Subscribe(handler2);  // call 2 — probably not intended

// RIGHT: share the result
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
// WRONG: blocks the thread, can hang on UI thread
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
// WRONG: unhandled OnError crashes the app (routes to DefaultExceptionHandler)
source.Subscribe(x => Process(x));

// RIGHT: always handle errors
source.Subscribe(
    x => Process(x),
    ex => LogError(ex),
    () => LogComplete());
```
