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

Never remove existing useful information from comments. After making changes, diff against main to confirm nothing substantive was lost.

## Unified Template

Start from this template for any primary overload. Delete sections that do not apply.

```xml
/// <summary>
/// [1-3 sentences: what it does, why you'd use it. No behavioral details.]
/// </summary>
/// <typeparam name="TObject">The type of items.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
/// <param name="exampleParam">An <see cref="IComparer{T}"/> that [what it controls].</param>
/// <returns>[What it emits and what each emission represents.]</returns>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
/// <remarks>
/// <para>[When to use. How it differs from alternatives. Multiple paragraphs fine.]</para>
///
/// <!-- SINGLE-SOURCE CHANGESET OPERATOR: one event table -->
/// <!-- MULTI-SOURCE OPERATOR: separate labeled tables per source (see below) -->
/// <!-- MUTATION HELPER: table describes what changeset is PRODUCED -->
/// <!-- NON-CHANGESET OUTPUT: table describes how source events affect subscriptions -->
///
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <!-- Cache operators -->
/// <item><term>Add</term><description>[specific outcome, bold output reasons]</description></item>
/// <item><term>Update</term><description>[specific outcome]</description></item>
/// <item><term>Remove</term><description>[specific outcome, mention cleanup]</description></item>
/// <item><term>Refresh</term><description>[specific outcome, trace carefully]</description></item>
/// <!-- List operators: also include these -->
/// <item><term>AddRange</term><description>[specific outcome]</description></item>
/// <item><term>Replace</term><description>[list equivalent of Update]</description></item>
/// <item><term>RemoveRange</term><description>[specific outcome]</description></item>
/// <item><term>Moved</term><description>[specific outcome]</description></item>
/// <item><term>Clear</term><description>[specific outcome]</description></item>
/// <!-- All operators -->
/// <item><term>OnError</term><description>[forwarded? swallowed? conditional?]</description></item>
/// <item><term>OnCompleted</term><description>[immediate? waits for children?]</description></item>
/// </list>
///
/// <!-- MULTI-SOURCE: use this pattern instead of the single table above -->
/// <para><b>Source changeset handling (parent events):</b></para>
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>[subscribes to child / creates state]</description></item>
/// <item><term>Update</term><description>[disposes old, subscribes to new]</description></item>
/// <item><term>Remove</term><description>[disposes, emits downstream removes]</description></item>
/// <item><term>Refresh</term><description>[no effect / re-evaluates]</description></item>
/// </list>
/// <para><b>Per-item observable handling:</b></para>
/// <list type="table">
/// <listheader><term>Emission</term><description>Behavior</description></listheader>
/// <item><term>First value</term><description>[what appears downstream]</description></item>
/// <item><term>Subsequent values</term><description>[updates? replacements?]</description></item>
/// <item><term>Error</term><description>[terminates? swallowed?]</description></item>
/// <item><term>Completed</term><description>[freezes? removed?]</description></item>
/// </list>
///
/// <para><b>Worth noting:</b> [Non-obvious behavior, edge cases, disposal semantics.]</para>
/// </remarks>
/// <seealso cref="[OtherOverloadsInSet]"/>
/// <seealso cref="[SafeOrAsyncVariant]"/>
/// <seealso cref="[SimilarOperator]"/>
/// <seealso cref="[ComplementaryOperator]"/>
```

For **secondary overloads** in an overload set:
```xml
<!-- When the overload delegates to the primary (simpler signature, fewer params): -->
/// <inheritdoc cref="[PrimarySignature]"/>
/// <param name="[delta]">A <see cref="[Type]"/> that [specific difference].</param>
/// <remarks>This overload [what differs]. Delegates to <see cref="[Primary]"/>.</remarks>

<!-- When the overload has a different implementation (different input type, different behavior): -->
<!-- Use the full unified template above. It needs its own complete documentation. -->
<!-- Still link to the other overloads via seealso for discoverability. -->
```

## Process

### 1. Analyze the Implementation

Read the actual source code. Trace each code path for every change reason the operator handles.

