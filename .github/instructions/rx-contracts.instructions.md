---
applyTo: "**/*.cs"
---
# Rx Contract Rules

Reference: [ReactiveX Contract](http://reactivex.io/documentation/contract.html)

## The Observable Contract

### 1. Serialized Notifications (CRITICAL)

`OnNext`, `OnError`, and `OnCompleted` calls MUST be serialized — they must not be called concurrently from different threads. This is the most commonly violated rule and the hardest to debug.

```csharp
// WRONG: concurrent OnNext from two threads
source1.Subscribe(item => observer.OnNext(Transform(item)));  // thread A
source2.Subscribe(item => observer.OnNext(Transform(item)));  // thread B — RACE!

// RIGHT: serialize through a shared queue
var queue = new SharedDeliveryQueue(locker);
source1.SynchronizeSafe(queue).Subscribe(observer);
source2.SynchronizeSafe(queue).Subscribe(observer);
```

### 2. Terminal Notifications

- `OnError(Exception)` and `OnCompleted()` are terminal — no further notifications after either
- An observable MUST call exactly one of: `OnError` OR `OnCompleted` (not both, not neither for finite sequences)
- After a terminal notification, all resources should be released

### 3. Notification Order

- `OnNext*` (`OnError` | `OnCompleted`)?
- Zero or more `OnNext`, followed by at most one terminal notification
- No `OnNext` after `OnError` or `OnCompleted`

### 4. Subscription Lifecycle

- `Subscribe` returns `IDisposable` — disposing unsubscribes
- After disposal, no further notifications should be delivered
- Disposal must be idempotent and thread-safe

### 5. Error Handling

- Exceptions thrown inside `OnNext` handlers propagate to the caller (the operator delivering)
- Operators should use `SubscribeSafe` (not `Subscribe`) to catch subscriber exceptions and route them to `OnError`
- Never swallow exceptions silently — always propagate or log

## DynamicData-Specific Rules

### 6. Lock Ordering

When operators use internal locks:
- **Never hold a lock during `observer.OnNext()`** — this is the #1 cause of cross-cache deadlocks
- Use the queue-drain pattern: enqueue under lock, deliver outside lock
- `SharedDeliveryQueue` and `DeliveryQueue<T>` implement this pattern

### 7. Changeset Immutability

- `IChangeSet<TObject, TKey>` instances emitted by `OnNext` must be effectively immutable after emission
- The receiver may hold a reference and iterate it later
- `ChangeAwareCache<T,K>.CaptureChanges()` returns a snapshot — safe to emit

### 8. Dispose Under Lock

When an operator's `Dispose` needs to synchronize with its delivery:
- Use `queue.AcquireReadLock()` to acquire the lock without triggering drain
- This ensures no delivery is in progress when cleanup runs

```csharp
return Disposable.Create(() =>
{
    subscription.Dispose();
    using var readLock = queue.AcquireReadLock();
    // Safe to clean up — no concurrent delivery
    cache.Clear();
});
```
