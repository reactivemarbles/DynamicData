using System;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Internal;

namespace DynamicData.List.Internal
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
            return _source.Scan(new ChangeAwareList<T>(), (filtered, changes) =>
             {
                 filtered.Filter(changes, _predicate);
                 return filtered;
             })
            .Select(list => list.CaptureChanges())
            .NotEmpty();
        }
    }
}
