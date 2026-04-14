# Add Documentation Skill

## Purpose

Codifies the process for writing publication-quality XML documentation for DynamicData operators. Use this when adding a new operator, updating an existing operator's docs, or batch-updating docs across a file.

## When to Use

- A new operator has been added and needs XML documentation
- An existing operator's behavior has changed and its docs need updating
- A batch documentation pass is needed across multiple operators
- A PR reviewer requests improved documentation

## Process Overview

1. **Analyze the implementation** (read the code, don't guess)
2. **Write the XML comments** following the template below
3. **Verify accuracy** by cross-checking claims against the implementation
4. **Build and test** to confirm XML is well-formed

---

## Step 1: Analyze the Implementation

Read the operator's internal implementation file (in `Cache/Internal/` or `List/Internal/`). For EACH of the following, trace the actual code path:

### For Cache Operators (`IChangeSet<TObject, TKey>`)
- **Add**: What happens when `ChangeReason.Add` arrives?
- **Update**: What happens when `ChangeReason.Update` arrives? Is `Previous` preserved?
- **Remove**: What happens when `ChangeReason.Remove` arrives? Any cleanup?
- **Refresh**: What happens when `ChangeReason.Refresh` arrives? Re-evaluate? Forward? Drop? Convert to Add/Remove?
- **OnError**: Forwarded? Swallowed? Caught and routed to callback?
- **OnCompleted**: Immediate? Waits for children? Conditional?

### For List Operators (`IChangeSet<T>`)
- **Add**: Single item added at index
- **AddRange**: Multiple items added at index
- **Replace**: Item at index replaced (cache equivalent of Update)
- **Remove**: Single item removed
- **RemoveRange**: Multiple items removed
- **Moved**: Item moved between indices
- **Refresh**: Signal to re-evaluate
- **Clear**: All items removed
- **OnError**: How errors propagate
- **OnCompleted**: How completion propagates

### Additional Analysis
- **Multi-source behavior**: For operators with multiple inputs (joins, set ops, MergeManyChangeSets), how are sources synchronized? What if one errors/completes independently?
- **Per-item observable behavior**: For *OnObservable operators, when is the subscription created/disposed? What if the observable never emits?
- **Gotchas**: Anything surprising, non-obvious, or easy to get wrong

---

## Step 2: Write the XML Comments

### Template for Primary Overloads (Cache)

```xml
/// <summary>
/// [1-3 sentences: what it does and why you'd use it. No behavioral details here.]
/// </summary>
/// <typeparam name="TObject">The type of items in the cache.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
/// <param name="exampleParam">An <see cref="IComparer{T}"/> that [what it controls].</param>
/// <returns>[What the return observable emits and what each emission represents.]</returns>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
/// <remarks>
/// <para>[Detailed explanation: when to use, how it works, what makes this overload different.
/// Multiple paragraphs are fine. This is where the real documentation lives.]</para>
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>[Specific behavior. Name output ChangeReasons in bold.]</description></item>
/// <item><term>Update</term><description>[Specific behavior. Mention Previous value handling.]</description></item>
/// <item><term>Remove</term><description>[Specific behavior. Mention cleanup if applicable.]</description></item>
/// <item><term>Refresh</term><description>[Specific behavior. This is often the surprising one.]</description></item>
/// <item><term>OnError</term><description>[Source errors? Child errors? Factory errors?]</description></item>
/// <item><term>OnCompleted</term><description>[Immediate? Waits for children? Conditional?]</description></item>
/// </list>
/// <para><b>Worth noting:</b> [Non-obvious behaviors, common mistakes, edge cases.]</para>
/// </remarks>
/// <seealso cref="[SafeVariant]"/>
/// <seealso cref="[AsyncVariant]"/>
/// <seealso cref="[SimilarOperator]"/>
/// <seealso cref="[ComplementaryOperator]"/>
```

### Template for Primary Overloads (List)

Same structure, but the event table uses `ListChangeReason` values:

```xml
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>[behavior]</description></item>
/// <item><term>AddRange</term><description>[behavior]</description></item>
/// <item><term>Replace</term><description>[behavior]</description></item>
/// <item><term>Remove</term><description>[behavior]</description></item>
/// <item><term>RemoveRange</term><description>[behavior]</description></item>
/// <item><term>Moved</term><description>[behavior]</description></item>
/// <item><term>Refresh</term><description>[behavior]</description></item>
/// <item><term>Clear</term><description>[behavior]</description></item>
/// <item><term>OnError</term><description>[behavior]</description></item>
/// <item><term>OnCompleted</term><description>[behavior]</description></item>
/// </list>
```

Rows with identical behavior can be combined (e.g., "Remove/RemoveRange/Clear").

### Template for Multi-Source Operators

Use separate labeled tables for each source:

```xml
/// <para><b>Source changeset handling (parent events):</b></para>
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>[what happens to subscriptions/state]</description></item>
/// ...
/// </list>
/// <para><b>Per-item observable handling:</b></para>
/// <list type="table">
/// <listheader><term>Emission</term><description>Behavior</description></listheader>
/// <item><term>First value</term><description>[what appears downstream]</description></item>
/// <item><term>Subsequent values</term><description>[updates? replacements?]</description></item>
/// <item><term>Error</term><description>[terminates stream? swallowed?]</description></item>
/// <item><term>Completed</term><description>[item freezes? removed?]</description></item>
/// </list>
```

### Template for Secondary (inheritdoc) Overloads

```xml
/// <inheritdoc cref="[PrimaryOverloadFullSignature]"/>
/// <param name="[differingParam]">A <see cref="[Type]"/> that [description specific to this overload].</param>
/// <remarks>This overload [omits the key / accepts a simpler factory / etc]. Delegates to <see cref="[Primary]"/>.</remarks>
```

### Template for Mutation Helpers (AddOrUpdate, Remove, Clear, Refresh)

The event table describes what changeset is PRODUCED, not consumed:

```xml
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>Produced when the key does not already exist in the cache.</description></item>
/// <item><term>Update</term><description>Produced when the key already exists.</description></item>
/// <item><term>Other</term><description>Not produced by this method.</description></item>
/// </list>
```

### Template for Non-Changeset Operators (MergeMany, Watch, Property Observers)

The event table describes how source changeset events affect subscriptions:

```xml
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>Subscribes to per-item observable.</description></item>
/// <item><term>Update</term><description>Disposes old subscription, subscribes to new.</description></item>
/// <item><term>Remove</term><description>Disposes subscription.</description></item>
/// <item><term>Refresh</term><description>No effect on subscriptions.</description></item>
/// <item><term>OnError</term><description>[child errors swallowed? forwarded?]</description></item>
/// <item><term>OnCompleted</term><description>[waits for children? immediate?]</description></item>
/// </list>
```

---

## Step 3: Quality Rules

### Param Tags
- Every `<param>` MUST mention and link its type via `<see cref="..."/>`
- Read the actual method signature to get the real type
- `Func<>` and `Action<>` link to `Func{T, TResult}` or `Action{T}`
- `IObservable<>` links to `IObservable{T}`
- `IComparer<>` and `IEqualityComparer<>` link appropriately
- `IScheduler` links directly
- `bool`, `int`, `string[]` params: no type link needed (self-evident)

### SeeAlso Tags
Every operator MUST cross-reference:
- **Other overloads in the same set** (bidirectional: primary links to all secondaries, each secondary links back to primary)
- **Safe variant** if one exists (Transform <-> TransformSafe)
- **Async variant** if one exists (Transform <-> TransformAsync)
- **Similar operators** (Filter <-> FilterImmutable <-> FilterOnObservable)
- **Complementary operators** often used together (Filter <-> AutoRefresh, Sort <-> Bind)
- **Commonly confused operators** (MergeMany vs MergeManyChangeSets vs MergeChangeSets)

### Type References
- Types and interfaces mentioned in comments MUST use `<see cref="..."/>`, not `<c>...</c>`
- Method names, event names, and property names use `<c>...</c>` (contextual, not discoverable types)
- Internal-only types MUST NOT appear in public documentation. Describe behavior, not implementation.

### Tone
- Summary: 1-3 sentences only. All behavioral detail in `<remarks>`.
- NEVER use em dashes (the long dash character). Use colons, commas, parentheses, or restructure.
- No emoji.
- No filler words: "comprehensive", "robust", "seamlessly", "leverage", "utilize", "facilitate".
- Be specific about outcomes: "an **Update** is emitted" not "the change is propagated".
- Use "Worth noting" (not "Gotchas") for non-obvious behavior sections.

### Accuracy
- Every behavioral claim MUST be traceable to the implementation code.
- If uncertain about a behavior, read the code. Do not guess from naming conventions.
- The Refresh row is the most commonly surprising one. Always verify it.

---

## Step 4: Build and Verify

```bash
dotnet build src/DynamicData/DynamicData.csproj --no-restore -c Release --framework net9.0
```

Check for:
- CS1570/CS1571/CS1572/CS1573 warnings (malformed XML)
- CS1574 warnings (unresolved cref attributes)
- 0 errors

---

## Batch Application

When documenting multiple operators at once:

1. **Dispatch analysis agents in parallel** (read-only explore agents) to trace implementations. Group related operators:
   - Transform family (Transform, TransformSafe, TransformAsync, TransformImmutable, etc.)
   - Filter family (Filter, FilterImmutable, FilterOnObservable)
   - Merge family (MergeMany, MergeChangeSets, MergeManyChangeSets)
   - Set operations (And, Or, Except, Xor)
   - Joins (InnerJoin, LeftJoin, RightJoin, FullJoin + Many variants)
   - Lifecycle (OnItemAdded, OnItemUpdated, OnItemRemoved, OnItemRefreshed, ForEachChange)

2. **Apply edits yourself or via a single agent** (never multiple agents editing the same file simultaneously: they will clobber each other's work).

3. **After editing, verify**:
   - Build passes
   - No em dashes: search for Unicode character 0x2014
   - No old-style "The source." params remaining
   - All event tables use `<listheader><term>Event</term>` header
   - Spot-check 5-10 operators against their implementation

4. **Diff against main** to confirm no important details were lost from the original comments. Preserve all existing useful information (additive only).

---

## Examples

### Good: Filter Refresh (shows re-evaluation with 4 outcomes)

```xml
/// <item><term>Refresh</term><description>Re-evaluated. Now passes but didn't before: <b>Add</b>.
/// Still passes: <b>Refresh</b> forwarded. No longer passes: <b>Remove</b>. Still fails: dropped.</description></item>
```

### Good: MergeChangeSets Remove (shows cross-source fallback)

```xml
/// <item><term>Remove</term><description>If the removed value was published downstream, all remaining
/// sources are scanned for the same key. If another source holds it, an <b>Update</b> is emitted with the
/// replacement value (selected by comparer if provided). If no source holds the key, a <b>Remove</b> is
/// emitted.</description></item>
```

### Good: OnItemRemoved (shows disposal behavior)

```xml
/// <item><term>OnCompleted</term><description>Forwarded. When invokeOnUnsubscribe is true, disposing the
/// subscription also invokes the callback for every item still in the cache.</description></item>
```

### Good: TransformOnObservable per-item table

```xml
/// <para><b>Per-item observable handling:</b></para>
/// <list type="table">
/// <listheader><term>Emission</term><description>Behavior</description></listheader>
/// <item><term>First value</term><description>The transformed item appears downstream as an <b>Add</b>.</description></item>
/// <item><term>Subsequent values</term><description>Each new value replaces the previous: an <b>Update</b> is emitted.</description></item>
/// <item><term>Error</term><description>Terminates the entire output stream.</description></item>
/// <item><term>Completed</term><description>Item remains at last value. No further updates possible.</description></item>
/// </list>
```