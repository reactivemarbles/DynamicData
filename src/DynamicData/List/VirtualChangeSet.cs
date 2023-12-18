// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

// ReSharper disable once CheckNamespace
namespace DynamicData;

internal sealed class VirtualChangeSet<T>(IChangeSet<T> virtualChangeSet, IVirtualResponse response) : IVirtualChangeSet<T>
    where T : notnull
{
    private readonly IChangeSet<T> _virtualChangeSet = virtualChangeSet ?? throw new ArgumentNullException(nameof(virtualChangeSet));

    public int Refreshes => _virtualChangeSet.Refreshes;

    public IVirtualResponse Response { get; } = response ?? throw new ArgumentNullException(nameof(response));

    int IChangeSet.Adds => _virtualChangeSet.Adds;

    int IChangeSet.Capacity
    {
        get => _virtualChangeSet.Capacity;
        set => _virtualChangeSet.Capacity = value;
    }

    int IChangeSet.Count => _virtualChangeSet.Count;

    int IChangeSet.Moves => _virtualChangeSet.Moves;

    int IChangeSet.Removes => _virtualChangeSet.Removes;

    int IChangeSet<T>.Replaced => _virtualChangeSet.Replaced;

    int IChangeSet<T>.TotalChanges => _virtualChangeSet.TotalChanges;

    public IEnumerator<Change<T>> GetEnumerator() => _virtualChangeSet.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
