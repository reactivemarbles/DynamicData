// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal sealed class ImmutableGroupChangeSet<TObject, TKey, TGroupKey> : ChangeSet<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>, IImmutableGroupChangeSet<TObject, TKey, TGroupKey>
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    public static new readonly IImmutableGroupChangeSet<TObject, TKey, TGroupKey> Empty = new ImmutableGroupChangeSet<TObject, TKey, TGroupKey>();

    public ImmutableGroupChangeSet(IEnumerable<Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>> items)
        : base(items)
    {
    }

    private ImmutableGroupChangeSet()
    {
    }
}
