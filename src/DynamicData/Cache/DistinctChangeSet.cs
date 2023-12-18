// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

internal sealed class DistinctChangeSet<T> : ChangeSet<T, T>, IDistinctChangeSet<T>
    where T : notnull
{
    public DistinctChangeSet(IEnumerable<Change<T, T>> items)
        : base(items)
    {
    }

    public DistinctChangeSet()
    {
    }

    public DistinctChangeSet(int capacity)
        : base(capacity)
    {
    }
}
