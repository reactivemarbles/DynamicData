using System;
using System.Reactive.Linq;
using DynamicData.Annotations;

namespace DynamicData.Internal
{
    internal class ImmutableFilter<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly Func<T, bool> _predicate;


        public ImmutableFilter([NotNull] IObservable<IChangeSet<T>> source, [NotNull] Func<T, bool> predicate)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            _source = source;
            _predicate = predicate;
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var filtered = new ChangeAwareList<T>();
                 return _source.Select(changes =>
                {
                    filtered.Filter(changes, _predicate);
                    return filtered.CaptureChanges();
                }).NotEmpty().SubscribeSafe(observer);
            });
        }
    }
}
