// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Suppresses all <see cref="ListChangeReason.Refresh"/> changes from the stream. All other change reasons pass through.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to strip refresh events.</param>
    /// <returns>A list changeset stream with Refresh changes removed.</returns>
    /// <seealso><c>WhereReasonsAreNot&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, ListChangeReason[])</c></seealso>
    /// <seealso><c>AutoRefresh&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    public static IObservable<IChangeSet<T>> SuppressRefresh<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull => source.WhereReasonsAreNot(ListChangeReason.Refresh);
}
