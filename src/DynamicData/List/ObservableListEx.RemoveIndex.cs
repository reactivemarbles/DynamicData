// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
    /// Strips index information from all changes in the stream.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to strip index information.</param>
    /// <returns>A list changeset stream with all index values removed from changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Removes index positions from every change in each changeset. This is useful when downstream operators do not require or support index-based operations.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add / AddRange / Remove / RemoveRange / Replace / Clear</term><description>Emitted with the same reason and an unspecified index of -1.</description></item>
    /// <item><term>Refresh</term><description>Emitted as a <b>Replace</b> of the item with itself, with unspecified indexes, because a <b>Refresh</b> must carry an index.</description></item>
    /// <item><term>Moved</term><description>Dropped, since a move has no meaning without its indexes.</description></item>
    /// <item><term>OnError / OnCompleted</term><description>Forwarded.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> because a refresh arrives downstream as a <b>Replace</b>, operators that treat refreshes specially (such as <see cref="SuppressRefresh{T}(IObservable{IChangeSet{T}})"/>, or <c>Transform</c> without <c>transformOnRefresh</c>) handle it as a replacement instead.</para>
    /// </remarks>
    /// <seealso cref="ChangeSetEx.YieldWithoutIndex{T}(IEnumerable{Change{T}})"/>
    public static IObservable<IChangeSet<T>> RemoveIndex<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Select(changes => new ChangeSet<T>(changes.YieldWithoutIndex()));
    }
}
