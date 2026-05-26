// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableCache extensions for Batch and BatchIf.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Collects changesets emitted within a time window and merges them into a single changeset.
    /// Uses Rx's <c>Buffer</c> operator followed by <see cref="FlattenBufferResult{TObject, TKey}"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to batch.</param>
    /// <param name="timeSpan">The <see cref="TimeSpan"/> time window for batching.</param>
    /// <param name="scheduler">The scheduler for timing. Defaults to <see cref="GlobalConfig.DefaultScheduler"/>.</param>
    /// <returns>An observable that emits merged changesets, one per time window.</returns>
    /// <remarks>
    /// <para>
    /// All changesets received during the time window are concatenated into a single changeset.
    /// This is useful for reducing UI update frequency when the source emits many rapid changes.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Buffered and included in the merged changeset at the end of the time window.</description></item>
    /// <item><term>Update</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>Remove</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>Refresh</term><description>Buffered and included in the merged changeset.</description></item>
    /// <item><term>OnCompleted</term><description>Any remaining buffered changes are flushed, then completion is forwarded.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> The merged changeset may contain contradictory changes (e.g., Add then Remove for the same key). Downstream operators handle this correctly, but raw inspection of the changeset may be surprising.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="BufferInitial{TObject, TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> Batch<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TimeSpan timeSpan, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Buffer(timeSpan, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult();
    }

    /// <inheritdoc cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload delegates to the primary overload with <c>initialPauseState: false</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => BatchIf(source, pauseIfTrueSelector, false, scheduler);

    /// <inheritdoc cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload delegates to the primary overload with default <c>initialPauseState: false</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, null, initialPauseState, scheduler: scheduler).Run();

    /// <inheritdoc cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>This overload omits <c>initialPauseState</c> (defaults to <see langword="false"/>) but accepts a timeout.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => BatchIf(source, pauseIfTrueSelector, false, timeOut, scheduler);

    /// <summary>
    /// Conditionally buffers changesets while a pause signal is active, then flushes all buffered
    /// changes as a single merged changeset when the signal resumes.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to conditionally buffer.</param>
    /// <param name="pauseIfTrueSelector">An <see cref="IObservable{bool}"/> that when <see langword="true"/>, buffering begins. When <see langword="false"/>, the buffer is flushed.</param>
    /// <param name="initialPauseState">If <see langword="true"/>, starts in a paused (buffering) state.</param>
    /// <param name="timeOut">A <see cref="TimeSpan"/> that maximum time the buffer stays open. When elapsed, the buffer is flushed regardless of pause state.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for timeout timing.</param>
    /// <returns>An observable that emits changesets, buffered or passthrough depending on pause state.</returns>
    /// <remarks>
    /// <para>
    /// While paused, incoming changesets are accumulated. On resume (or timeout), all buffered changesets
    /// are merged into a single changeset and emitted. While not paused, changesets pass through immediately.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Buffered while paused; forwarded immediately while active.</description></item>
    /// <item><term>Update</term><description>Buffered while paused; forwarded immediately while active.</description></item>
    /// <item><term>Remove</term><description>Buffered while paused; forwarded immediately while active.</description></item>
    /// <item><term>Refresh</term><description>Buffered while paused; forwarded immediately while active.</description></item>
    /// <item><term>OnError</term><description>Buffered data is lost.</description></item>
    /// <item><term>OnCompleted</term><description>Any remaining buffered data is flushed before completion.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> If the source completes while paused, buffered data IS flushed before OnCompleted. However, if the source errors while paused, buffered data is lost.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="pauseIfTrueSelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Batch{TObject, TKey}"/>
    /// <seealso cref="BufferInitial{TObject, TKey}"/>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, TimeSpan? timeOut = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

        return new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, timeOut, initialPauseState, scheduler: scheduler).Run();
    }

    /// <inheritdoc cref="BatchIf{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to conditionally buffer.</param>
    /// <param name="pauseIfTrueSelector">An <see cref="IObservable{bool}"/> that controls buffering: <see langword="true"/> begins buffering, <see langword="false"/> flushes the buffer.</param>
    /// <param name="initialPauseState">If <see langword="true"/>, starts in a paused (buffering) state.</param>
    /// <param name="timer">An optional <see cref="IObservable{Unit}"/> timer. The buffer is flushed each time the timer produces a value, and buffering ceases when it completes.</param>
    /// <param name="scheduler">An optional <see cref="IScheduler"/> for scheduling work.</param>
    /// <remarks>This overload accepts an explicit timer observable instead of a <see cref="TimeSpan"/> timeout.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> BatchIf<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, IObservable<Unit>? timer = null, IScheduler? scheduler = null)
        where TObject : notnull
        where TKey : notnull => new BatchIf<TObject, TKey>(source, pauseIfTrueSelector, null, initialPauseState, timer, scheduler).Run();
}
