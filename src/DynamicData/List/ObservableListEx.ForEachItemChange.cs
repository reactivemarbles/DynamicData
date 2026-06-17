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
    /// Invokes <paramref name="action"/> for every individual <see cref="ItemChange{TObject}"/> in each changeset.
    /// Range changes are flattened into individual item changes first, so the callback only receives Add, Replace, Remove, and Refresh.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to observe each item-level change in.</param>
    /// <param name="action">The <see cref="Action{ItemChange{TObject}}"/> action invoked for each individual item change.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ForEachChange{TObject}(IObservable{IChangeSet{TObject}}, Action{Change{TObject}})"/>, this operator flattens
    /// <b>AddRange</b>, <b>RemoveRange</b>, and <b>Clear</b> into individual <see cref="ItemChange{TObject}"/> entries before invoking the callback.
    /// </para>
    /// </remarks>
    /// <seealso cref="ForEachChange{TObject}(IObservable{IChangeSet{TObject}}, Action{Change{TObject}})"/>
    public static IObservable<IChangeSet<TObject>> ForEachItemChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<ItemChange<TObject>> action)
        where TObject : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        action.ThrowArgumentNullExceptionIfNull(nameof(action));

        return source.Do(changes => changes.Flatten().ForEach(action));
    }
}
