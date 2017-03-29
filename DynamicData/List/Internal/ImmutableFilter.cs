using System;
using System.Reactive.Linq;
using DynamicData.Annotations;

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
            /*
             * Apply the transform operator so 'IsMatch' state can be evalutated and captured one time only
             * This is to eliminate the need to re-apply the predicate when determining whether an item was previously matched
            */
            return _source.Transform(t => new ItemWithMatch(t, _predicate(t)))
                .Scan(new ChangeAwareList<ItemWithMatch>(), (filtered, changes) =>
                {
                    filtered.Filter(changes, iwm => iwm.IsMatch);
                    return filtered;
                })
                .Select(list => list.CaptureChanges().Transform(iwm => iwm.Item))
                .NotEmpty();
        }

        private class ItemWithMatch
        {
            public T Item { get; }
            public bool IsMatch { get;  }

            public ItemWithMatch(T item, bool isMatch)
            {
                Item = item;
                IsMatch = isMatch;
            }
        }
    }
}
