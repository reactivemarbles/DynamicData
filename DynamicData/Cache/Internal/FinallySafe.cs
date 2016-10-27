using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Cache.Internal
{
    internal class FinallySafe<T>
    {
        private readonly IObservable<T> _source;
        private readonly Action _finallyAction;

        public FinallySafe([NotNull] IObservable<T> source, [NotNull] Action finallyAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (finallyAction == null) throw new ArgumentNullException(nameof(finallyAction));
            _source = source;
            _finallyAction = finallyAction;
        }

        public IObservable<T> Run()
        {
            return Observable.Create<T>(o =>
            {
                var finallyOnce = Disposable.Create(_finallyAction);

                var subscription = _source.Subscribe(o.OnNext,
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
    }
}