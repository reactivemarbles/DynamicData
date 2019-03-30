using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class DisposeMany<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Action<TObject> _removeAction;

        public DisposeMany([NotNull] IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _removeAction = removeAction ?? throw new ArgumentNullException(nameof(removeAction));
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var locker = new object();
                var cache = new Cache<TObject, TKey>();
                var subscriber = _source
                    .Synchronize(locker)
                    .Do(changes => RegisterForRemoval(changes, cache), observer.OnError)
                    .SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    subscriber.Dispose();

                    lock (locker)
                    {
                        cache.Items.ForEach(t => _removeAction(t));
                        cache.Clear();
                    }
                });
            });
        }

        private void RegisterForRemoval(IChangeSet<TObject, TKey> changes, Cache<TObject, TKey> cache)
        {
            changes.ForEach(change =>
            {
                switch (change.Reason)
                {
                    case ChangeReason.Update:
                        change.Previous.IfHasValue(t => _removeAction(t));
                        break;
                    case ChangeReason.Remove:
                        _removeAction(change.Current);
                        break;
                }
            });
            cache.Clone(changes);
        }
    }
}