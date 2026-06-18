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
    /// Suppresses all <see cref="ListChangeReason.Refresh"/> changes from the stream. All other change reasons pass through.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to strip refresh events.</param>
    /// <returns>A list changeset stream with Refresh changes removed.</returns>
    /// <seealso cref="WhereReasonsAreNot{T}(IObservable{IChangeSet{T}}, ListChangeReason[])"/>
    /// <seealso cref="AutoRefresh{TObject}(IObservable{IChangeSet{TObject}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> SuppressRefresh<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => source.WhereReasonsAreNot(ListChangeReason.Refresh);
}
