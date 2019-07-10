// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace DynamicData.Cache.Internal
{
    internal interface IFilter<TObject, TKey>
    {
        Func<TObject, bool> Filter { get; }
        IChangeSet<TObject, TKey> Refresh(IEnumerable<KeyValuePair<TKey, TObject>> items);
        IChangeSet<TObject, TKey> Update(IChangeSet<TObject, TKey> updates);
    }
}
