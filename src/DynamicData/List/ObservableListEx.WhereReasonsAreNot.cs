// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Filters the changeset stream to exclude changes with the specified <see cref="ListChangeReason"/> values.
    /// Index information is stripped from the output because removing some changes invalidates the original index positions.
    /// The exception is when only <see cref="ListChangeReason.Refresh"/> is excluded, since removing Refresh does not affect index calculations.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to filter by excluding change reasons.</param>
    /// <param name="reasons">The <see cref="ListChangeReason"/> change reasons to exclude. Must specify at least one.</param>
    /// <returns>A list changeset stream with the specified change reasons removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reasons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reasons"/> is empty.</exception>
    /// <remarks>
    /// <para>
    /// Empty changesets (after filtering) are automatically suppressed. When only <see cref="ListChangeReason.Refresh"/> is excluded,
    /// indices are preserved, since removing Refresh does not affect index calculations.
    /// </para>
    /// </remarks>
    /// <seealso cref="WhereReasonsAre{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> WhereReasonsAreNot<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
        where T : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));
        }

        if (reasons.Length == 1 && reasons[0] == ListChangeReason.Refresh)
        {
            // If only refresh changes are removed, then there's no need to remove the indexes
            return source.Select(changes =>
            {
                var filtered = changes.Where(c => c.Reason != ListChangeReason.Refresh);
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(
            updates =>
            {
                var filtered = updates.Where(u => !matches.Contains(u.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
    }
}
