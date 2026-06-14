---
applyTo: "**/*.cs"
---
# Rx Contract Canonical Rules

Distilled from **[Microsoft Rx Design Guidelines v1.0 (October 2010)](https://go.microsoft.com/fwlink/?LinkID=205219)** and supplemented by the codebase-specific guidance in `rx.instructions.md`. This is the authoritative reference for what is and is not an Rx contract violation in DynamicData operators.

**Every operator added or modified must self-audit against these rules. Every bug fix must explicitly state which rules were verified.**

Rules use the **original Microsoft document's `§X.Y` numbering** throughout so citations are unambiguous and traceable to the source spec. Cite rule IDs in code review comments, PR descriptions, and commit messages (e.g., "Fixes §6.6 violation in `BatchIf`").

Sections preserve the original A/B/C ordering of this document (consumer contract first, then operator authors, then consumer-side usage), which is the order most useful for a contributor working in this repo:
- **§4** is the Observable Contract from the consumer's perspective (what callers can rely on)
- **§6** is rules for operator authors (what your operator must guarantee)
- **§5** is rules for code that uses Rx (consumer-side correctness)

---

## Quick reference by consumer type

**If you're a downstream subscriber relying on Rx outputs:** read §4. These are the guarantees you can count on from any well-behaved `IObservable`.

**If you're writing code that uses Rx operators:** read §5. These are the rules about correct use of Rx in calling code (`ObserveOn` placement, when `Synchronize` is appropriate, subscribing with all three handlers, etc.).

**If you're writing or modifying an operator:** read §6. Also apply §5 recursively inside the operator (per §6.20) because operator internals are themselves Rx-consuming code.

**Reviewing a PR:** start with the "Common violation patterns" table at the bottom; it's organized by symptom.

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

**If you find an obsolete API name used anywhere in this codebase, that is itself a finding to surface.**

---

## §4: The Observable Contract (consumer-facing)

### §4.1. The Rx Grammar
```
OnNext* (OnCompleted | OnError)?
```
- Zero or more `OnNext`, optionally followed by exactly ONE terminal notification.
- `OnError` and `OnCompleted` are **mutually exclusive**: emit one or the other, never both.
- After a terminal notification, **no further notifications of any kind**: not even another OnError or OnCompleted.

### §4.2. Serialized Notifications
- `OnNext`, `OnError`, `OnCompleted` calls to a single observer instance **MUST never execute concurrently**.
- This is the most-violated rule in practice; downstream operators and consumer code assume serialization.
- Violation produces silent state corruption, not exceptions.

### §4.3. Resource Cleanup on Terminal
- After `OnError` or `OnCompleted`, the operator MUST immediately release its resources.
- Side effects bound to subscription lifetime (`Observable.Using`, `Finally`) MUST fire deterministically.

### §4.4. Unsubscribe Semantics
- Calling `Dispose`: queued-but-not-yet-started work is cancelled.
- Work already in progress MAY complete, BUT **its results MUST NOT be signaled** to the unsubscribed observer.
- Messages MAY arrive during the `Dispose` call itself (Dispose can race with OnNext).
- After `Dispose` returns control to the caller: **no more messages arrive**.
- The unsubscription process MAY continue asynchronously on a different context after `Dispose` returns.

---

## §6: Operator Author Rules

### §6.1. Prefer Composition Over Observable.Create
- The Rx contracts are axioms, not guidelines. Trust them completely.
- Before reaching for `Observable.Create`, ask: can this be expressed via existing operators?
- Manual observer forwarding inside `Observable.Create` reimplements what `Subscribe` already guarantees, and tends to introduce bugs the existing operators would have prevented.
- See `rx.instructions.md` "Composition First" section for the rationale and the Defer pattern as an alternative for per-subscription state.

### §6.2. Use Observable.Create (not raw IObservable) when you must
- `Observable.Create` ensures grammar compliance (auto-unsubscribe on terminal, single-terminal enforcement).
- Custom `IObservable` implementations are higher-risk and only justified for non-standard contracts (e.g., when the return type must implement `ISubject` or a richer interface).

### §6.4. Protect Calls to User Code
**Wrap every user-provided delegate in try/catch and route exceptions to `observer.OnError`:**
- Selector functions (Transform, Select, etc.)
- Predicates (Filter, Where, etc.)
- Comparers (Sort, GroupOn, etc.)
- Key selectors (GroupOn, ToObservableChangeSet, etc.)
- Action callbacks (Do, OnItemRemoved, SubscribeMany, etc.)
- Calls to dictionaries/lists/hashsets that use a user-provided comparer

**DO NOT wrap these (they are "edge of the monad"):** Subscribe, Dispose, OnNext, OnError, OnCompleted. Calling OnError from these places leads to undefined behavior.

**Exception:** `IScheduler` implementations protect inside the scheduler itself, not at every call site.

### §6.5. Subscribe Implementations Must Not Throw
- Subscribe may be called asynchronously (e.g., second source in `Concat` is subscribed after first completes).
- A throw crashes the application: no observer in scope to route to.
- Error conditions detected in Subscribe MUST be routed via `observer.OnError(...)` followed by `return Disposable.Empty;`.

### §6.6. OnError Has Abort Semantics
- Once a source emits OnError, the operator MUST emit no further messages, not even buffered or aggregated state.
- **Example: a buffering operator must DISCARD its buffer on source error, not flush it.** The semantic is "abort", not "graceful degradation".
- Operators that aggregate state across multiple OnNext (Sort, Group, Page, etc.) must not "salvage" that state into a final OnNext when OnError arrives.

### §6.7. Multi-Source Operators MUST Serialize
When combining multiple sources into one output observer:
```csharp
var gate = new object();
source1.Synchronize(gate).Subscribe(observer);
source2.Synchronize(gate).Subscribe(observer);
```
OR equivalent gate-based pattern (DynamicData's `CacheParentSubscription._synchronize` is the canonical example for cache operators).

**All three notification types (OnNext / OnError / OnCompleted) must be serialized through the same gate**, not just OnNext.

### §6.8. Single-Source Operators MUST NOT Redundantly Serialize
- Per §6.7 every operator already serializes; downstream operators can ASSUME serialized input.
- Adding `Synchronize` "just in case" is an anti-pattern: it clutters, harms performance, and signals misunderstanding of the contract.
- `Synchronize` at the consumer-facing boundary (per §5.8) is reserved for fixing genuinely non-conforming external sources.

### §6.9 / §6.10 / §6.11. Parameterize Concurrency via Scheduler
- Concurrency-introducing operators take an `IScheduler` parameter (§6.9).
- Provide an overload with a sensible default scheduler (§6.10; prefer Immediate where possible per §6.12).
- The `IScheduler` parameter is the **LAST** argument for fluent IntelliSense (§6.11).
- Exception: `params T[]` operators take the scheduler as the second-to-last argument.

### §6.12. Avoid Introducing Concurrency Where Possible
- Adding concurrency skews timing data: delivery time is itself observable data.
- Default schedulers should be the minimal-concurrency option (often `Scheduler.Immediate`).
- Only introduce concurrency when it is essential to the operator's semantics.

### §6.13. Hand Out All Disposables
- Every `IDisposable` created inside an operator (subscription disposables, scheduled-action disposables, resource handles) MUST be reachable through the disposable returned to the subscriber.
- Use `CompositeDisposable`, `SerialDisposable`, `SingleAssignmentDisposable`, `RefCountDisposable` to compose.
- Hidden disposables = leaks at unsubscribe time and broken backpressure-style cleanup.

### §6.14. Operators Must Not Block
- Return `IObservable<T>`, never `T`.
- Even aggregation operators (Sum, Count, etc.) return `IObservable<T>`: the caller can use `First*`/`Last*`/`Single*` to escape the observable world if blocking is genuinely needed at the call site.

### §6.15. Avoid Deep Recursion in Operators
- Stack depth at operator invocation is unknown; recursive operators can blow the stack faster than expected.
- Use `IScheduler.Schedule(self => ...)` recursive overload OR `IEnumerable<IObservable<T>>` + `Concat` with a `yield` iterator.

### §6.16. Argument Validation Outside Observable.Create
- Per §6.5, `Observable.Create`'s subscribe lambda must not throw.
- Therefore: `ArgumentNullException` and other validation checks belong BEFORE the `Observable.Create<T>(...)` call.
- Exception: validation that genuinely requires the subscription to be live (rare).

### §6.17. Idempotent Dispose
- Multiple `Dispose()` calls on the same disposable: first runs cleanup, subsequent are no-ops.
- Required because consumers don't know subscription state and may dispose defensively.
- Use a `_disposedValue` flag or equivalent guard.

### §6.18. Dispose Must Not Throw
- Dispose chains cascade through compositions.
- A throw crashes the app, and the observer is already unsubscribed, so OnError cannot route the exception.
- If cleanup can fail, swallow + log; never propagate.
- When disposing multiple children, wrap each in try/catch so one failure doesn't skip the rest.

### §6.19. Custom IObservable Implementations MUST Follow the Contract
- If you can't use `Observable.Create`, you take on the burden of contract compliance yourself.
- All of §4 applies to your implementation.

### §6.20. Operator Implementations MUST Follow Usage Guidelines
- Operators internally compose other operators (per §6.1).
- §5 ("Using Rx") rules apply recursively to operator internals.

---

## §5: Using Rx (consumer-side rules)

These rules apply recursively inside operator implementations too, per §6.20.

### §5.2. Provide All Three Subscribe Arguments
- Default `Subscribe(onNext)` overload rethrows OnError on the source thread, crashing the app.
- Always provide onError and onCompleted handlers unless:
  - The source is guaranteed not to complete
  - The source is guaranteed not to error
  - The default behavior is genuinely desired

### §5.4. Pass Specific Scheduler to Concurrency-Introducing Operators
- Prefer `source.Throttle(timeSpan, specificScheduler)` over `source.Throttle(timeSpan).ObserveOn(specificScheduler)`.
- Creates the work on the right scheduler from the start; saves a scheduler hop.

### §5.5. ObserveOn Late, Few
- Each `ObserveOn` schedules per-message work and is expensive.
- Apply filters first, then `ObserveOn`, to avoid scheduling work for messages that get filtered out.

### §5.6. Limit Buffers
- `Replay`, `Buffer`, etc. without size/time limits cause unbounded memory growth.
- Always provide a limit: `Replay(10000, TimeSpan.FromHours(1))`.

### §5.7. Make Side Effects Explicit via Do
- Side effects (logging, mutation, etc.) buried in selector/predicate lambdas are unauditable.
- Hoist them into `Do(...)` so the side-effect is visible in the pipeline.
- Note: per Rx semantics, `Do` runs for each subscriber unless the pipeline is shared via `Publish`.

### §5.8. Use Synchronize Only for Non-Conforming Sources
- Operators created by Rx (and DynamicData) already follow the contract.
- `Synchronize()` (consumer-facing, no gate) is for external non-conforming `IObservable` implementations only.
- NOTE: the **gate-based** `Synchronize(gate)` inside multi-source operators (per §6.7) is a DIFFERENT pattern and IS valid.

### §5.10. Share Side Effects via Publish
- Cold observables re-run their side effects on every subscribe.
- If sharing is needed, use `Publish(shared => ...)` or `Publish().RefCount()`.

---

## Common violation patterns (quick scan during code review)

| Violation shape | Rule | Detection cue |
|---|---|---|
| Buffering operator flushes buffer on OnError | §6.6 | `OnError` handler calls OnNext before propagating |
| Multi-source operator forgets to serialize OnCompleted (only serializes OnNext) | §6.7 | `Synchronize` applied to OnNext but raw OnCompleted/OnError |
| Single-source operator wraps OnNext in unnecessary `lock(gate)` | §6.8 | `lock` inside a Transform/Filter/Select implementation |
| `null` check inside `Observable.Create<T>(obs => { if (x == null) throw ... })` | §6.16 | Validation inside the subscribe lambda |
| Subscribe lambda throws on error condition rather than routing to OnError | §6.5 | `throw` inside `Observable.Create<T>(observer => { ... throw ... })` |
| Dispose method throws (or contains code that can throw without catch) | §6.18 | IDisposable.Dispose without try/finally; multi-child dispose without per-child try/catch |
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

This file is a derivative of the [Microsoft Rx Design Guidelines v1.0 (October 2010)](https://go.microsoft.com/fwlink/?LinkID=205219) (canonical fwlink; resolves to `download.microsoft.com/.../Rx Design Guidelines.pdf`). The Microsoft document has not been republished since v1.0 and the underlying Rx contract is stable. **Consult the PDF directly when revising rule text, adding new rules, or resolving disputes about intent.**

**Do NOT modify rule IDs.** External code reviews, PR descriptions, and commit messages cite the `§X.Y` IDs; renumbering breaks those references. The IDs come from the original PDF and changing them severs traceability to the source spec.

**DO add new rules** in a new section (e.g., a DynamicData-specific section that goes beyond standard Rx) if the codebase discovers patterns the Microsoft document doesn't cover. Use a non-numeric prefix (e.g., `DD-1`, `DD-2`) to make clear they are not from the original spec.

**Cross-references:** see `rx.instructions.md` for prose-style teaching of the same concepts plus DynamicData-specific patterns (Defer for per-subscription state, Observable.Create when truly needed, disposable family reference, common pitfalls).
