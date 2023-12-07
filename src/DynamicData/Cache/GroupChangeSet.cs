// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

internal sealed class GroupChangeSet<TObject, TKey, TGroupKey> : ChangeSet<IGroup<TObject, TKey, TGroupKey>, TGroupKey>, IGroupChangeSet<TObject, TKey, TGroupKey>
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    public static new readonly IGroupChangeSet<TObject, TKey, TGroupKey> Empty = new GroupChangeSet<TObject, TKey, TGroupKey>();

    public GroupChangeSet(IEnumerable<Change<IGroup<TObject, TKey, TGroupKey>, TGroupKey>> items)
        : base(items)
    {
    }

    private GroupChangeSet()
    {
    }
}
