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
    /// Validates that each changeset contains no duplicate keys.
    /// If duplicates are detected, an <see cref="InvalidOperationException"/> is emitted via <c>OnError</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to validate for unique keys.</param>
    /// <returns>A changeset stream guaranteed to contain unique keys per changeset.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Forwarded as <b>Add</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Update</term><description>Forwarded as <b>Update</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Remove</term><description>Forwarded as <b>Remove</b> if the key is unique within the changeset.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b> if the key is unique within the changeset.</description></item>
    /// <item><term>OnError</term><description>Also emitted with <see cref="InvalidOperationException"/> if duplicate keys are detected in a changeset.</description></item>
    /// </list>
    /// </remarks>
    public static IObservable<IChangeSet<TObject, TKey>> EnsureUniqueKeys<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new UniquenessEnforcer<TObject, TKey>(source).Run();
    }
}
