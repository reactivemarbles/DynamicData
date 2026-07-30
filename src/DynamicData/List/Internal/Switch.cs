// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the Switch class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="sources">The sources value.</param>
internal sealed class Switch<T>(IObservable<IObservable<IChangeSet<T>>> sources)
    where T : notnull
{
    /// <summary>
    /// The _sources field.
    /// </summary>
    private readonly IObservable<IObservable<IChangeSet<T>>> _sources = sources ?? throw new ArgumentNullException(nameof(sources));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = InternalEx.NewLock();

                var destination = new SourceList<T>();

                var populator = Observable.Switch(
                    _sources.Do(
                        _ =>
                        {
                            lock (locker)
                            {
                                destination.Clear();
                            }
                        })).Synchronize(locker).PopulateInto(destination);

                var publisher = destination.Connect().SubscribeSafe(observer);
                return new CompositeDisposable(destination, populator, publisher);
            });
}
