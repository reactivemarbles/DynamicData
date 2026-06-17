// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Calls <c>Evaluate()</c> on items that implement <see cref="IEvaluateAware"/> when a <b>Refresh</b> change arrives.
    /// Other change reasons are forwarded without invoking Evaluate.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to trigger re-evaluation on.</param>
    /// <returns>An observable that emits the same changesets as <paramref name="source"/>, unchanged.</returns>
    /// <remarks>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term><b>Add</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Update</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Remove</b></term><description>Forwarded unchanged.</description></item>
    ///   <item><term><b>Refresh</b></term><description>Calls <c>Evaluate()</c> on the item, then forwards the change.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> InvokeEvaluate<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : IEvaluateAware
        where TKey : notnull => source.Do(changes => changes.Where(u => u.Reason == ChangeReason.Refresh).ForEach(u => u.Current.Evaluate()));
}
