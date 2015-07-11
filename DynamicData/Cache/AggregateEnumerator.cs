using System.Collections;
using System.Collections.Generic;

namespace DynamicData
{
    internal class AggregateEnumerator<TObject, TKey> : IEnumerable<AggregateItem<TObject, TKey>>
    {
        private readonly IChangeSet<TObject, TKey> _source;

        public AggregateEnumerator(IChangeSet<TObject, TKey> source)
        {
            _source = source;
        }

        public IEnumerator<AggregateItem<TObject, TKey>> GetEnumerator()
        {
            foreach (var change in _source)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        yield return new AggregateItem<TObject, TKey>(AggregateType.Add, change.Current, change.Key);
                        break;
                    case ChangeReason.Update:
                        yield return new AggregateItem<TObject, TKey>(AggregateType.Remove, change.Previous.Value, change.Key);
                        yield return new AggregateItem<TObject, TKey>(AggregateType.Add, change.Current, change.Key);
                        break;
                    case ChangeReason.Remove:
                        yield return new AggregateItem<TObject, TKey>(AggregateType.Remove, change.Current, change.Key);
                        break;
                    default:
                        continue;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}