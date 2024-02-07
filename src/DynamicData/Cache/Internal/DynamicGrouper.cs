// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicData.Cache.Internal;

internal sealed class DynamicGrouper<TObject, TKey, TGroupKey>
{
    private readonly ChangeAwareCache<ManagedGroup<TObject, TKey, TGroupKey>, TGroupKey> _groups = new();
    private readonly Dictionary<TKey, TGroupKey> _groupKeys = new();


}
