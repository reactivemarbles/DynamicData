---
applyTo: "**/*.cs"
---
# Rx Design Guide

Complete distillation of the **[Microsoft Rx Design Guidelines v1.0 (October 2010)](https://go.microsoft.com/fwlink/?LinkID=205219)**. This is the authoritative reference for the Rx contract and the rules for using Rx and implementing Rx operators in DynamicData.

**Code samples and API names have been updated to current Rx.NET (modernized from the 2010 spec).** Treat the conventions here as current.

**Every operator added or modified must self-audit against these rules. Every bug fix must explicitly state which rules were verified.**

Rules use the original document's `§X.Y` numbering. Cite rule IDs in code reviews, PR descriptions, and commit messages (e.g., "Fixes §6.6 violation in `BatchIf`"). Always use the literal `§` character — never ASCII substitutes (`S`, `SS`, `sec`, etc.).

---

## §4: The Rx contract

`IObservable<T>` and `IObserver<T>` only specify their methods' arguments and return types. The Rx library makes additional assumptions about these interfaces that are not expressible in the .NET type system. These assumptions form a contract that **all producers and consumers** of Rx types must follow.

### §4.1. Assume the Rx Grammar

Messages follow `OnNext* (OnCompleted | OnError)?`:
- Zero or more `OnNext`, optionally followed by exactly one terminal notification.
- `OnError` and `OnCompleted` are **mutually exclusive**.
- After a terminal, **no further notifications of any kind**, not even another terminal.

The single terminal lets consumers deterministically clean up; the single-failure rule supports abort semantics in multi-source operators (§6.6).

**When to ignore:** only for non-conforming `IObservable` sources. Restore conformance with `Synchronize()` (§5.8).

### §4.2. Assume observer instances are called in a serialized fashion

`OnNext`, `OnError`, and `OnCompleted` calls to a single observer **MUST never execute concurrently.** Only the operators that produce multi-source sequences are required to serialize (§6.7); consumers can safely assume serialization.

This is the most-violated rule in practice. Violations produce silent state corruption, not exceptions.

**When to ignore:** for a custom `IObservable` that doesn't serialize, wrap with `Synchronize()` to restore the guarantee.

### §4.3. Assume resources are cleaned up after an OnError or OnCompleted message

After a terminal, the operator MUST immediately release its resources. `Observable.Using` / `Finally` fire deterministically.

### §4.4. Assume a best effort to stop all outstanding work on Unsubscribe

When `Dispose` is called:
- Queued work that has not started is cancelled.
- Work already in progress may complete, but its results **MUST NOT** be signaled to the unsubscribed observer.
- Messages may arrive **during** the `Dispose` call itself (Dispose races with `OnNext`).
- After `Dispose` returns: no more messages arrive.
- The unsubscription process may continue asynchronously on a different context after `Dispose` returns.

---

## §5: Using Rx

Rules for code that consumes Rx. Apply recursively inside operator implementations (§6.20).

### §5.1. Consider drawing a Marble-diagram

Sketching the inputs and outputs over time often makes the operator choice obvious (delay-then-call → `Throttle`; new sequence per input → `SelectMany`).

**When to ignore:** when you're comfortable enough without one.

### §5.2. Consider passing multiple arguments to Subscribe

`Subscribe(onNext)` **rethrows OnError on the source thread, crashing the app.** The default `OnCompleted` is a no-op. Provide all three handlers unless:
- The sequence is guaranteed not to complete (e.g. a UI event)
- The sequence is guaranteed not to error
- The default behavior is genuinely desired

### §5.3. Consider using LINQ query expression syntax

Rx implements the query-expression pattern; `SelectMany`-based pipelines often read better as LINQ. Skip when many of your operators have no query-syntax equivalent.

### §5.4. Consider passing a specific scheduler to concurrency-introducing operators

Better to introduce concurrency on the right scheduler from the start than to fix it up with `ObserveOn`:
```csharp
keyUp.Throttle(TimeSpan.FromSeconds(1), DispatcherScheduler.Current);
```
Without the scheduler, the default `Throttle` overload would deliver on the ThreadPool.

**When to ignore:** when combining many sources from different contexts, use §5.5 (one `ObserveOn` at the end).

### §5.5. Call the ObserveOn operator as late and in as few places as possible

`ObserveOn` schedules per-message work. Placing it after filters avoids scheduling work for messages that get filtered out. Skip entirely when no specific context is required.

### §5.6. Consider limiting buffers

`Replay`, `Buffer`, etc. without size/time limits cause unbounded memory growth: `Replay(10000, TimeSpan.FromHours(1))`.

### §5.7. Make side-effects explicit using the Do operator

Side effects buried in selector/predicate lambdas are unauditable, and they run **per subscription** (unless shared via §5.10). Hoist them into `Do(...)`:
```csharp
xs.Where(x => x.Failed).Do(x => Log(x)).Subscribe(...);
```

**When to ignore:** when the side effect needs data unavailable to `Do`.

### §5.8. Use the Synchronize operator only to "fix" custom IObservable implementations

Rx and DynamicData operators already satisfy §4.1 / §4.2. Calling `Synchronize()` on one of them is redundant and counterproductive. Only use it on external sources that don't follow the contract.

> NOTE: this refers to the **single-argument `Synchronize()`** for non-conforming sources. The **gate-based `Synchronize(gate)`** in multi-source operators (§6.7) is a different pattern and is valid.

### §5.9. Assume messages can come through until unsubscribe has completed

Messages can be in flight while `Dispose` is being called and may still arrive during the `Dispose` call. After `Dispose` returns control, no more messages arrive. Unsubscription itself may still be running on another context.

### §5.10. Use the Publish operator to share side-effects

Most observables are cold: each subscription replays side effects. When side effects must happen only once, share via `Publish(shared => ...)` or `Publish().RefCount()`.

**When to ignore:** when subscriptions have no side effects, or when repeating them is harmless. The extra machinery is unnecessary.

---

## §6: Operator implementations

### §6.1. Implement new operators by composing existing operators

Composition reuses the corner-case handling the Rx team built into base operators:
```csharp
public static IObservable<TResult> SelectMany<TSource, TResult>(
    this IObservable<TSource> source,
    Func<TSource, IObservable<TResult>> selector)
    => source.Select(selector).Merge();
```
`Select` already protects against selector exceptions (§6.4); `Merge` already serializes (§6.7).

**When to ignore:** no appropriate base operators exist, OR profiling proves the composed form is too slow.

### §6.2. Implement custom operators using Observable.Create

When composition isn't enough, use `Observable.Create`. It enforces grammar compliance: auto-unsubscribe on terminal, single-terminal enforcement.

**When to ignore:** the operator must return a non-conforming sequence (rare; usually testing), or the return type must implement more than `IObservable` (e.g. `ISubject`).

### §6.3. Implement operators for existing observable sequences as generic extension methods

Extension methods → IntelliSense on every sequence. Generics → applicable to any element type.

**When to ignore:** the operator doesn't work on a source sequence, or genuinely cannot be generic.

### §6.4. Protect calls to user code from within an operator

Wrap every user-provided delegate in try/catch and route exceptions to `observer.OnError`:
- Selectors, predicates, comparers, key selectors
- Action callbacks (`Do`, `OnItemRemoved`, `SubscribeMany`)
- Calls to dictionaries / lists / hashsets that use a user-provided comparer

```csharp
source.Subscribe(
    x =>
    {
        TResult result;
        try { result = selector(x); }
        catch (Exception ex) { observer.OnError(ex); return; }
        observer.OnNext(result);
    },
    observer.OnError,
    observer.OnCompleted);
```

**Edge of the monad** (do NOT wrap): `Subscribe`, `Dispose`, `OnNext`, `OnError`, `OnCompleted`. Calling `OnError` from these places is undefined behavior.

**Exception:** for `IScheduler` calls, protect inside the scheduler implementation, not at every call site.

**When to ignore:** for calls to user code made **before** creating the observable (outside `Observable.Create`). Those run on the current execution context and follow normal control flow.

### §6.5. Subscribe implementations should not throw

`Subscribe` may be called asynchronously (e.g. the second source of `Concat` after the first completes). A throw crashes the app because no observer is in scope. Route errors via `observer.OnError(...)` then `return Disposable.Empty;`.

**When to ignore:** when the error is catastrophic and should bring the program down anyway.

### §6.6. OnError messages should have abort semantics

Once `OnError` arrives, the operator MUST emit no further messages — **not even buffered or aggregated state.** The canonical violation: a buffering operator that "salvages" its buffer into a final `OnNext` on error. The buffer must be **discarded**. Aggregating operators (Sort, Group, Page, etc.) must not salvage state on error.

### §6.7. Serialize calls to IObserver methods within observable sequence implementations

When combining multiple sources into one output, serialize **all three notification types** (OnNext, OnError, OnCompleted) through a shared gate:
```csharp
var gate = new object();
source1.Synchronize(gate).Subscribe(observer);
source2.Synchronize(gate).Subscribe(observer);
```

**When to ignore:**
- Single-source operator (§6.8 applies)
- No concurrency introduced
- Other constraints guarantee no concurrency

> NOTE: if a source breaks the contract, the consumer can fix it with `Synchronize()` (§5.8) before passing it in.

### §6.8. Avoid serializing operators

Per §6.7 every operator already serializes; downstream operators can **assume** serialized input. Adding `Synchronize` "just in case" clutters code, harms performance, and signals misunderstanding of the contract. Fix non-conforming sources at the consumer boundary (§5.8), not inside operators.

### §6.9. Parameterize concurrency by providing a scheduler argument

Concurrency-introducing operators take an `IScheduler`:
```csharp
public static IObservable<TValue> Return<TValue>(TValue value, IScheduler scheduler)
    => Observable.Create<TValue>(observer =>
        scheduler.Schedule(() => { observer.OnNext(value); observer.OnCompleted(); }));
```

**When to ignore:** the operator doesn't control concurrency creation (e.g. event-to-observable wrappers), or must use a specific scheduler internally.

### §6.10. Provide a default scheduler

In most cases there is a good default. Provide it as an overload so callers can stay succinct. Per §6.12, prefer `Scheduler.Immediate`.

**When to ignore:** when no good default exists.

### §6.11. The scheduler should be the last argument to the operator

Makes the operator fluent in IntelliSense; combined with §6.10's overload, callers can add or omit the scheduler without changing argument order.

**When to ignore:** `params T[]` operators require `params` last. Make the scheduler the **second-to-last** argument instead.

### §6.12. Avoid introducing concurrency

Adding concurrency changes timeliness, and **delivery time is itself observable data** — concurrency skews that data. Defaults should be `Scheduler.Immediate` where possible; only introduce concurrency when essential to the operator's semantics.

> NOTE: with `Scheduler.Immediate`, `Subscribe` becomes blocking. Expensive computation in that situation is a candidate for introducing concurrency.

### §6.13. Hand out all disposable instances created inside the operator to consumers

Every `IDisposable` created inside an operator (subscriptions, scheduled actions, resources) MUST be reachable through the disposable returned to the subscriber. Compose via the `System.Reactive.Disposables` family:

| Type | Purpose |
|---|---|
| `CompositeDisposable` | Groups multiple disposables; dispose together |
| `SerialDisposable` | Replaceable holder; assignment disposes the previous |
| `SingleAssignmentDisposable` | Assignable once; throws on second assignment |
| `RefCountDisposable` | Underlying disposed only when all dependents released |
| `BooleanDisposable` | Exposes `IsDisposed` state |
| `CancellationDisposable` | Bridges `IDisposable` and `CancellationToken` |
| `ContextDisposable` | Disposes on a specified `SynchronizationContext` |
| `ScheduledDisposable` | Disposes via a scheduler |

Hidden disposables leak at unsubscribe time and break cleanup.

### §6.14. Operators should not block

Return `IObservable<T>`, never `T`. Aggregation operators like `Sum` return `IObservable<int>`; callers escape via `First*`/`Last*`/`Single*` when they need a value.

### §6.15. Avoid deep stacks caused by recursion in operators

Stack depth at operator invocation is unknown; recursive operators blow the stack faster than expected. Two solutions:
- The recursive `IScheduler.Schedule(self => ...)` overload
- `yield`-based `IEnumerable<IObservable<T>>` + `Concat`

### §6.16. Argument validation should occur outside Observable.Create

Per §6.5 the subscribe lambda must not throw. Therefore null checks and other validation belong **before** the `Observable.Create<T>(...)` call:
```csharp
if (source == null) throw new ArgumentNullException(nameof(source));
if (selector == null) throw new ArgumentNullException(nameof(selector));
return Observable.Create<TResult>(observer => /* ... */);
```

**When to ignore:** when validation genuinely requires the subscription to be live (rare).

### §6.17. Unsubscription should be idempotent

The `IDisposable` returned from `Subscribe` doesn't expose state. Consumers may dispose defensively. First `Dispose` runs cleanup; subsequent calls are no-ops. Use a `_disposedValue` flag or equivalent guard.

### §6.18. Unsubscription should not throw

Disposal cascades through compositions. A throw crashes the app, and the observer is already unsubscribed so `OnError` can't route the exception.
- If cleanup can fail: swallow + log; never propagate.
- When disposing multiple children: wrap each in try/catch so one failure doesn't skip the rest.

### §6.19. Custom IObservable implementations should follow the Rx contract

When you can't use `Observable.Create` (§6.2), your custom `IObservable` must still satisfy all of §4. You take on the burden the library would otherwise carry.

**When to ignore:** only for sequences that intentionally break the contract (e.g. testing how downstream code behaves under broken contracts).

### §6.20. Operator implementations should follow guidelines for Rx usage

Operators internally compose other operators (§6.1). The §5 rules apply **recursively** inside operator implementations.

---

## Maintaining this document

Derivative of the [Microsoft Rx Design Guidelines v1.0 (October 2010)](https://go.microsoft.com/fwlink/?LinkID=205219). The Microsoft document has not been republished since v1.0 and the underlying Rx contract is stable. **Consult the PDF when revising rule text or resolving questions about original intent.**

**Do NOT modify rule IDs.** External code reviews, PRs, and commit messages cite `§X.Y` IDs; renumbering breaks references.

**DO add new rules** in a new section if the codebase discovers patterns the Microsoft document doesn't cover. Use a non-numeric prefix (e.g., `DD-1`) to make clear they are not from the original spec.

**See also:** `rx.instructions.md` for DynamicData-specific patterns and practical Rx.NET reference material (Defer pattern, disposable code samples, modern operator catalog, common pitfalls).
