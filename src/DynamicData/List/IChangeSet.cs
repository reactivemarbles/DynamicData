// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// <para>A collection of changes.</para>
/// <para>Changes are always published in the order.</para>
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
public interface IChangeSet<TObject> : IEnumerable<Change<TObject>>, IChangeSet
    where TObject : notnull
{
    /// <summary>
    ///     Gets the number of updates.
    /// </summary>
    int Replaced { get; }

    /// <summary>
    /// Gets the total count of items changed.
    /// </summary>
    int TotalChanges { get; }
}
