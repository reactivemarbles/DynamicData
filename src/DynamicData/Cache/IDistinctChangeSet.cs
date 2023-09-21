// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable CheckNamespace
namespace DynamicData;

/// <summary>
/// A collection of distinct value updates.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public interface IDistinctChangeSet<T> : IChangeSet<T, T>
    where T : notnull
{
}
