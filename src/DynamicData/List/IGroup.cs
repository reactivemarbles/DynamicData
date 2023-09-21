// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData;

/// <summary>
/// A grouping of observable lists.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TGroup">The type of the group.</typeparam>
public interface IGroup<TObject, out TGroup>
    where TObject : notnull
{
    /// <summary>
    /// Gets the group key.
    /// </summary>
    TGroup GroupKey { get; }

    /// <summary>
    /// Gets the observable list.
    /// </summary>
    IObservableList<TObject> List { get; }
}
