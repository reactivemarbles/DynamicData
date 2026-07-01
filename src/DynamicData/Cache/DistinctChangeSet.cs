// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Provides members for the DistinctChangeSet class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal sealed class DistinctChangeSet<T> : ChangeSet<T, T>, IDistinctChangeSet<T>
    where T : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctChangeSet{T}"/> class.
    /// </summary>
    /// <param name="items">The items value.</param>
    public DistinctChangeSet(IEnumerable<Change<T, T>> items)
        : base(items)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctChangeSet{T}"/> class.
    /// </summary>
    public DistinctChangeSet()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctChangeSet{T}"/> class.
    /// </summary>
    /// <param name="capacity">The capacity value.</param>
    public DistinctChangeSet(int capacity)
        : base(capacity)
    {
    }
}
