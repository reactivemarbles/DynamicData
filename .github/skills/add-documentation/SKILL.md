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

## Process

### 1. Analyze the Implementation

Read the actual source code. For every public member, trace the code paths.

**For changeset operators** (methods extending `IObservable<IChangeSet<...>>`):

Trace what happens for each change reason the operator handles. Cache operators use `ChangeReason` (Add, Update, Remove, Refresh). List operators use `ListChangeReason` (Add, AddRange, Replace, Remove, RemoveRange, Moved, Refresh, Clear). Also trace OnError and OnCompleted propagation.

Key questions per operator:
- Does Refresh re-evaluate? Forward as-is? Drop? Convert to Add/Remove?
- Are child/per-item errors swallowed or forwarded?
- Does OnCompleted wait for child subscriptions?
- What cleanup happens on Remove or disposal?

**For non-changeset public APIs** (classes, interfaces, enums, static helpers):

Document the contract: what does each public member do, what are the preconditions, what does it return, what exceptions can it throw.

### 2. Write the XML Comments

#### Summary Tag
1-3 sentences only. What it does and why you'd use it. No behavioral details here; those go in remarks.

#### Param Tags
Every `<param>` MUST mention and link its type:
```xml
/// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
/// <param name="comparer">An <see cref="IComparer{T}"/> that determines sort order.</param>
/// <param name="scheduler">An <see cref="IScheduler"/> for timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
```
Exempt: `bool`, `int`, `string` params where the type is self-evident.

#### Event Table (changeset operators)
Every changeset-processing operator gets a `<list type="table">` inside `<remarks>`:

```xml
/// <list type="table">
/// <listheader><term>Event</term><description>Behavior</description></listheader>
/// <item><term>Add</term><description>[specific outcome, bold output ChangeReasons]</description></item>
/// ...OnError and OnCompleted rows...
/// </list>
```

Cache operators: rows for Add, Update, Remove, Refresh, OnError, OnCompleted.
List operators: rows for Add, AddRange, Replace, Remove, RemoveRange, Moved, Refresh, Clear, OnError, OnCompleted. Combine rows with identical behavior (e.g., "Remove/RemoveRange/Clear").

**Multi-source operators** (joins, set ops, MergeManyChangeSets, *OnObservable): use separate labeled tables:
```xml
/// <para><b>Source changeset handling (parent events):</b></para>
/// <list type="table">...</list>
/// <para><b>Per-item observable handling:</b></para>
/// <list type="table">...</list>
```

**Mutation helpers** (AddOrUpdate, Remove, Clear, etc.): table describes what changeset is PRODUCED.

**Non-changeset output operators** (MergeMany, Watch, property observers): table describes how source events affect subscriptions.

#### Worth Noting Section
Add `<para><b>Worth noting:</b> ...</para>` for non-obvious behavior: disposal callbacks, silent error swallowing, items invisible until first emission, index-stripping, etc.

#### SeeAlso Tags
Every operator MUST cross-reference (bidirectional for overload sets):
- Other overloads in the same set (primary links to all, each secondary links back)
- Safe/async/immutable variants
- Similar operators solving related problems
- Complementary operators often used together
- Commonly confused operators

#### Type References
Types and interfaces: `<see cref="IComparer{T}"/>`. Method names and events: `<c>Adapt</c>`. Internal-only types MUST NOT appear. Describe behavior, not implementation.

#### InheritDoc for Secondary Overloads
```xml
/// <inheritdoc cref="[PrimarySignature]"/>
/// <param name="[delta]">A <see cref="[Type]"/> that [specific difference].</param>
/// <remarks>This overload [what differs]. Delegates to <see cref="[Primary]"/>.</remarks>
```

### 3. Verify

```bash
dotnet build src/DynamicData/DynamicData.csproj --no-restore -c Release --framework net9.0
```
- 0 errors, no new CS1574 warnings (unresolved cref)
- No em dashes (Unicode 0x2014)
- No "The source.</param>" remaining
- All tables use `<listheader><term>Event</term>`
- Spot-check 3-5 operators against their implementation

### 4. Diff Against Main

Confirm no important details were lost from original comments. The overhaul must be additive.

## Tone Rules

- No em dashes. Use colons, commas, parentheses.
- No emoji.
- No filler: "comprehensive", "robust", "seamlessly", "leverage", "utilize", "facilitate".
- Be specific: "an **Update** is emitted" not "the change is propagated".
- Use "Worth noting" for non-obvious behavior (not "Gotchas").

## Batch Application

For large-scale passes across many operators:

1. Dispatch parallel **read-only** agents to analyze implementations (group by family: Transform, Filter, Merge, etc.)
2. Apply edits via a **single agent or yourself** (multiple agents editing one file will clobber each other)
3. Build, audit metrics (table count, seealso count, em dashes, old params), spot-check accuracy
4. Diff against main for lost details

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Guessing behavior from method name | Read the implementation file |
| Missing Refresh row (the tricky one) | Always trace Refresh explicitly |
| `<c>IComparer</c>` for a type | `<see cref="IComparer{T}"/>` |
| Referencing internal types | Describe behavior instead |
| One-way seealso links | Make them bidirectional |
| Multiple agents editing same file | One editor at a time |
| Tables with only 4 cache rows for list operators | List needs AddRange, RemoveRange, Moved, Clear too |