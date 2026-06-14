---
applyTo: "**/*.cs"
---
# Rx Design Guide

A complete distillation of the **[Microsoft Rx Design Guidelines v1.0 (October 2010)](https://go.microsoft.com/fwlink/?LinkID=205219)** (canonical fwlink; resolves to `download.microsoft.com/.../Rx Design Guidelines.pdf`). This is the authoritative reference for the Rx contract and the rules for using Rx and implementing Rx operators in DynamicData.

**This document is meant to be self-contained.** Future contributors and agents should not have to re-consume the PDF: the entire spec (every rule, every "When to ignore" caveat, the clarifying samples) is preserved here. Samples have been modernized to current Rx.NET API names; the PDF's original 2010 names appear in the Modernization notes table below.

**Every operator added or modified must self-audit against these rules. Every bug fix must explicitly state which rules were verified.**

Rules use the **original Microsoft document's `§X.Y` numbering** throughout so citations are unambiguous and traceable to the source spec. Cite rule IDs in code review comments, PR descriptions, and commit messages (e.g., "Fixes §6.6 violation in `BatchIf`"). Always use the literal `§` character — never ASCII substitutes (`S`, `SS`, `sec`, etc.).

---

## Document structure

The file follows the original Microsoft document's section order:
- **§2** introduces what the guidelines are and how to read them.
- **§3** is "When to use Rx" — applicability guidelines for choosing Rx in the first place.
- **§4** is the Observable Contract from the consumer's perspective (what callers can rely on).
- **§5** is rules for code that uses Rx (consumer-side correctness).
- **§6** is rules for operator authors (what your operator must guarantee).

§1 (the PDF's table of contents) is omitted here as it carries no rules.

---

## Modernization notes

This document is derived from a 2010 specification but uses **current Rx.NET API names throughout**. Where the original PDF references obsolete names, this document uses the modern equivalents. Do NOT use the obsolete names in new code or fixes:

| PDF / 2010 name (obsolete) | Modern equivalent |
|---|---|
| `Observable.CreateWithDisposable<T>(Func<IObserver<T>, IDisposable>)` | `Observable.Create<T>(Func<IObserver<T>, IDisposable>)` (same signature, name unified) |
| `Observable.Create<T>` (2010 variant taking `Func<IObserver<T>, Action>`) | Still exists, but prefer the `IDisposable`-returning overload for resource lifetime correctness |
| `MutableDisposable` | `SerialDisposable` |
| `Scheduler.Dispatcher` | `DispatcherScheduler.Current` (WPF/WinForms) or platform-specific |
| `Scheduler.ThreadPool` | `Scheduler.Default` or `TaskPoolScheduler.Default` |
| `Scheduler.NewThread` | `NewThreadScheduler.Default` |
| `Observable.FromEvent<TDelegate, TArgs>(d => d.Invoke, add, remove)` (3-arg form) | `Observable.FromEventPattern<TArgs>(add, remove)` (typed event pattern) |
| `Observable.Start(Func<T>)` for async work | `Observable.FromAsync(Func<Task<T>>)` for proper async/await integration |
| `IScheduler.Schedule(Action)` (interface method) | `IScheduler.Schedule(this IScheduler, Action)` extension method (same usage, now an extension) |

Modern additions worth knowing (not in the 2010 PDF):
- **`SingleAssignmentDisposable`**: assign once, throws on second assignment. Useful when a disposable is wired up asynchronously after subscription returns.
- **`RefCountDisposable`**: reference-counted disposable; underlying resource disposed only when all dependents released.
- **`IScheduler.Schedule<TState>(TState, Func<IScheduler, TState, IDisposable>)`** generic overload: avoids closure allocation in hot paths. Prefer this in performance-sensitive operators.
- **`ValueTask<T>` / `IAsyncEnumerable<T>` interop**: `ToObservable()`, `ToAsyncEnumerable()` bridges.
- **`System.Threading.Lock`** (NET9+): modern lock type used by DynamicData's `Rxx.Synchronize` on NET9+.

**Keep this table current.** When you encounter a 2010 API name in the PDF that doesn't yet appear here (because Rx.NET has since renamed, removed, or supplemented it), add a row. If you find an obsolete API name used anywhere in this codebase, that is itself a finding to surface.

---

## §2: Introduction

These guidelines aid in developing applications and libraries that use Rx. They have evolved over time during the development of the Rx library, and continue to evolve as Rx evolves.

The guidelines are **not absolute truths.** They are patterns the Rx team found helpful, not rules to be followed blindly. There are situations where certain guidelines do not apply. The PDF lists known exceptions for each rule in its "When to ignore this guideline" sections, which are preserved here verbatim.

The guidelines are listed in **no particular order.** There is neither total nor partial ordering between them. The §X.Y numbering is a stable ID for citation purposes only, not a priority ranking.

---

## §3: When to use Rx

### §3.1. Use Rx for orchestrating asynchronous and event-based computations

Code that deals with more than one event or asynchronous computation gets complicated quickly: it needs a state machine for ordering, and explicit handling of success and failure termination for every separate computation. The result is code that doesn't follow normal control flow, is hard to understand, and is hard to maintain.

Rx makes these computations first-class citizens. This provides a model for readable, composable APIs over asynchronous computations.

**Sample (modernized to current Rx.NET API)** — autocomplete-style "dictionary suggest" that throttles user input and cancels stale lookups:
```csharp
var keyUp = Observable.FromEventPattern<KeyEventArgs>(textBox, nameof(textBox.KeyUp));

var dictionarySuggest = keyUp
    .Select(_ => textBox.Text)
    .Where(text => !string.IsNullOrEmpty(text))
    .DistinctUntilChanged()
    .Throttle(TimeSpan.FromMilliseconds(250), uiScheduler)
    .SelectMany(text => AsyncLookupInDictionary(text).TakeUntil(keyUp));

dictionarySuggest.Subscribe(
    results => listView.Items.AddRange(results.Select(r => new ListViewItem(r)).ToArray()),
    error => LogError(error));
```
`Throttle` collapses bursts of keystrokes, `SelectMany` flattens the per-text async lookups, `TakeUntil(keyUp)` cancels in-flight lookups when the user types again. In imperatively-written code each of these would be a separate timer or callback with explicit exception bookkeeping.

**When to ignore this guideline:** if the application has very few asynchronous/event-based operations or few places where they need to be composed, the cost of depending on Rx (redistribution and the learning curve) might outweigh the cost of coding the operations manually.

### §3.2. Use Rx to deal with asynchronous sequences of data

Several other .NET asynchronous libraries exist, but most work best for operations that return a single message. They usually do not support operations that produce **multiple** messages over the lifetime of the operation.

Rx follows the grammar `OnNext* (OnCompleted | OnError)?` (see §4.1). This makes Rx suitable both for operations that produce a single message and operations that produce multiple messages.

**Sample (modernized)** — pipelined encryption of a 4 GB file using 64K blocks (no full-file buffering):
```csharp
inFile.AsyncRead(blockSize: 2 << 15)
      .Select(Encrypt)
      .WriteToStream(outFile)
      .Subscribe(
          _ => Console.WriteLine("Successfully encrypted the file."),
          error => Console.WriteLine($"An error occurred while encrypting: {error.Message}"));
```
Each 64K block flows through the pipeline as a separate `OnNext`. Encryption runs per block; writes happen as soon as a block is encrypted. Memory usage stays bounded, throughput stays high.

**When to ignore this guideline:** if the application has very few operations with multiple messages, the cost of depending on Rx might outweigh the cost of coding them manually.

---

## §4: The Rx contract

`IObservable<T>` and `IObserver<T>` only specify their methods' arguments and return types. The Rx library makes additional assumptions about these interfaces that are not expressible in the .NET type system. These assumptions form a contract that **all producers and consumers** of Rx types must follow. The contract makes it possible to reason about and prove the correctness of operators and user code.

### §4.1. Assume the Rx Grammar

Messages sent to instances of `IObserver` follow the grammar:
```
OnNext* (OnCompleted | OnError)?
```
- Zero or more `OnNext`, optionally followed by exactly one terminal notification.
- `OnError` and `OnCompleted` are **mutually exclusive**: emit one or the other, never both.
- After a terminal notification, **no further notifications of any kind**, not even another terminal.

The single terminal message ensures that consumers can deterministically establish when it is safe to perform cleanup. A single failure further ensures that abort semantics can be maintained for operators that work on multiple observable sequences (see §6.6).

**When to ignore this guideline:** only when working with a non-conforming `IObservable` implementation. Such a source can be made conforming by calling `Synchronize()` on it (per §5.8).

### §4.2. Assume observer instances are called in a serialized fashion

Rx uses a push model and .NET supports multithreading, so without serialization different messages could arrive on different execution contexts at the same time. Forcing every consumer to handle this would require pervasive housekeeping, hurt maintainability, and harm performance.

Only the operators that produce multi-source observable sequences are required to perform serialization (see §6.7). **Consumers can safely assume that messages on a single observer arrive in a serialized fashion.**

```csharp
var count = 0;
xs.Subscribe(v =>
{
    count++;
    Console.WriteLine($"OnNext has been called {count} times.");
});
```
No locking or interlocking is required to read or write `count`: only one call to `OnNext` can be in-flight at a time.

**When to ignore this guideline:** if you have to consume a custom `IObservable` implementation that doesn't follow the contract for serialization, use `Synchronize()` to restore the guarantee.

### §4.3. Assume resources are cleaned up after an OnError or OnCompleted message

§4.1 states that no more messages should arrive after a terminal. This makes it possible to clean up any resources used by the subscription the moment the terminal arrives. Cleaning up immediately makes side effects predictable and lets the runtime reclaim resources promptly.

```csharp
Observable.Using(
    () => new FileStream(path, FileMode.Create),
    fs => Observable.Range(0, 10000)
        .Select(v => Encoding.ASCII.GetBytes(v.ToString()))
        .WriteToStream(fs))
    .Subscribe();
```
`Using` creates a resource that will be disposed upon unsubscription. The cleanup guarantee ensures unsubscription is called automatically once a terminal arrives.

**When to ignore this guideline:** none known.

### §4.4. Assume a best effort to stop all outstanding work on Unsubscribe

When `Dispose` is called on a subscription, the source makes a best-effort attempt to stop all outstanding work.
- Queued work that has not yet started is cancelled.
- Work already in progress **may** still complete (it is not always safe to abort), but its results **MUST NOT** be signaled to the unsubscribed observer.
- Messages may arrive during the `Dispose` call itself (Dispose can race with `OnNext`).
- After `Dispose` returns control to the caller: no more messages arrive.
- The unsubscription process may continue asynchronously on a different context after `Dispose` returns.

```csharp
// Sample 1: queued work is cancelled — observer never fires
Observable.Timer(TimeSpan.FromSeconds(2)).Subscribe(...).Dispose();

// Sample 2: in-progress work runs to completion, but its result is dropped
Observable.Start(() => { Thread.Sleep(TimeSpan.FromSeconds(2)); return 5; })
          .Subscribe(...).Dispose();
```

---

## §5: Using Rx

These rules govern code that consumes Rx. They apply recursively inside operator implementations too, per §6.20.

### §5.1. Consider drawing a Marble-diagram

Draw a marble diagram of the observable sequence you want to create. By drawing the diagram, you get a clearer picture of which operators to use.

A marble diagram shows events occurring over time, with both input and output sequences. Sketching one often makes the answer obvious: a "delay then call" pattern maps to `Throttle`, a "create a new sequence per input" pattern maps to `SelectMany`, and so on.

**When to ignore this guideline:** when you are comfortable enough with the sequence you want to write. Even Rx team members still reach for a whiteboard occasionally.

### §5.2. Consider passing multiple arguments to Subscribe

Rx provides Subscribe overloads that take delegates instead of an `IObserver`, because C# and VB do not support anonymous inner classes. These overloads use defaults for any method you omit.

The single-argument `Subscribe(onNext)` overload **rethrows OnError on the thread the message arrives on**, crashing the application. The no-argument `OnCompleted` default is a no-op. In most situations, dealing with the exception (either recovering or aborting gracefully) and knowing the sequence completed successfully are both important, so it is best to provide all three arguments.

**When to ignore this guideline:**
- The observable sequence is guaranteed not to complete (e.g. a UI event like `KeyUp`).
- The observable sequence is guaranteed not to error (e.g. an event, a materialized sequence, etc.).
- The default behavior is the desired behavior.

### §5.3. Consider using LINQ query expression syntax

Rx implements the query expression pattern, so LINQ query syntax can be used over observable sequences:
```csharp
// Method syntax
var r = xs.SelectMany(x => ys, (x, y) => x + y);

// Equivalent query syntax
var r1 = from x in xs
         from y in ys
         select x + y;
```

**When to ignore this guideline:** if your query uses many operators that don't have query-syntax equivalents, the mixed style may negate the readability benefit.

### §5.4. Consider passing a specific scheduler to concurrency-introducing operators

Rather than using `ObserveOn` to change execution context, create concurrency on the right scheduler from the start. Operators that introduce concurrency provide a scheduler argument overload. Passing the right scheduler upfront eliminates downstream `ObserveOn` hops.

```csharp
var keyUp = Observable.FromEventPattern<KeyEventArgs>(textBox, nameof(textBox.KeyUp));
var throttled = keyUp.Throttle(TimeSpan.FromSeconds(1), DispatcherScheduler.Current);
```
Without the explicit scheduler, the default `Throttle` overload would deliver `OnNext` from a ThreadPool timer. Passing the dispatcher scheduler keeps all messages on the UI thread.

**When to ignore this guideline:** when combining several events that originate on different execution contexts, use §5.5 to put all messages on a specific context as late as possible.

### §5.5. Call the ObserveOn operator as late and in as few places as possible

`ObserveOn` schedules an action per message. This changes timing and adds load. Placing it later in the query (after filtering) reduces both concerns.

```csharp
var result =
    (from x in xs.Throttle(TimeSpan.FromSeconds(1))
     from y in ys.TakeUntil(zs).Sample(TimeSpan.FromMilliseconds(250))
     select x + y)
    .Merge(ws)
    .Where(x => x.Length < 10)
    .ObserveOn(DispatcherScheduler.Current);
```
Placing `ObserveOn` earlier would do scheduling work for messages that the `Where` filter throws away.

**When to ignore this guideline:** if your use of the observable sequence is not bound to a specific execution context, do not use `ObserveOn` at all.

### §5.6. Consider limiting buffers

Rx comes with operators that buffer over observable sequences (e.g. `Replay`). The buffer size scales with the input sequence. If unbounded, the buffer causes memory pressure.

Most buffering operators provide policies to limit the buffer by time or size:
```csharp
var result = xs.Replay(bufferSize: 10000, window: TimeSpan.FromHours(1));
```

**When to ignore this guideline:** when the number of messages populating the buffer is small, or when the buffer is already bounded by its surrounding context.

### §5.7. Make side-effects explicit using the Do operator

Rx operators take delegates as arguments, and it is possible to put side-effecting code (writes to disk, mutation of global state, logging) into those delegates. Because Rx composition runs each operator for each subscription (except sharing operators like `Publish`), every side-effect occurs for every subscription.

If you want side effects per subscription, **make that explicit by putting the side-effecting code in a `Do` operator** so it's visible in the pipeline.

```csharp
var result = xs.Where(x => x.Failed).Do(x => Log(x)).Subscribe(...);
```

**When to ignore this guideline:** when the side effect needs data from an operator that is not available to a separate `Do`.

### §5.8. Use the Synchronize operator only to "fix" custom IObservable implementations

Observable sequences created by Rx operators already follow the contract for grammar (§4.1) and serialization (§4.2). Adding `Synchronize` to one of them is redundant and counterproductive. **Only use `Synchronize` on observable sequences created by other sources that do not follow the contract.**

```csharp
var result = from x in customNonConformingSource.Synchronize()
             from y in ys
             where x > y
             select y;
```

**When to ignore this guideline:** none known.

> NOTE: this guideline refers to the **single-argument `Synchronize()`** that wraps a non-conforming source. The **gate-based `Synchronize(gate)`** used inside multi-source operators to satisfy §6.7 is a different pattern and is valid.

### §5.9. Assume messages can come through until unsubscribe has completed

Because Rx uses a push model, messages can be sent from different execution contexts and can be in flight while `Dispose` is called. These messages **may still come through during the call to `Dispose`**. After `Dispose` returns control to the caller, no more messages will arrive. The unsubscription process itself may still be in progress on a different context.

**When to ignore this guideline:** once `OnCompleted` or `OnError` has been received, the Rx grammar guarantees the subscription is finished, so this concern doesn't apply.

### §5.10. Use the Publish operator to share side-effects

Most observable sequences are cold: each subscription gets its own set of side effects. Some situations require side effects to happen only once. `Publish` shares a single underlying subscription among multiple subscribers via multicast.

The most convenient overloads are the ones that accept a function with a shared observable sequence as an argument:
```csharp
var xs = Observable.Create<string>(observer =>
{
    Console.WriteLine("Side effect");
    observer.OnNext("Hi");
    observer.OnCompleted();
    return Disposable.Empty;
});

xs.Publish(sharedXs =>
{
    sharedXs.Subscribe(Console.WriteLine);
    sharedXs.Subscribe(Console.WriteLine);
    return sharedXs;
}).Run();
```
The "Side effect" line prints once, not twice, because both subscribers share a single subscription to `xs`.

**When to ignore this guideline:** only use `Publish` when sharing is required. In most situations either the subscriptions have no side effects, or the side effects can execute multiple times without issues, and the extra machinery of `Publish` is unnecessary.

---

## §6: Operator implementations

### §6.1. Implement new operators by composing existing operators

Many operations can be composed from existing operators. This leads to smaller, easier-to-maintain code. The Rx team has put significant effort into corner cases in the base operators; composing them reuses that work for free.

```csharp
public static IObservable<TResult> SelectMany<TSource, TResult>(
    this IObservable<TSource> source,
    Func<TSource, IObservable<TResult>> selector)
{
    return source.Select(selector).Merge();
}
```
`Select` already handles selector-thrown exceptions; `Merge` already handles serialization of inner sequences firing concurrently. The composed `SelectMany` gets both for free.

**When to ignore this guideline:**
- No appropriate set of base operators exists.
- Performance analysis proves the composed implementation has performance issues.

### §6.2. Implement custom operators using Observable.Create

When you cannot follow §6.1, use `Observable.Create` to create the observable sequence. `Observable.Create` provides several contract-protection benefits:
- When the sequence finishes (`OnError` or `OnCompleted`), any subscription is automatically unsubscribed.
- Any subscribed observer instance will only see a single terminal message. No further messages are sent, enforcing the grammar of §4.1.

> **Modernization note:** the PDF refers to `Observable.CreateWithDisposable<T>(Func<IObserver<T>, IDisposable>)`. In modern Rx.NET this is `Observable.Create<T>(Func<IObserver<T>, IDisposable>)` (same signature, name unified). There is also an `Action`-returning overload; prefer the `IDisposable`-returning one for resource lifetime correctness.

**When to ignore this guideline:**
- The operator needs to return an observable sequence that doesn't follow the Rx contract (rare; usually only for testing how code behaves under broken contracts).
- The returned object must implement more than `IObservable` (e.g. `ISubject` or a custom class).

### §6.3. Implement operators for existing observable sequences as generic extension methods

An operator becomes more powerful when it can be applied widely. Implement operators as **extension methods** so they appear in IntelliSense on any existing observable sequence, and make them **generic** so they work regardless of element type.

**When to ignore this guideline:**
- The operator doesn't work on a source observable sequence.
- The operator works on a specific data shape and cannot be made generic.

### §6.4. Protect calls to user code from within an operator

When user code is called from within an operator, the call may happen outside the execution context of the original operator invocation (e.g. asynchronously on a scheduler). Any exception that escapes will terminate the program unexpectedly. Instead, **catch and route to `observer.OnError`** so subscribers can handle it.

Common kinds of user code that must be protected:
- Selector functions passed to the operator.
- Predicates passed to the operator.
- Comparers passed to the operator.
- Key selectors passed to the operator.
- Action callbacks (`Do`, `OnItemRemoved`, `SubscribeMany`, etc.)
- Calls to dictionaries, lists, and hashsets that use a user-provided comparer.

```csharp
return Observable.Create<TResult>(observer => source.Subscribe(
    x =>
    {
        TResult result;
        try { result = selector(x); }
        catch (Exception exception)
        {
            observer.OnError(exception);
            return;
        }
        observer.OnNext(result);
    },
    observer.OnError,
    observer.OnCompleted));
```

> NOTE: calls to `IScheduler` implementations are **not** considered for this guideline. Most schedulers deal with asynchronous calls, so only a small set of issues would be caught at the call site. Instead, protect the arguments passed to schedulers **inside each scheduler implementation**.

**Edge of the monad:** do **not** wrap calls to `Subscribe`, `Dispose`, `OnNext`, `OnError`, or `OnCompleted`. Calling `OnError` from these places leads to undefined behavior.

**When to ignore this guideline:** for calls to user code made before creating the observable sequence (outside `Observable.Create`). Those calls are on the current execution context and follow normal control flow.

### §6.5. Subscribe implementations should not throw

Subscribe may be called asynchronously (e.g. the second source argument to `Concat` is subscribed to only after the first source completes). A throw at that moment would bring down the program because there is no observer in scope to route to. Instead, **error conditions detected inside `Subscribe` must be routed via `observer.OnError(...)` followed by `return Disposable.Empty;`**.

```csharp
public IObservable<byte[]> ReadSocket(Socket socket) =>
    Observable.Create<byte[]>(observer =>
    {
        if (!socket.Connected)
        {
            observer.OnError(new InvalidOperationException("the socket is no longer connected"));
            return Disposable.Empty;
        }
        // ... rest of subscribe ...
    });
```

**When to ignore this guideline:** when a catastrophic error occurs that should bring the whole program down anyway.

### §6.6. OnError messages should have abort semantics

Normal .NET control flow uses abort semantics for exceptions: the stack is unwound, current code is interrupted. Rx mimics this. Once a source emits `OnError`, **the operator MUST emit no further messages — not even buffered or aggregated state.**

The canonical violation: a buffering operator that "salvages" its buffer into a final `OnNext` on `OnError`. The buffer must be **discarded**:
```csharp
public static IObservable<byte[]> MinimumBuffer(this IObservable<byte[]> source, int bufferSize) =>
    Observable.Create<byte[]>(observer =>
    {
        var data = new List<byte>();
        return source.Subscribe(
            value =>
            {
                data.AddRange(value);
                if (data.Count > bufferSize)
                {
                    observer.OnNext(data.ToArray());
                    data.Clear();
                }
            },
            observer.OnError,
            () =>
            {
                if (data.Count > 0)
                    observer.OnNext(data.ToArray());
                observer.OnCompleted();
            });
    });
```
The `OnCompleted` branch flushes the buffer (success path), but the `OnError` branch is direct passthrough: it abandons the buffer. The semantic is **abort**, not graceful degradation.

Operators that aggregate state across multiple `OnNext` (Sort, Group, Page, etc.) must not salvage that state on `OnError`.

**When to ignore this guideline:** none known.

### §6.7. Serialize calls to IObserver methods within observable sequence implementations

Rx is composable: many operators play together. If every operator had to deal with concurrency individually, they would become very complex. Concurrency is best controlled at the place it first occurs. Consuming Rx would become harder if every consumer had to deal with concurrency too.

**When combining multiple sources into one output observer, serialize all three notification types through a shared gate**:
```csharp
public static IObservable<TResult> ZipEx<TLeft, TRight, TResult>(
    this IObservable<TLeft> left,
    IObservable<TRight> right,
    Func<TLeft, TRight, TResult> resultSelector)
{
    return Observable.Create<TResult>(observer =>
    {
        var group = new CompositeDisposable();
        var gate = new object();
        var leftQ = new Queue<TLeft>();
        var rightQ = new Queue<TRight>();

        group.Add(left.Subscribe(
            value =>
            {
                lock (gate)
                {
                    if (rightQ.Count > 0)
                    {
                        var rightValue = rightQ.Dequeue();
                        TResult result;
                        try { result = resultSelector(value, rightValue); }
                        catch (Exception e) { observer.OnError(e); return; }
                        observer.OnNext(result);
                    }
                    else
                    {
                        leftQ.Enqueue(value);
                    }
                }
            },
            observer.OnError,
            observer.OnCompleted));

        // ... symmetric handler for `right` using the same `gate` ...

        return group;
    });
}
```
Two sources, one gate. **All three notification types — `OnNext`, `OnError`, `OnCompleted` — must be serialized through the same gate**, not just `OnNext`. Without this, the operator's internal state (the two queues) could be corrupted by interleaved deliveries.

The equivalent pattern using `Synchronize(gate)` is more concise:
```csharp
var gate = new object();
source1.Synchronize(gate).Subscribe(observer);
source2.Synchronize(gate).Subscribe(observer);
```
DynamicData's `CacheParentSubscription._synchronize` is the canonical example for cache operators.

**When to ignore this guideline:**
- The operator works on a single source observable sequence (then §6.8 applies).
- The operator does not introduce concurrency.
- Other constraints guarantee no concurrency is in play.

> NOTE: If a source observable sequence breaks the contract, a developer can fix it before passing it to an operator by calling `Synchronize()` (per §5.8).

### §6.8. Avoid serializing operators

Per §6.7 every operator already serializes; downstream operators can **assume** serialized input. Adding `Synchronize` "just in case" clutters the code, harms performance, and signals misunderstanding of the contract.

If a source isn't following the contract, fix it at the consumer boundary with `Synchronize()` (per §5.8). The scope of additional synchronization should be limited to where it is needed.

```csharp
// Select assumes its source follows §6.7 and requires no additional locking
public static IObservable<TResult> Select<TSource, TResult>(
    this IObservable<TSource> source, Func<TSource, TResult> selector) =>
    Observable.Create<TResult>(observer => source.Subscribe(
        x =>
        {
            TResult result;
            try { result = selector(x); }
            catch (Exception e) { observer.OnError(e); return; }
            observer.OnNext(result);
        },
        observer.OnError,
        observer.OnCompleted));
```

**When to ignore this guideline:** none known.

### §6.9. Parameterize concurrency by providing a scheduler argument

There are many different notions of concurrency, and no scenario fits all, so it is best to **parameterize the concurrency an operator introduces** via the `IScheduler` interface.

```csharp
public static IObservable<TValue> Return<TValue>(TValue value, IScheduler scheduler) =>
    Observable.Create<TValue>(observer =>
        scheduler.Schedule(() =>
        {
            observer.OnNext(value);
            observer.OnCompleted();
        }));
```

**When to ignore this guideline:**
- The operator is not in control of creating the concurrency (e.g. an operator converting an event to an observable: the event source is in control, not the operator).
- The operator is in control but needs to use a specific scheduler for introducing concurrency.

### §6.10. Provide a default scheduler

In most cases there is a good default scheduler. Providing it as an overload makes calling code more succinct.

```csharp
public static IObservable<TValue> Return<TValue>(TValue value) =>
    Return(value, Scheduler.Immediate);
```

> NOTE: when choosing the default, follow §6.12 — use `Scheduler.Immediate` where possible, only choose a scheduler with more concurrency when needed.

**When to ignore this guideline:** when no good default can be chosen.

### §6.11. The scheduler should be the last argument to the operator

Putting the scheduler last makes the operator fluent in IntelliSense. Combined with §6.10's default-providing overload, adding or omitting a scheduler becomes natural without changing argument order.

```csharp
public static IObservable<TValue> Return<TValue>(TValue value) => Return(value, Scheduler.Immediate);
public static IObservable<TValue> Return<TValue>(TValue value, IScheduler scheduler) => /* ... */;
```

**When to ignore this guideline:** C# and VB `params` syntax requires the `params` argument to be last. For `params T[]` operators, make the scheduler the **second-to-last** argument instead.

### §6.12. Avoid introducing concurrency

Adding concurrency changes the timeliness of a sequence: messages are scheduled to arrive later, and **delivery time is itself observable data**. Adding concurrency skews that data.

This guideline includes not transferring control to a different context such as the UI context.

```csharp
// Select does not use a scheduler — it stays on the source's OnNext call,
// staying in the same time window.
public static IObservable<TResult> Select<TSource, TResult>(
    this IObservable<TSource> source, Func<TSource, TResult> selector) =>
    Observable.Create<TResult>(observer => source.Subscribe(
        x =>
        {
            try { observer.OnNext(selector(x)); }
            catch (Exception e) { observer.OnError(e); }
        },
        observer.OnError,
        observer.OnCompleted));

// Return defaults to Scheduler.Immediate — no concurrency introduced.
public static IObservable<TValue> Return<TValue>(TValue value) => Return(value, Scheduler.Immediate);
```

**When to ignore this guideline:** when introducing concurrency is an essential part of what the operator does.

> NOTE: When using `Scheduler.Immediate` or calling the observer directly from within `Subscribe`, the `Subscribe` call becomes blocking. Any expensive computation in that situation is a candidate for introducing concurrency.

### §6.13. Hand out all disposable instances created inside the operator to consumers

Disposable instances control subscription lifetime and cancellation of scheduled actions. Rx gives users an opportunity to unsubscribe via disposable instances. After a subscription has ended, no more messages are allowed through, and any leftover state inside the sequence is inefficient and can lead to unexpected semantics.

To compose multiple disposable instances, Rx provides the `System.Reactive.Disposables` namespace:

| Name | Description |
|---|---|
| `CompositeDisposable` | Composes and disposes a group of disposable instances together. |
| `SerialDisposable` *(modern; PDF says `MutableDisposable`)* | A holder for a replaceable disposable; assigning a new disposable disposes the previous. |
| `BooleanDisposable` | Maintains state on whether disposing has occurred. |
| `CancellationDisposable` | Wraps `CancellationToken` into the disposable pattern. |
| `ContextDisposable` | Disposes an underlying disposable in a specified `SynchronizationContext`. |
| `ScheduledDisposable` | Uses a scheduler to dispose an underlying disposable. |
| `SingleAssignmentDisposable` *(modern, not in PDF)* | Assignable once; throws on second assignment. Useful when the disposable is wired up asynchronously after subscription returns. |
| `RefCountDisposable` *(modern, not in PDF)* | Reference-counted disposable; underlying resource disposed only when all dependents released. |

```csharp
// Hand the group of internal subscriptions back to the subscriber via a CompositeDisposable
return Observable.Create<TResult>(observer =>
{
    var group = new CompositeDisposable();
    group.Add(left.Subscribe(/* ... */));
    group.Add(right.Subscribe(/* ... */));
    return group;
});
```

**When to ignore this guideline:** none known.

### §6.14. Operators should not block

Rx is a library for composing asynchronous and event-based programs over observable collections. A blocking operator loses those characteristics, and potentially loses composability (e.g. by returning `T` instead of `IObservable<T>`).

```csharp
public static IObservable<int> Sum(this IObservable<int> source) =>
    source.Aggregate(0, (prev, curr) => checked(prev + curr));
```
`Sum` returns `IObservable<int>` rather than `int`. It does not block, and the result remains composable. If the developer wants to escape the observable world, they can use `First*`, `Last*`, or `Single*`.

**When to ignore this guideline:** none known.

### §6.15. Avoid deep stacks caused by recursion in operators

Code inside Rx operators can be called from different execution contexts in many different scenarios. It is nearly impossible to establish how deep the stack already is. A recursive operator can trigger stack overflow much sooner than expected.

Two recommended ways to avoid this:
- Use the **recursive `Schedule` extension method** on `IScheduler`.
- Implement an **infinite-looping `IEnumerable<IObservable<T>>`** using `yield` and convert it via `Concat`.

```csharp
// Sample 1: scheduler-based recursion
public static IObservable<TSource> Repeat<TSource>(TSource value, IScheduler scheduler) =>
    Observable.Create<TSource>(observer =>
        scheduler.Schedule(self =>
        {
            observer.OnNext(value);
            self();
        }));

// Sample 2: yield iterator + Concat
public static IObservable<TSource> Repeat<TSource>(TSource value) =>
    RepeatHelper(value).Concat();

private static IEnumerable<IObservable<TSource>> RepeatHelper<TSource>(TSource value)
{
    while (true)
        yield return Observable.Return(value);
}
```
Schedulers such as the current-thread scheduler do not rely on stack semantics. The yield-iterator pattern ensures stack depth does not grow per iteration.

**When to ignore this guideline:** none known.

### §6.16. Argument validation should occur outside Observable.Create

§6.5 requires that `Observable.Create`'s subscribe lambda not throw. Therefore **argument validation that may throw belongs outside the `Observable.Create<T>(...)` call**:

```csharp
public static IObservable<TResult> Select<TSource, TResult>(
    this IObservable<TSource> source, Func<TSource, TResult> selector)
{
    if (source == null) throw new ArgumentNullException(nameof(source));
    if (selector == null) throw new ArgumentNullException(nameof(selector));

    return Observable.Create<TResult>(observer => source.Subscribe(/* ... */));
}
```

**When to ignore this guideline:** when some aspect of the argument cannot be checked until the subscription is active (rare).

### §6.17. Unsubscription should be idempotent

The `IDisposable` returned from `Subscribe` doesn't expose subscription state. Consumers don't know whether they've already disposed, and may dispose defensively. Multiple `Dispose` calls **must be allowed**: the first runs the cleanup, subsequent calls are no-ops.

```csharp
var subscription = xs.Subscribe(Console.WriteLine);
subscription.Dispose();
subscription.Dispose();   // no-op, must not throw
```
Use a `_disposedValue` flag or equivalent guard.

**When to ignore this guideline:** none known.

### §6.18. Unsubscription should not throw

Rx composition chains subscriptions, which means it also chains unsubscriptions. Any operator can trigger an unsubscription at any time. A throw will crash the application, and because the observer is already unsubscribed, `OnError` cannot route the exception.

- If cleanup can fail, swallow + log; never propagate.
- When disposing multiple children, wrap each in try/catch so one failure doesn't skip the rest.

**When to ignore this guideline:** none known.

### §6.19. Custom IObservable implementations should follow the Rx contract

When you cannot follow §6.2 (use `Observable.Create`), the custom `IObservable` must still follow the Rx contract (§4) to behave correctly with Rx operators.

**When to ignore this guideline:** only when writing observable sequences that need to break the contract on purpose (e.g. for testing how code behaves when the contract is broken).

### §6.20. Operator implementations should follow guidelines for Rx usage

Operators internally compose other operators (per §6.1). The §5 ("Using Rx") guidelines apply **recursively to operator internals**.

**When to ignore this guideline:** as described in §2, only follow a guideline when it makes sense in that specific situation.

---

## Common violation patterns (quick scan during code review)

| Violation shape | Rule | Detection cue |
|---|---|---|
| Buffering operator flushes buffer on OnError | §6.6 | `OnError` handler calls OnNext before propagating |
| Multi-source operator forgets to serialize OnCompleted (only serializes OnNext) | §6.7 | `Synchronize` applied to OnNext but raw OnCompleted/OnError |
| Single-source operator wraps OnNext in unnecessary `lock(gate)` | §6.8 | `lock` inside a Transform/Filter/Select implementation |
| `null` check inside `Observable.Create<T>(obs => { if (x == null) throw ... })` | §6.16 | Validation inside the subscribe lambda |
| Subscribe lambda throws on error condition rather than routing to OnError | §6.5 | `throw` inside `Observable.Create<T>(observer => { ... throw ... })` |
| Dispose method throws (or contains code that can throw without catch) | §6.18 | `IDisposable.Dispose` without try/finally; multi-child dispose without per-child try/catch |
| Re-entrant Dispose runs cleanup twice | §6.17 | Missing `_disposedValue` flag or equivalent idempotency guard |
| Scheduler-queued work fires OnNext after Dispose | §4.4 | `IScheduler.Schedule(() => observer.OnNext(x))` without checking subscription state |
| User callback (selector, comparer, predicate, action) not protected by try/catch | §6.4 | Direct invocation of user delegate inside operator without wrapping |
| Recursive operator without `Schedule(self)` or `Concat`+yield | §6.15 | Direct self-recursion in OnNext handler |
| Hidden disposable not exposed to caller | §6.13 | `_ = scheduler.Schedule(...)` discarding the return value |
| Sum/Aggregate returns `T` instead of `IObservable<T>` | §6.14 | Blocking return type on what should be an operator |
| Argument validation missing entirely on a public extension method | §6.16 | No null check on extension method parameters |

---

## How to use this reference

### When writing or modifying an operator
1. After implementing the operator, walk every rule in §4 and §6.
2. For each rule, confirm the operator does not violate it.
3. Document the audit in the PR description (e.g., "Audited against §4.1-§4.4, §6.1, §6.4-§6.8, §6.13, §6.16-§6.18; §6.7 N/A (single-source operator)").

### When reviewing a PR
1. Use the "Common violation patterns" table as a scanning checklist.
2. Cite rule IDs in review comments (e.g., "§6.6: buffer should be discarded on error, not flushed").
3. Block merge on any unaddressed rule violation.

### When debugging an Rx-related bug
1. Identify the rule(s) most likely to explain the symptom.
2. Add tests that exercise the contract boundary (use `Tests/Utilities/ValidateSynchronization` for §4.2, `TestSourceCache.SetError` for §4.4 / §6.6, etc.).
3. Reference the rule ID in the fix commit message.

### Citing rules

Always use the literal `§` character when citing rule IDs (in commit messages, PR descriptions, code review comments, and code comments). Do NOT use ASCII substitutes (`S`, `SS`, `sec`, `§§`, etc.). Example commit subject:

```
Fix §6.6 violation in BatchIf

BatchIf was flushing its buffered changeset on source OnError, which
violates §6.6's abort semantic. Discard the buffer instead so error
deliveries are not preceded by stale OnNext.
```

---

## Maintaining this document

This file is a derivative of the [Microsoft Rx Design Guidelines v1.0 (October 2010)](https://go.microsoft.com/fwlink/?LinkID=205219). The Microsoft document has not been republished since v1.0 and the underlying Rx contract is stable. **Consult the PDF directly when revising rule text, adding new rules, or resolving disputes about intent.**

**Do NOT modify rule IDs.** External code reviews, PR descriptions, and commit messages cite the `§X.Y` IDs; renumbering breaks those references. The IDs come from the original PDF and changing them severs traceability.

**DO add new rules** in a new section (e.g., a DynamicData-specific section that goes beyond standard Rx) if the codebase discovers patterns the Microsoft document doesn't cover. Use a non-numeric prefix (e.g., `DD-1`, `DD-2`) to make clear they are not from the original spec.

**Keep the Modernization notes table current.** When a 2010 API name appears in the PDF that has since been renamed, removed, or supplemented in Rx.NET, add a row.

**Cross-references:** see `rx.instructions.md` for DynamicData-specific patterns and practical Rx.NET reference material that complements this design guide (Defer pattern for per-subscription state, disposable family practical examples, the modern Rx.NET operator catalog, common pitfalls). That file links back here for any rule it touches on.
