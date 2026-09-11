// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
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
    /// Applies each change from the source changeset to the specified <paramref name="target"/> collection as a side effect.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to clone.</param>
    /// <param name="target">The <see cref="ICollection{TObject}"/> target collection to which changes are applied.</param>
    /// <returns>An observable that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>The item is added to <paramref name="target"/>. Forwarded as <b>Add</b>.</description></item>
    /// <item><term>Update</term><description>The previous item is removed from <paramref name="target"/> and the current item is added. Forwarded as <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>The item is removed from <paramref name="target"/>. Forwarded as <b>Remove</b>.</description></item>
    /// <item><term>Refresh</term><description>Ignored (<see cref="ICollection{T}"/> has no concept of refresh). Forwarded as <b>Refresh</b>.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Clone<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, ICollection<TObject> target)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        target.ThrowArgumentNullExceptionIfNull(nameof(target));

        return source.Do(
            changes =>
            {
                foreach (var item in changes.ToConcreteType())
                {
                    switch (item.Reason)
                    {
                        case ChangeReason.Add:
                            {
                                target.Add(item.Current);
                            }

                            break;

                        case ChangeReason.Update:
                            {
                                target.Remove(item.Previous.Value);
                                target.Add(item.Current);
                            }

                            break;

                        case ChangeReason.Remove:
                            target.Remove(item.Current);
                            break;
                    }
                }
            });
    }
}
