using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
    internal sealed class ImmutableGroupChangeSet<TObject, TKey, TGroupKey> : ChangeSet<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>, IImmutableGroupChangeSet<TObject, TKey, TGroupKey>
    {

        public new static readonly IImmutableGroupChangeSet<TObject, TKey, TGroupKey> Empty = new ImmutableGroupChangeSet<TObject, TKey, TGroupKey>();

        private ImmutableGroupChangeSet()
        {
        }

        public ImmutableGroupChangeSet(IEnumerable<Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>> items)
            : base(items)
        {
        }
    }
}