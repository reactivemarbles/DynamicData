using System;
using System.Collections.Generic;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// A collection of changes.
    /// 
    /// Changes are always published in the order.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface IChangeSet<TObject, TKey> : IChangeSet, IEnumerable<Change<TObject, TKey>>
    {
        /// <summary>
        /// The number of evaluates
        /// </summary>
        [Obsolete(Constants.EvaluateIsDead)]
        int Evaluates { get; }
        
        /// <summary>
        /// The number of updates
        /// </summary>
        int Updates { get; }
    }
}
