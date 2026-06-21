// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
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
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(pauseIfTrueSelector);

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
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(pauseIfTrueSelector);

        return new BufferIf<T>(source, pauseIfTrueSelector, initialPauseState, timeOut, scheduler).Run();
    }
}
