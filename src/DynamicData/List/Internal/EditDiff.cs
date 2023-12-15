// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class EditDiff<T>(ISourceList<T> source, IEqualityComparer<T>? equalityComparer)
    where T : notnull
{
    private readonly IEqualityComparer<T> _equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;

    private readonly ISourceList<T> _source = source ?? throw new ArgumentNullException(nameof(source));

    public void Edit(IEnumerable<T> items) => _source.Edit(
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
