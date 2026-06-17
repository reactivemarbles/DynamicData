// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using DynamicData.Binding;
using DynamicData.Cache.Internal;
using DynamicData.List.Internal;
using DynamicData.List.Linq;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Filters the changeset stream to include only changes with the specified <see cref="ListChangeReason"/> values.
    /// Index information is stripped from the output because removing some changes invalidates the original index positions.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to filter by change reason.</param>
    /// <param name="reasons">The <see cref="ListChangeReason"/> change reasons to include. Must specify at least one.</param>
    /// <returns>A list changeset stream containing only changes with the specified reasons.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reasons"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="reasons"/> is empty.</exception>
    /// <remarks>
    /// <para>Filters individual changes within each changeset. If filtering removes all changes from a changeset, the empty changeset is suppressed via <see cref="NotEmpty{T}(IObservable{IChangeSet{T}})"/>.</para>
    /// <para><b>Worth noting:</b> Filtering out <b>Remove</b> changes can cause downstream operators to accumulate items indefinitely (memory leak). Index information is stripped because removing some changes invalidates the original index positions.</para>
    /// </remarks>
    /// <seealso cref="WhereReasonsAreNot{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> WhereReasonsAre<T>(this IObservable<IChangeSet<T>> source, params ListChangeReason[] reasons)
        where T : notnull
    {
        reasons.ThrowArgumentNullExceptionIfNull(nameof(reasons));

        if (reasons.Length == 0)
        {
            throw new ArgumentException("Must enter at least 1 reason", nameof(reasons));
        }

        var matches = new HashSet<ListChangeReason>(reasons);
        return source.Select(
            changes =>
            {
                var filtered = changes.Where(change => matches.Contains(change.Reason)).YieldWithoutIndex();
                return new ChangeSet<T>(filtered);
            }).NotEmpty();
    }
}
