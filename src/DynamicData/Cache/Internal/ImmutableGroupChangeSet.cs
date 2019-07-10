// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
    internal sealed class ImmutableGroupChangeSet<TObject, TKey, TGroupKey> : ChangeSet<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>, IImmutableGroupChangeSet<TObject, TKey, TGroupKey>
    {

        public new static readonly IImmutableGroupChangeSet<TObject, TKey, TGroupKey> Empty = new ImmutableGroupChangeSet<TObject, TKey, TGroupKey>();

        private ImmutableGroupChangeSet()
        {
        }

        public ImmutableGroupChangeSet(IEnumerable<Change<IGrouping<TObject, TKey, TGroupKey>, TGroupKey>> items)
            : base(items)
        {
        }
    }
}