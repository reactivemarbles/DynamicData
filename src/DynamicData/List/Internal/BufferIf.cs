// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the BufferIf class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="pauseIfTrueSelector">The pauseIfTrueSelector value.</param>
/// <param name="initialPauseState">The initialPauseState value.</param>
/// <param name="timeOut">The timeOut value.</param>
/// <param name="scheduler">The scheduler value.</param>
internal sealed class BufferIf<T>(IObservable<IChangeSet<T>> source, IObservable<bool> pauseIfTrueSelector, bool initialPauseState = false, TimeSpan? timeOut = null, IScheduler? scheduler = null)
    where T : notnull
{
    /// <summary>
    /// The _pauseIfTrueSelector field.
    /// </summary>
    private readonly IObservable<bool> _pauseIfTrueSelector = pauseIfTrueSelector ?? throw new ArgumentNullException(nameof(pauseIfTrueSelector));

    /// <summary>
    /// The _scheduler field.
    /// </summary>
    private readonly IScheduler _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// The _timeOut field.
    /// </summary>
    private readonly TimeSpan _timeOut = timeOut ?? TimeSpan.Zero;

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = InternalEx.NewLock();
                var paused = initialPauseState;
                var buffer = new ChangeSet<T>();
                var timeoutSubscriber = new SerialDisposable();
                var timeoutSubject = new Signal<bool>();

                var bufferSelector = Observable.Return(initialPauseState).Concat(_pauseIfTrueSelector.Merge(timeoutSubject)).ObserveOn(_scheduler).Synchronize(locker).Publish();

                var pause = bufferSelector.Where(state => state).Subscribe(
                    _ =>
                    {
                        paused = true;

                        // add pause timeout if required
                        if (_timeOut != TimeSpan.Zero)
                        {
                            timeoutSubscriber.Disposable = Observable.Timer(_timeOut, _scheduler).Select(_ => false).SubscribeSafe(timeoutSubject);
                        }
                    });

                var resume = bufferSelector.Where(state => !state).Subscribe(
                    _ =>
                    {
                        paused = false;

                        // publish changes and clear buffer
                        if (buffer.Count == 0)
                        {
                            return;
                        }

                        observer.OnNext(buffer);
                        buffer = [];

                        // kill off timeout if required
                        timeoutSubscriber.Disposable = Disposable.Empty;
                    });

                var updateSubscriber = _source.Synchronize(locker).Subscribe(
                    updates =>
                    {
                        if (paused)
                        {
                            buffer.AddRange(updates);
                        }
                        else
                        {
                            observer.OnNext(updates);
                        }
                    });

                var connected = bufferSelector.Connect();

                return Disposable.Create(
                    () =>
                    {
                        connected.Dispose();
                        pause.Dispose();
                        resume.Dispose();
                        updateSubscriber.Dispose();
                        timeoutSubject.OnCompleted();
                        timeoutSubscriber.Dispose();
                    });
            });
}
