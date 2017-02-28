using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class SpecifiedGrouper<TObject, TKey, TGroupKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _source;
        private readonly Func<TObject, TGroupKey> _groupSelector;
        private readonly IObservable<IDistinctChangeSet<TGroupKey>> _resultGroupSource;

        public SpecifiedGrouper(IObservable<IChangeSet<TObject, TKey>> source,
            Func<TObject, TGroupKey> groupSelector,
            IObservable<IDistinctChangeSet<TGroupKey>> resultGroupSource)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (groupSelector == null) throw new ArgumentNullException(nameof(groupSelector));
            if (resultGroupSource == null) throw new ArgumentNullException(nameof(resultGroupSource));

            _source = source;
            _groupSelector = groupSelector;
            _resultGroupSource = resultGroupSource;
        }

        public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run()
        {
            return Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>
                (
                    observer =>
                    {
                        var locker = new object();

                        //create source group cache
                        var sourceGroups = _source.Synchronize(locker)
                            .Group(_groupSelector)
                            .DisposeMany()
                            .AsObservableCache();

                        //create parent groups
                        var parentGroups = _resultGroupSource.Synchronize(locker)
                            .Transform(x =>
                            {
                                //if child already has data, populate it.
                                var result = new ManagedGroup<TObject, TKey, TGroupKey>(x);
                                var child = sourceGroups.Lookup(x);
                                if (child.HasValue)
                                {
                                    //dodgy cast but fine as a groups is always a ManagedGroup;
                                    var group = (ManagedGroup<TObject, TKey, TGroupKey>)child.Value;
                                    result.Update(updater => updater.Update(group.GetInitialUpdates()));
                                }
                                return result;
                            })
                            .DisposeMany()
                            .AsObservableCache();

                        //connect to each individual item and update the resulting group
                        var updateFromcChilds = sourceGroups.Connect()
                            .SubscribeMany(x => x.Cache.Connect().Subscribe(updates =>
                            {
                                var groupToUpdate = parentGroups.Lookup(x.Key);
                                if (groupToUpdate.HasValue)
                                {
                                    groupToUpdate.Value.Update(updater => updater.Update(updates));
                                }
                            }))
                            .DisposeMany()
                            .Subscribe();

                        var notifier = parentGroups
                            .Connect()
                            .Select(x =>
                            {
                                var groups = x.Select(s => new Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>(s.Reason, s.Key, s.Current));
                                return new GroupChangeSet<TObject, TKey, TGroupKey>(groups);
                            })
                            .SubscribeSafe(observer);

                        return Disposable.Create(() =>
                        {
                            notifier.Dispose();
                            sourceGroups.Dispose();
                            parentGroups.Dispose();
                            updateFromcChilds.Dispose();
                        });
                    });
        }
    }
}