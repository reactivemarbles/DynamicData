using System;
using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
    internal interface IFilter<TObject, TKey>
    {
        Func<TObject, bool> Filter { get; }
        IChangeSet<TObject, TKey> Evaluate(IEnumerable<KeyValuePair<TKey, TObject>> items);
        IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates);
    }
}
