// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the FinallySafe class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="finallyAction">The finallyAction value.</param>
internal sealed class FinallySafe<T>(IObservable<T> source, Action finallyAction)
{
    /// <summary>
    /// The _finallyAction field.
    /// </summary>
    private readonly Action _finallyAction = finallyAction ?? throw new ArgumentNullException(nameof(finallyAction));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<T> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<T> Run() => Observable.Create<T>(
            o =>
            {
                var finallyOnce = Disposable.Create(_finallyAction);

                var subscription = _source.Subscribe(
                    o.OnNext,
                    ex =>
                    {
                        try
                        {
                            o.OnError(ex);
                        }
                        finally
                        {
                            finallyOnce.Dispose();
                        }
                    },
                    () =>
                    {
                        try
                        {
                            o.OnCompleted();
                        }
                        finally
                        {
                            finallyOnce.Dispose();
                        }
                    });

                return new CompositeDisposable(subscription, finallyOnce);
            });
}
