using System.Collections.Generic;

namespace DynamicData.Aggregation
{
    /// <summary>
    /// A changeset which has been shaped for rapid online aggregations
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IAggregateChangeSet<T> : IEnumerable<AggregateItem<T>>
    {
    }
}
