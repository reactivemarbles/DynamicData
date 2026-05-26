// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// ObservableList extensions for changeset stream lifecycle helpers and buffering.
/// </summary>
public static partial class ObservableListEx
{
    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>
    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <para>This overload starts unpaused and has no timeout.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, IScheduler? scheduler = null)
        where T : notnull => BufferIf(source, pauseIfTrueSelector, false, scheduler);

    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>
    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <para>This overload allows setting the initial pause state but has no timeout.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

        return BufferIf(source, pauseIfTrueSelector, initialPauseState, null, scheduler);
    }

    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <remarks>
    /// <inheritdoc cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, bool, TimeSpan?, IScheduler?)"/>
    /// <para>This overload starts unpaused and accepts a timeout but not an explicit initial pause state.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, TimeSpan? timeOut, IScheduler? scheduler = null)
        where T : notnull => BufferIf(source, pauseIfTrueSelector, false, timeOut, scheduler);

    /// <summary>
    /// Buffers changeset notifications while a pause signal is active, then flushes all buffered changes when resumed.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to conditionally buffer.</param>
    /// <param name="pauseIfTrueSelector">An <see cref="IObservable{bool}"/> of <see cref="bool"/> that controls buffering: <see langword="true"/> pauses (buffers), <see langword="false"/> resumes (flushes).</param>
    /// <param name="initialPauseState">The initial pause state. When <see langword="true"/>, buffering starts immediately.</param>
    /// <param name="timeOut">An optional <see cref="TimeSpan"/> maximum duration to keep the buffer open. After this time, the buffer is flushed regardless of pause state.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for timeout scheduling.</param>
    /// <returns>A list changeset stream that buffers during pause and emits combined changesets on resume.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="pauseIfTrueSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// All changeset events are buffered at the changeset level (not individual changes) while paused.
    /// On resume, all buffered changesets are emitted as a single combined changeset. If the buffer is empty on resume,
    /// no emission occurs.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Any (while paused)</term><description>Accumulated in an internal buffer. Not emitted downstream.</description></item>
    /// <item><term>Any (while active)</term><description>Passed through immediately.</description></item>
    /// <item><term>Pause selector emits false</term><description>All buffered changesets are flushed downstream as one combined changeset.</description></item>
    /// <item><term>Timeout fires</term><description>Automatically resumes and flushes the buffer.</description></item>
    /// <item><term>OnError</term><description>Forwarded immediately (not buffered).</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded immediately.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Each pause/resume cycle re-arms the timeout. Rapid toggling can create many small buffer windows.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> BufferIf<T>(this IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState, TimeSpan? timeOut, IScheduler? scheduler = null)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        pauseIfTrueSelector.ThrowArgumentNullExceptionIfNull(nameof(pauseIfTrueSelector));

        return new BufferIf<T>(source, pauseIfTrueSelector, initialPauseState, timeOut, scheduler).Run();
    }

    /// <summary>
    /// Buffers changesets during an initial time window, then emits a single combined changeset and passes through subsequent changes.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to buffer during the initial loading period.</param>
    /// <param name="initialBuffer">The <see cref="TimeSpan"/> time period (measured from first emission) during which changes are buffered.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> for timing the buffer window.</param>
    /// <returns>A list changeset stream where the initial burst is combined into one changeset.</returns>
    /// <remarks>
    /// <para>
    /// For a configured duration after the first emission, all changesets are buffered and combined into a single emission.
    /// After this initial window, subsequent changesets pass through immediately.
    /// </para>
    /// </remarks>
    /// <seealso cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="FlattenBufferResult{T}(IObservable{IList{IChangeSet{T}}})"/>
    public static IObservable<IChangeSet<TObject>> BufferInitial<TObject>(this IObservable<IChangeSet<TObject>> source, TimeSpan initialBuffer, IScheduler? scheduler = null)
        where TObject : notnull => source.DeferUntilLoaded().Publish(
            shared =>
            {
                var initial = shared.Buffer(initialBuffer, scheduler ?? GlobalConfig.DefaultScheduler).FlattenBufferResult().Take(1);

                return initial.Concat(shared);
            });

    /// <summary>
    /// Defers downstream delivery until the source emits its first changeset, then forwards all subsequent changesets.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to defer until the first changeset arrives.</param>
    /// <returns>A list changeset stream that begins emitting only after the source has produced its first changeset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Subscribes to the source immediately but buffers internally until the first changeset arrives, at which point it emits
    /// the initial data and all subsequent changesets. This is useful when downstream consumers should not receive an empty initial state.
    /// </para>
    /// </remarks>
    /// <seealso cref="SkipInitial{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DeferUntilLoaded<T>(source).Run();
    }

    /// <inheritdoc cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <remarks>
    /// <inheritdoc cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <para>Convenience overload that calls <c>source.Connect().DeferUntilLoaded()</c>.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> DeferUntilLoaded<T>(this IObservableList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Connect().DeferUntilLoaded();
    }

    /// <summary>
    /// Suppresses empty changesets from the stream. Only changesets with at least one change are forwarded.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to suppress empty changesets.</param>
    /// <returns>A list changeset stream with empty changesets filtered out.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="WhereReasonsAre{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    public static IObservable<IChangeSet<T>> NotEmpty<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Where(s => s.Count != 0);
    }

    /// <summary>
    /// Skips the initial changeset (the snapshot emitted on subscription) and forwards all subsequent changesets.
    /// Internally defers until loaded, then skips the first emission.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to skip the initial changeset.</param>
    /// <returns>A list changeset stream that omits the initial snapshot.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>Warning:</b> This operator assumes the initial changeset is empty. If the source emits a non-empty
    /// initial snapshot, those items are silently dropped while downstream consumers remain unaware of them.
    /// Any later <b>Refresh</b>, <b>Replace</b>, <b>Remove</b>, or <b>Moved</b> change targeting one of those
    /// dropped items will throw because the downstream collection has no record of them. Only use this against
    /// a source you know starts empty (for example, a <see cref="ISourceList{T}"/> that has not yet been populated).
    /// </para>
    /// </remarks>
    /// <seealso cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="StartWithEmpty{T}(IObservable{IChangeSet{T}})"/>
    public static IObservable<IChangeSet<T>> SkipInitial<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.DeferUntilLoaded().Skip(1);
    }

    /// <summary>
    /// Prepends an empty changeset to the source stream. Useful for initializing downstream consumers that expect an initial emission.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to prepend an empty changeset to.</param>
    /// <returns>A list changeset stream that begins with an empty changeset.</returns>
    /// <seealso cref="DeferUntilLoaded{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="SkipInitial{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ObservableCacheEx.StartWithEmpty{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}})"/>
    public static IObservable<IChangeSet<T>> StartWithEmpty<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => source.StartWith(ChangeSet<T>.Empty);
}
