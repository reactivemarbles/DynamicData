using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal class DistinctChangeSet<T> : ChangeSet<T, T>, IDistinctChangeSet<T>
    {
        public DistinctChangeSet(IEnumerable<Change<T, T>> items)
            : base(items)
        {
        }
    }
}