Key questions:
- What does each change reason produce downstream? Name the exact output.
- Are there conditional outcomes? (e.g., Filter's Update has four possible results)
- Does the operator create per-item subscriptions? When created/disposed?
- Are errors from child subscriptions forwarded or swallowed?
- Does OnCompleted wait for child subscriptions?

Refresh behavior varies significantly between operators: some re-evaluate (Filter), some forward as-is (Transform by default), some drop (FilterImmutable), some convert to other change types. But all change reasons deserve the same careful tracing.

### 2. Choose What to Include

| The method... | Template sections to use |
|---|---|
| Extends `IObservable<IChangeSet>`, produces `IObservable<IChangeSet>` | Single event table (cache or list rows) |
| Has multiple input sources or per-item observables | Multi-source tables (parent + child) |
| Extends `ISourceCache`/`ISourceList`, returns void | Single table, framed as "produced" |
| Produces non-changeset output (`IObservable<T>`, `bool`, etc.) | Single table, framed as subscription lifecycle |
| Is a class, interface, enum, or non-extension member | Summary, params, returns, remarks (no event table) |

### 3. Apply Quality Rules

**Params**: Every `<param>` must read as natural English with the type linked via `<see cref="..."/>` woven into the sentence. No type is exempt from linking (including enums, `Optional`, `Change`, `IChangeSet`, standard library types like `IComparer`, `TimeSpan`, `IScheduler`). Use `<see langword="true"/>` / `<see langword="false"/>` / `<see langword="null"/>` for C# keywords.

Param writing rules:
- Start with an article ("The", "A", "An") or a condition ("When", "If")
- The type link appears naturally in the sentence, not as a prefix dumped before the description
- Never echo the parameter name as the entire description ("The source.", "The destination.")
- For `IObservable<IChangeSet<T,K>>` source params, use the two-part format: `The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.`
- For deeply nested generics (3+ levels), use `{T}` in the cref and describe the actual type in prose
- For `params` array parameters, do not include `[]` in the cref. Mention "array" in prose if needed.
- For `Optional.None` references, use `<see cref="Optional.None{T}"/>`

```xml
<!-- BAD: type dumped as prefix, meaningless description -->
/// <param name="source"><see cref="IObservable{T}"/> the source.</param>
/// <param name="destination"><see cref="IObservable{T}"/> the destination.</param>
/// <param name="options">A <see cref="BindingOptions"/> that  The binding options.</param>

<!-- GOOD: natural English with type links woven in -->
/// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
/// <param name="destination">The <see cref="IObservableCollection{TObject}"/> that will receive the changes.</param>
/// <param name="options">The <see cref="BindingOptions"/> that controls binding behavior.</param>
/// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
/// <param name="equalityComparer">The <see cref="IEqualityComparer{TObject}"/> used to determine whether a new item is the same as an existing cached item.</param>
```

**SeeAlso**: Bidirectional for overload sets. Link safe/async/immutable variants, similar operators, complementary operators, commonly confused operators.

**Type references**: All types use `<see cref="..."/>`, including enums, structs, and standard library types. Method/event/property names use `<c>...</c>`. Internal types must not appear; describe behavior instead.

**Tone**: No em dashes. No emoji. No filler words (comprehensive, robust, seamlessly, leverage, utilize, facilitate). Be specific: "an **Update** is emitted" not "the change is propagated". Use "Worth noting" for non-obvious behavior.

### 4. Verify

```bash
dotnet build src/DynamicData/DynamicData.csproj --no-restore -c Release --framework net9.0
```
- 0 errors, no new CS1574 warnings
- No em dashes (Unicode 0x2014)
- No "The source.</param>" remaining
- Spot-check 3-5 operators against implementation
- Diff against main: confirm nothing lost

## Before and After

**BEFORE** (old-style):
```xml
/// <summary>
/// Filters the specified source.
/// </summary>
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
/// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
/// <param name="filter">A <see cref="Func{T, TResult}"/> predicate. Items returning <c>true</c> are included.</param>
/// <returns>A changeset stream containing only items satisfying <paramref name="filter"/>.</returns>
/// <remarks>
/// <para>Use this overload when the predicate is fixed for the subscription's lifetime.</para>
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>Predicate evaluated. If passes, <b>Add</b> emitted. Otherwise dropped.</description></item>
/// <item><term>Update</term><description>Re-evaluated. Both pass: <b>Update</b>. New passes only: <b>Add</b>. Old passed only: <b>Remove</b>. Neither: dropped.</description></item>
/// <item><term>Remove</term><description>If downstream, <b>Remove</b> emitted. Otherwise dropped.</description></item>
/// <item><term>Refresh</term><description>Re-evaluated. Now passes: <b>Add</b>. Still passes: <b>Refresh</b>. No longer passes: <b>Remove</b>. Still fails: dropped.</description></item>
/// <item><term>OnError</term><description>Forwarded.</description></item>
/// <item><term>OnCompleted</term><description>Forwarded.</description></item>
/// </list>
/// <para><b>Worth noting:</b> Refresh re-evaluation can promote or demote items.</para>
/// </remarks>
/// <seealso cref="FilterImmutable{TObject, TKey}"/>
/// <seealso cref="FilterOnObservable{TObject, TKey}"/>
/// <seealso cref="AutoRefresh{TObject, TKey}"/>
```

## Batch Application

1. Dispatch parallel **read-only** agents to analyze implementations (group by family)
2. Apply edits via **single agent or yourself** (multiple agents editing one file clobber each other)
3. Build, audit metrics, spot-check accuracy
4. Diff against main for lost details

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Guessing behavior from method name | Read the implementation |
| `<c>IComparer</c>` for a type | `<see cref="IComparer{T}"/>` |
| Referencing internal types | Describe behavior instead |
| One-way seealso links | Bidirectional |
| Multiple agents editing same file | One editor at a time |
| Removing existing information | Additive only; diff against main |
| Only cache rows for list operators | List needs AddRange, RemoveRange, Moved, Clear |
| Params missing type links | Every param links its type |