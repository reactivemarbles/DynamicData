---
name: add-documentation
description: Use when adding or updating XML documentation for any public API in DynamicData, including new operators, changed behavior, batch documentation passes, or PR reviewer requests for improved docs. Covers extension methods, public classes, interfaces, enums, and any member visible in the published API reference.
---

# Add Documentation

## Overview

Write publication-quality XML documentation for DynamicData public APIs. Every behavioral claim must be traced from the implementation source, not guessed from naming conventions. The documentation will be extracted as HTML and published on the ReactiveUI docs site.

## When to Use

- New operator or public API added
- Existing operator's behavior changed
- Batch documentation pass across a file
- PR reviewer requests improved documentation
- Any public type, interface, enum, or member needs docs

## Core Rule: Additive Only

Never remove existing useful information from comments. You are adding detail, not replacing it. After making changes, diff against main to confirm nothing substantive was lost. If the old docs contained a behavioral detail, a parameter constraint, or a usage note, it must survive in the new version.

## Process

### 1. Analyze the Implementation

Read the actual source code. For every public member, trace each code path. Do not guess from method names or parameter names.

**For changeset operators** (methods extending `IObservable<IChangeSet<...>>`):

Trace what happens for each change reason the operator handles. Cache operators use `ChangeReason` (Add, Update, Remove, Refresh). List operators use `ListChangeReason` (Add, AddRange, Replace, Remove, RemoveRange, Moved, Refresh, Clear). Also trace OnError and OnCompleted propagation.

