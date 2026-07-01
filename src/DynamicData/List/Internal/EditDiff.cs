// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the EditDiff class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
internal sealed class EditDiff<T>(ISourceList<T> source, IEqualityComparer<T>? equalityComparer)
    where T : notnull
{
    /// <summary>
    /// The _equalityComparer field.
    /// </summary>
    private readonly IEqualityComparer<T> _equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly ISourceList<T> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Edit operation.
    /// </summary>
    /// <param name="items">The items value.</param>
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
