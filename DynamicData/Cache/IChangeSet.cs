using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData
{
    /// <summary>
    /// A collection of changes.
    /// 
    /// Changes are always published in the order.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IChangeSet<TObject,  TKey> : IChangeSet, IEnumerable<Change<TObject, TKey>>
    {

    }
}