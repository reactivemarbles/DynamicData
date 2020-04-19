// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// API for updating a source cache.
    /// 
    /// Use edit to produce singular changeset.
    /// 
    /// NB: The evaluate method is used to signal to any observing operators
    /// to reevaluate whether the the object still matches downstream operators.
    /// This is primarily targeted to inline object changes such as datetime and calculated fields.
    /// 
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public interface ISourceUpdater<TObject, TKey> : ICacheUpdater<TObject, TKey>
    {
        /// <summary>
        /// Clears existing values and loads the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        void Load(IEnumerable<TObject> items);

        /// <summary>
        /// Adds or changes the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        void AddOrUpdate(IEnumerable<TObject> items);

        /// <summary>
        /// Adds or updates the item.
        /// </summary>
        /// <param name="item">The item.</param>
        void AddOrUpdate(TObject item);

        /// <summary>
        /// Adds or updates the item using a comparer.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="comparer">The comparer.</param>
        void AddOrUpdate(TObject item, IEqualityComparer<TObject> comparer);

        /// <summary>
        /// Refreshes the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        void Refresh(IEnumerable<TObject> items);

        /// <summary>
        /// Refreshes the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        void Refresh(TObject item);

        /// <summary>
        /// Refreshes the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        [Obsolete(Constants.EvaluateIsDead)]
        void Evaluate(IEnumerable<TObject> items);

        /// <summary>
        /// Refreshes the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        [Obsolete(Constants.EvaluateIsDead)]
        void Evaluate(TObject item);

        /// <summary>
        /// Removes the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        void Remove(IEnumerable<TObject> items);

        /// <summary>
        /// Removes the specified item.
        /// </summary>
        /// <param name="item">The item.</param>
        void Remove(TObject item);
    }
}
