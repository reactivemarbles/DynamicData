// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Lifted from here https://github.com/benaadams/Ben.Enumerable. Many thanks to the genius of the man.
namespace DynamicData.Kernel;

/// <summary>
/// A enumerable that also contains the enumerable list.
/// </summary>
/// <typeparam name="T">The type of items.</typeparam>
internal interface IEnumerableIList<T> : IEnumerable<T>
{
    /// <summary>
    /// Gets the enumerator.
    /// </summary>
    /// <returns>The enumerator.</returns>
    new EnumeratorIList<T> GetEnumerator();
}
