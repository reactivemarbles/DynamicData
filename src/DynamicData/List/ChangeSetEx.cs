// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Kernel;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Change set extensions.
/// </summary>
public static class ChangeSetEx
{
    /// <summary>
    /// Returns a flattened source with the index.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An enumerable of change sets.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IEnumerable<ItemChange<T>> Flatten<T>(this IChangeSet<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new ItemChangeEnumerator<T>(source);
    }

    /// <summary>
    /// Gets the type of the change i.e. whether it is an item or a range change.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The change type.</returns>
    public static ChangeType GetChangeType(this ListChangeReason source) => source switch
    {
        ListChangeReason.Add or ListChangeReason.Refresh or ListChangeReason.Replace or ListChangeReason.Moved or ListChangeReason.Remove => ChangeType.Item,
        ListChangeReason.AddRange or ListChangeReason.RemoveRange or ListChangeReason.Clear => ChangeType.Range,
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };

    /// <summary>
    /// Transforms the change set into a different type using the specified transform function.
    /// </summary>
    /// <typeparam name="TSource">The type of the source.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="transformer">The transformer.</param>
    /// <returns>The change set.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// transformer.
    /// </exception>
    public static IChangeSet<TDestination> Transform<TSource, TDestination>(this IChangeSet<TSource> source, Func<TSource, TDestination> transformer)
        where TSource : notnull
        where TDestination : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        transformer.ThrowArgumentNullExceptionIfNull(nameof(transformer));

        var changes = source.Select(
            change =>
            {
                if (change.Type == ChangeType.Item)
                {
                    return new Change<TDestination>(change.Reason, transformer(change.Item.Current), change.Item.Previous.Convert(transformer), change.Item.CurrentIndex, change.Item.PreviousIndex);
                }

                return new Change<TDestination>(change.Reason, change.Range.Select(transformer), change.Range.Index);
            });

        return new ChangeSet<TDestination>(changes);
    }

    /// <summary>
    /// Remove the index from the changes.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An enumerable of changes.</returns>
    public static IEnumerable<Change<T>> YieldWithoutIndex<T>(this IEnumerable<Change<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new WithoutIndexEnumerator<T>(source);
    }

    /// <summary>
    /// Returns a flattened source.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An enumerable of changes.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    internal static IEnumerable<UnifiedChange<T>> Unified<T>(this IChangeSet<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new UnifiedChangeEnumerator<T>(source);
    }
}
