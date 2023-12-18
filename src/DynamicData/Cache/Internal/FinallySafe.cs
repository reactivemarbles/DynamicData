// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class FinallySafe<T>(IObservable<T> source, Action finallyAction)
{
    private readonly Action _finallyAction = finallyAction ?? throw new ArgumentNullException(nameof(finallyAction));

    private readonly IObservable<T> _source = source ?? throw new ArgumentNullException(nameof(source));

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
