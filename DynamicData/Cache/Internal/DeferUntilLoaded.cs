using System;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class DeferUntilLoaded<TObject, TKey>
    {
        private readonly IObservable<IChangeSet<TObject, TKey>> _result;

        public DeferUntilLoaded(IObservableCache<TObject, TKey> source)
        {
            _result = source.CountChanged.Where(count => count != 0)
                .Take(1)
                .Select(_ => new ChangeSet<TObject, TKey>())
                .Concat(source.Connect())
                .NotEmpty();
        }

        public DeferUntilLoaded(IObservable<IChangeSet<TObject, TKey>> source)
        {
            _result = source.MonitorStatus()
                .Where(status => status == ConnectionStatus.Loaded)
                .Take(1)
                .Select(_ => new ChangeSet<TObject, TKey>())
                .Concat(source)
                .NotEmpty();
        }

        public IObservable<IChangeSet<TObject, TKey>> Run()
        {
            return _result;
        }
    }
}