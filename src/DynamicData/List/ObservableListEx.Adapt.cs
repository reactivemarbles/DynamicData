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
    /// Injects a side effect into a changeset stream via an <see cref="IChangeSetAdaptor{T}"/>.
    /// The adaptor's <c>Adapt</c> method is invoked for each changeset before it is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to observe and adapt.</param>
    /// <param name="adaptor">The <see cref="IChangeSetAdaptor{T}"/> adaptor whose <c>Adapt</c> method is invoked for each changeset.</param>
    /// <returns>A list changeset stream identical to the source, with the adaptor side effect applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="adaptor"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This is the primary extension point for custom UI binding adaptors (e.g., <see cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    /// delegates to this operator). If the adaptor throws, the exception propagates downstream as <c>OnError</c>.
    /// </para>
    /// </remarks>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    public static IObservable<IChangeSet<T>> Adapt<T>(this IObservable<IChangeSet<T>> source, IChangeSetAdaptor<T> adaptor)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        adaptor.ThrowArgumentNullExceptionIfNull(nameof(adaptor));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var locker = InternalEx.NewLock();
                return source.Synchronize(locker).Select(
                    changes =>
                    {
                        adaptor.Adapt(changes);
                        return changes;
                    }).SubscribeSafe(observer);
            });
    }
}
