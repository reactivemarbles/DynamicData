// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
#if REACTIVE_SHIM
using DynamicData.Reactive.Binding;
#else
using DynamicData.Binding;
#endif

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
    /// Watches a specific property on all items and emits just the property value (without the sender) when it changes.
    /// Requires <typeparamref name="TObject"/> to implement <see cref="INotifyPropertyChanged"/>.
    /// This is NOT a changeset operator: it returns a flat <c>IObservable&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of item. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TValue">The type of the property value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to observe a specific property value on items in.</param>
    /// <param name="propertyAccessor">An <c>Expression&lt;TDelegate&gt;</c> expression selecting the property to observe.</param>
    /// <param name="notifyOnInitialValue">When <see langword="true"/> (default), the current value is emitted immediately upon subscribing to each item.</param>
    /// <returns>An observable emitting the property value whenever it changes on any tracked item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertyAccessor"/> is <see langword="null"/>.</exception>
    /// <seealso><c>WhenPropertyChanged&lt;TObject, TValue&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TValue&gt;&gt;, bool)</c></seealso>
    /// <seealso><c>WhenAnyPropertyChanged&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, string[])</c></seealso>
    /// <seealso><c>ObservableCacheEx.WhenValueChanged&lt;TObject, TKey, TValue&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Expression&lt;Func&lt;TObject, TValue&gt;&gt;, bool)</c></seealso>
    public static IObservable<TValue?> WhenValueChanged<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TValue>> propertyAccessor, bool notifyOnInitialValue = true)
        where TObject : INotifyPropertyChanged
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(propertyAccessor);

        var factory = propertyAccessor.GetFactory();
        return source.MergeMany(t => factory(t, notifyOnInitialValue).Select(pv => pv.Value));
    }
}