Key questions per operator:
- What does each change reason produce downstream? Name the exact output ChangeReason.
- Are there conditional outcomes? (e.g., Filter's Update has four possible results depending on old/new predicate evaluation)
- Does the operator create per-item subscriptions? When are they created and disposed?
- What cleanup happens on Remove or disposal?
- Are errors from child subscriptions forwarded or swallowed?
- Does OnCompleted wait for child subscriptions or forward immediately?

Pay special attention to Refresh: its behavior varies significantly between operators. Some re-evaluate (Filter), some forward as-is (Transform by default), some drop entirely (FilterImmutable, TransformImmutable), and some convert to other change types. But all change reasons deserve the same careful tracing.

**For non-changeset public APIs** (classes, interfaces, enums, static helpers):

Document the contract: what does each public member do, what are the preconditions, what does it return, what exceptions can it throw.

### 2. Choose a Template

| The method... | Use this template |
|---|---|
| Extends `IObservable<IChangeSet>` and produces `IObservable<IChangeSet>` | Changeset operator (cache or list) |
| Has multiple input sources (joins, set ops, *OnObservable) | Multi-source operator (separate tables per source) |
| Extends `ISourceCache`/`ISourceList` and returns void | Mutation helper (table describes what's produced) |
| Extends `IObservable<IChangeSet>` but produces non-changeset output (`IObservable<T>`, `IObservable<bool>`, etc.) | Non-changeset operator (table describes subscription lifecycle) |
| Is a class, interface, enum, or other non-extension member | Standard XML docs (summary, params, returns, remarks) |

### 3. Write the XML Comments

#### Summary Tag
1-3 sentences only. What it does and why you'd use it. No behavioral details here; those go in remarks.

#### Param Tags
Every `<param>` MUST mention and link its type via `<see cref="..."/>`:
```xml
/// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
/// <param name="comparer">An <see cref="IComparer{T}"/> that determines sort order.</param>
/// <param name="scheduler">An <see cref="IScheduler"/> for timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
/// <param name="transformFactory">A <see cref="Func{T, TResult}"/> that transforms each source item.</param>
```
Exempt: `bool`, `int`, `string` params where the type is self-evident.

#### Event Table (changeset operators)
Every changeset-processing operator gets a `<list type="table">` inside `<remarks>`.

Cache operators use these rows:
```xml
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>[specific outcome]</description></item>
/// <item><term>Update</term><description>[specific outcome]</description></item>
/// <item><term>Remove</term><description>[specific outcome]</description></item>
/// <item><term>Refresh</term><description>[specific outcome]</description></item>
/// <item><term>OnError</term><description>[forwarded? swallowed? conditional?]</description></item>
/// <item><term>OnCompleted</term><description>[immediate? waits? conditional?]</description></item>
/// </list>
```

List operators include the additional list-specific reasons:
```xml
/// <item><term>AddRange</term><description>[specific outcome]</description></item>
/// <item><term>Replace</term><description>[specific outcome]</description></item>
/// <item><term>RemoveRange</term><description>[specific outcome]</description></item>
/// <item><term>Moved</term><description>[specific outcome]</description></item>
/// <item><term>Clear</term><description>[specific outcome]</description></item>
```
Combine rows with identical behavior (e.g., "Remove/RemoveRange/Clear").

**Multi-source operators** use separate labeled tables:
```xml
/// <para><b>Source changeset handling (parent events):</b></para>
/// <list type="table">...</list>
/// <para><b>Per-item observable handling:</b></para>
/// <list type="table">
/// <listheader><term>Emission</term><description>Behavior</description></listheader>
/// <item><term>First value</term><description>[what appears downstream]</description></item>
/// <item><term>Subsequent values</term><description>[updates? replacements?]</description></item>
/// <item><term>Error</term><description>[terminates? swallowed?]</description></item>
/// <item><term>Completed</term><description>[freezes? removed?]</description></item>
/// </list>
```

**Mutation helpers** describe what changeset is PRODUCED (not consumed).

**Non-changeset output operators** describe how source events affect internal subscriptions.

#### Worth Noting Section
Add `<para><b>Worth noting:</b> ...</para>` for non-obvious behavior: disposal callbacks firing for all tracked items, silent error swallowing, items invisible until first emission, index-stripping, default parameter effects, etc.

#### SeeAlso Tags
Every operator MUST cross-reference (bidirectional for overload sets):
- Other overloads in the same set (primary links to all, each secondary links back)
- Safe/async/immutable variants
- Similar operators solving related problems
- Complementary operators often used together
- Commonly confused operators

#### Type References
- Types and interfaces: `<see cref="IComparer{T}"/>` (linked, discoverable)
- Method names, event names, property names: `<c>Adapt</c>` (inline code, contextual)
- Internal-only types: MUST NOT appear. Describe behavior, not implementation.

#### InheritDoc for Secondary Overloads
```xml
/// <inheritdoc cref="[PrimarySignature]"/>
/// <param name="[delta]">A <see cref="[Type]"/> that [specific difference].</param>
/// <remarks>This overload [what differs]. Delegates to <see cref="[Primary]"/>.</remarks>
```

### 4. Verify

```bash
dotnet build src/DynamicData/DynamicData.csproj --no-restore -c Release --framework net9.0
```
- 0 errors, no new CS1574 warnings (unresolved cref)
- No em dashes (Unicode 0x2014)
- No "The source.</param>" remaining
- All tables use `<listheader><term>Event</term>`
- Spot-check 3-5 operators against their implementation
- Diff against main: confirm no existing useful information was lost

## Before and After Example

**BEFORE** (typical old-style doc):
```xml
/// <summary>
/// Filters the specified source.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <param name="source">The source.</param>
/// <param name="filter">The filter.</param>
/// <returns>An observable which emits change sets.</returns>
```

**AFTER** (publication-quality):
```xml
/// <summary>
/// Filters items from the source changeset stream using a static predicate.
/// Only items satisfying <paramref name="filter"/> are included downstream.
/// </summary>
/// <typeparam name="TObject">The type of items in the cache.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/> to filter.</param>
/// <param name="filter">A <see cref="Func{T, TResult}"/> predicate. Items returning <c>true</c> are included.</param>
/// <returns>A changeset stream containing only items that satisfy <paramref name="filter"/>.</returns>
/// <remarks>
/// <para>Use this overload when the predicate is fixed for the subscription's lifetime.</para>
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>Predicate evaluated. If passes, <b>Add</b> emitted. Otherwise dropped.</description></item>
/// <item><term>Update</term><description>Re-evaluated. Both pass: <b>Update</b>. New passes, old didn't: <b>Add</b>. Old passed, new doesn't: <b>Remove</b>. Neither: dropped.</description></item>
/// <item><term>Remove</term><description>If downstream, <b>Remove</b> emitted. Otherwise dropped.</description></item>
/// <item><term>Refresh</term><description>Re-evaluated. Now passes but didn't: <b>Add</b>. Still passes: <b>Refresh</b> forwarded. No longer passes: <b>Remove</b>. Still fails: dropped.</description></item>
/// <item><term>OnError</term><description>Forwarded.</description></item>
/// <item><term>OnCompleted</term><description>Forwarded.</description></item>
/// </list>
/// <para><b>Worth noting:</b> Refresh events trigger re-evaluation, which can promote or demote items.</para>
/// </remarks>
/// <seealso cref="FilterImmutable{TObject, TKey}"/>
/// <seealso cref="FilterOnObservable{TObject, TKey}"/>
/// <seealso cref="AutoRefresh{TObject, TKey}"/>
```

## Tone Rules

- No em dashes. Use colons, commas, parentheses.
- No emoji.
- No filler: "comprehensive", "robust", "seamlessly", "leverage", "utilize", "facilitate".
- Be specific: "an **Update** is emitted" not "the change is propagated".
- Use "Worth noting" for non-obvious behavior (not "Gotchas").
- Write like a senior developer, not a marketing brochure.

## Batch Application

For large-scale passes across many operators:

1. Dispatch parallel **read-only** agents to analyze implementations (group by family: Transform, Filter, Merge, etc.)
2. Apply edits via a **single agent or yourself** (multiple agents editing one file will clobber each other's work)
3. Build, audit metrics (table count, seealso count, em dashes, old params), spot-check accuracy
4. Diff against main for lost details

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Guessing behavior from method name | Read the implementation file |
| `<c>IComparer</c>` for a type | `<see cref="IComparer{T}"/>` |
| Referencing internal types in public docs | Describe behavior instead |
| One-way seealso links | Make them bidirectional |
| Multiple agents editing same file | One editor at a time |
| Removing existing useful information | Additive only; diff against main |
| Tables with only cache rows for list operators | List needs AddRange, RemoveRange, Moved, Clear too |
| Params missing type links | Every param mentions and links its type |