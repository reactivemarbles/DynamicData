// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal class EditDiff<T>
    where T : notnull
{
    private readonly IEqualityComparer<T> _equalityComparer;

    private readonly ISourceList<T> _source;

    public EditDiff(ISourceList<T> source, IEqualityComparer<T>? equalityComparer)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;
    }

    public void Edit(IEnumerable<T> items)
    {
        _source.Edit(
            innerList =>
            {
                var originalItems = innerList.AsArray();
                var newItems = items.AsArray();

                var removes = originalItems.Except(newItems, _equalityComparer);
                var adds = newItems.Except(originalItems, _equalityComparer);

                innerList.Remove(removes);
                innerList.AddRange(adds);
            });
    }
}
