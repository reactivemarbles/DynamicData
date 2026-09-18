---
applyTo: "**/*.cs"
---
# Rx Gate-Holding Operators

Several Rx combinators install a private `_gate` and hold it for the full duration of every downstream `OnNext`. This file records which ones matter to DynamicData, what we use instead, and the audit of every cache-side usage.

## The rule

**Never hold a lock while emitting downstream.**

Any operator that holds a lock while emitting adds an edge to the lock ordering. Two of them is all it takes for ABBA: A holds its gate and emits into B where it needs B's gate, while a second path has B holding its gate and emitting into A. Downstream of a DynamicData pipeline is consumer code whose locks we cannot audit, so the only reliable defence is not to hold one across the callout.

`SharedDeliveryQueue` is the mechanism: enqueue, release, drain outside the lock. Nothing of ours is held while a downstream observer runs.

## Helpers

Defined in `Internal/SynchronizeSafeExtensions.cs` and `Internal/DeliveryQueueMergeExtensions.cs`.

| Helper | Use when |
|---|---|
| `UnsynchronizedMerge<T>` | Drop-in for `Observable.Merge` where every input is already serialized. Preserves Merge's terminal semantics without installing a gate. |
| `UnsynchronizedCombineLatest<TFirst, TSecond, TResult>` | Two-input drop-in for `Observable.CombineLatest`, same precondition. |
| `DeliveryQueueMerge<T>` | Same-type merge that owns its own `DeliveryQueue<T>`, so the call site never mentions queue plumbing. |

**Precondition for the unsynchronized variants:** every input must already be serialized, which in this library means routing each one through the same `SharedDeliveryQueue` via `SynchronizeSafe(queue)` before the merge.

The precondition is a property of how a pipeline is wired, not of the operator, and it breaks silently. `TransformAsync`'s `forced` path applied `SynchronizeSafe(queue)` and then ran an async `Select(...).Concat()` after it, so results came back off-gate; stock `Observable.Merge` serialized them anyway and hid the defect. Route every input through the queue before merging, and put the `SynchronizeSafe` call last in the chain so nothing escapes it.

## Audit

Every cache-side usage of an Rx combinator that holds a gate during downstream delivery.

| Rx operator | DynamicData cache usage | Verdict |
|---|---|---|
| `Merge` | `AutoRefresh`, `Page`, `Virtualise`, `Sort`, `SortAndPage`, `SortAndVirtualize`, `GroupOnImmutable`, `QueryWhenChanged`, `TransformWithForcedTransform`, `GroupOn`, `GroupOnDynamic`, `TransformAsync`, `TransformMany`, `Switch` | Replaced with `UnsynchronizedMerge` / `DeliveryQueueMerge` |
| `Merge` | `FullJoin` / `InnerJoin` / `LeftJoin` / `RightJoin` | Left alone: inputs come from independently materialized caches that share no queue, so Merge's gate IS the serializer |
| `Merge` | `AsyncDisposeMany` disposal fan-in, `ToObservableOptional` initial-value branch, `TransformAsync.Merge(maxConcurrency)` | Left alone: not in queue-drain context |
| `Merge(int)` | `ObservablePropertyFactory`, property-chain plumbing | Left alone: not in queue-drain context |
| `CombineLatest` | `TreeBuilder` | Replaced with `UnsynchronizedCombineLatest` |
| `CombineLatest` | `TrueFor`, `Binding/NotifyPropertyChangedEx` | Left alone: not in queue-drain context |
| `Switch` | `Cache/Internal/Switch.cs` and the `IObservableCache` overload that delegates to it | Refactored to an inline `SerialDisposable` |
| `Switch` | `ObservableCache` subscription plumbing, `AggregationEx` | Left alone: one-shot or aggregation, not queue-drain |
| `Synchronize` | Previous `Synchronize(lock)` usages | Migrated to `SynchronizeSafe` |
| `Synchronize` | `EditDiffChangeSetOptional` defensive serialize | Removed: `§6.8` / `§5.8` anti-pattern, the source already guarantees serialization by `§4.2` |
| `Buffer` (time-based) | `AutoRefresh` change buffering, consumer-facing `Buffer` overloads | Left alone: single input, the gate protects internal buffer state |
| `Throttle` | `WhenChanged` / `AutoRefresh` throttle, `GroupOnProperty` regrouper throttle | Left alone: single input, the gate protects internal throttle state |
| `Zip`, `WithLatestFrom`, `Sample`, `Window`, `Join`, `GroupJoin`, observable-`SelectMany` | Not used in cache pipelines | n/a. All `Zip` and `SelectMany` matches are LINQ-over-`IEnumerable`, not the Rx operators |

## Maintenance

When an operator starts or stops using one of these combinators, update the table. When a new gate-holding Rx combinator comes into use, add a row for it and state the verdict.
