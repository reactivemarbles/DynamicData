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
    /// Applies changeset mutations to a target <see cref="IObservableCollection{T}"/> for UI data binding.
    /// </summary>
    /// <typeparam name="T">The type of items in the list.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{T}}"/> to bind to a collection.</param>
    /// <param name="targetCollection">The <see cref="IObservableCollection{T}"/> target collection to keep in sync.</param>
    /// <param name="resetThreshold">When a changeset exceeds this many changes, the collection is reset instead of applying individual changes.</param>
    /// <returns>A continuation of the source changeset stream (allows further chaining).</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="targetCollection"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Delegates to <see cref="Adapt{T}(IObservable{IChangeSet{T}}, IChangeSetAdaptor{T})"/> with an internal collection adaptor.
    /// Each changeset is applied to the target collection on the calling thread. For UI binding, ensure the source is
    /// observed on the UI thread (e.g., via <c>ObserveOn</c>).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Item inserted at the specified index in the target collection.</description></item>
    /// <item><term>AddRange</term><description>Items inserted as a range. If the count exceeds <paramref name="resetThreshold"/>, the collection is cleared and repopulated.</description></item>
    /// <item><term>Replace</term><description>Item at the specified index is replaced.</description></item>
    /// <item><term>Remove</term><description>Item at the specified index is removed.</description></item>
    /// <item><term>RemoveRange/Clear</term><description>Items removed from the collection.</description></item>
    /// <item><term>Moved</term><description>Item is moved between positions in the collection.</description></item>
    /// <item><term>Refresh</term><description>Depends on the adaptor implementation.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, BindingOptions)"/>
    /// <seealso cref="Bind{T}(IObservable{IChangeSet{T}}, out ReadOnlyObservableCollection{T}, int)"/>
    /// <seealso cref="Clone{T}(IObservable{IChangeSet{T}}, IList{T})"/>
    /// <seealso cref="Adapt{T}(IObservable{IChangeSet{T}}, IChangeSetAdaptor{T})"/>
    /// <seealso cref="ObservableCacheEx.Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IObservableCollection{TObject}, int)"/>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, IObservableCollection<T> targetCollection, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        targetCollection.ThrowArgumentNullExceptionIfNull(nameof(targetCollection));

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;

        var options = resetThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = resetThreshold };

        return source.Bind(targetCollection, options);
    }

    /// <summary>
    /// Binds the source changeset stream to <paramref name="targetCollection"/>, with fine-grained <see cref="BindingOptions"/> control over reset threshold and other behaviors.
    /// </summary>
    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, IObservableCollection<T> targetCollection, BindingOptions options)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        targetCollection.ThrowArgumentNullExceptionIfNull(nameof(targetCollection));

        var adaptor = new ObservableCollectionAdaptor<T>(targetCollection, options);
        return source.Adapt(adaptor);
    }

    /// <summary>
    /// Constructs a <see cref="ReadOnlyObservableCollection{T}"/> and binds the changeset stream to it.
    /// Use this overload when you need a read-only view (typically for UI binding) without managing the backing collection yourself.
    /// The created collection is returned via the <paramref name="readOnlyObservableCollection"/> output parameter.
    /// </summary>
    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <remarks>
    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <para>The created collection is backed by an internal <c>ObservableCollectionExtended&lt;T&gt;</c>. Callers receive only the read-only wrapper.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, out ReadOnlyObservableCollection<T> readOnlyObservableCollection, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        // if user has not specified different defaults, use system wide defaults instead.
        // This is a hack to retro fit system wide defaults which override the hard coded defaults above
        var defaults = DynamicDataOptions.Binding;
        var options = resetThreshold == BindingOptions.DefaultResetThreshold
            ? defaults
            : defaults with { ResetThreshold = resetThreshold };

        return source.Bind(out readOnlyObservableCollection, options);
    }

    /// <summary>
    /// Constructs a <see cref="ReadOnlyObservableCollection{T}"/> and binds the changeset stream to it,
    /// with fine-grained <see cref="BindingOptions"/> control over reset threshold and other behaviors.
    /// The created collection is returned via the <paramref name="readOnlyObservableCollection"/> output parameter.
    /// </summary>
    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <remarks>
    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    /// <para>The created collection is backed by an internal <c>ObservableCollectionExtended&lt;T&gt;</c>. Callers receive only the read-only wrapper.</para>
    /// </remarks>
    public static IObservable<IChangeSet<T>> Bind<T>(this IObservable<IChangeSet<T>> source, out ReadOnlyObservableCollection<T> readOnlyObservableCollection, BindingOptions options)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        var target = new ObservableCollectionExtended<T>();
        var result = new ReadOnlyObservableCollection<T>(target);
        var adaptor = new ObservableCollectionAdaptor<T>(target, options);
        readOnlyObservableCollection = result;
        return source.Adapt(adaptor);
    }

#if SUPPORTS_BINDINGLIST
    /// <summary>
    /// Binds the source changeset stream to a WinForms <see cref="BindingList{T}"/>, keeping <paramref name="bindingList"/> in sync.
    /// </summary>
    /// <inheritdoc cref="Bind{T}(IObservable{IChangeSet{T}}, IObservableCollection{T}, int)"/>
    public static IObservable<IChangeSet<T>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(this IObservable<IChangeSet<T>> source, BindingList<T> bindingList, int resetThreshold = BindingOptions.DefaultResetThreshold)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        bindingList.ThrowArgumentNullExceptionIfNull(nameof(bindingList));

        return source.Adapt(new BindingListAdaptor<T>(bindingList, resetThreshold));
    }
#endif
}
