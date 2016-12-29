using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal class OnItemRemoved<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Action<TObject> _removeAction;

        public OnItemRemoved([NotNull] IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> removeAction)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (removeAction == null) throw new ArgumentNullException(nameof(removeAction));
            _source = source;
            _removeAction = removeAction;
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                var cache = new Cache<TObject, TKey>();
                var subscriber = _source
                    .Do(changes => RegisterForRemoval(changes, cache), observer.OnError)
                    .SubscribeSafe(observer);

                return Disposable.Create(() =>
                {
                    cache.Items.ForEach(t => _removeAction(t));
                    cache.Clear();
                    subscriber.Dispose();
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