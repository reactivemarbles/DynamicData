// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using DynamicData.Binding;

namespace DynamicData;

/// <summary>
/// ObservableCache extensions for SortAndBind.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Bind paged data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>Creates a <see cref="ReadOnlyObservableCollection{T}"/> and delegates to <see cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, PageContext{TObject}}}, IList{TObject})"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection)
        where TObject : notnull
        where TKey : notnull
    {
        var targetList = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(targetList);

        return source.Bind(targetList);
    }

    /// <summary>
    /// Bind paged data to the specified collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> with default settings.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>Creates a <see cref="ReadOnlyObservableCollection{T}"/> and delegates to <see cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, PageContext{TObject}}}, IList{TObject}, SortAndBindOptions)"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        var targetList = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(targetList);

        return source.Bind(targetList, options);
    }

    /// <summary>
    /// Bind paged data to the specified collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="targetList">The list to bind to.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>This is the primary Bind overload for paged data. It applies paged changeset mutations directly to the target list.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> source,
        IList<TObject> targetList)
        where TObject : notnull
        where TKey : notnull =>
        new BindPaged<TObject, TKey>(source, targetList, null).Run();

    /// <summary>
    /// Bind paged data to the specified collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="targetList">The list to bind to.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> with default settings.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>This overload accepts <see cref="SortAndBindOptions"/> to control reset threshold behavior.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> source,
        IList<TObject> targetList,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new BindPaged<TObject, TKey>(source, targetList, options).Run();

    /// <summary>
    /// Bind virtualized and sorted data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>Creates a <see cref="ReadOnlyObservableCollection{T}"/> and delegates to <see cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, VirtualContext{TObject}}}, IList{TObject})"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection)
        where TObject : notnull
        where TKey : notnull
    {
        var targetList = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(targetList);

        return source.Bind(targetList);
    }

    /// <summary>
    /// Bind virtualized data to the specified collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> with default settings.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>Creates a <see cref="ReadOnlyObservableCollection{T}"/> and delegates to <see cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, VirtualContext{TObject}}}, IList{TObject}, SortAndBindOptions)"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        var targetList = new ObservableCollectionExtended<TObject>();
        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(targetList);

        return source.Bind(targetList, options);
    }

    /// <summary>
    /// Bind virtualized data to the specified collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="targetList">The list to bind to.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>This is the primary Bind overload for virtualized data. It applies virtualized changeset mutations directly to the target list.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> source,
        IList<TObject> targetList)
        where TObject : notnull
        where TKey : notnull =>
        new BindVirtualized<TObject, TKey>(source, targetList, null).Run();

    /// <summary>
    /// Bind virtualized data to the specified collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="targetList">The list to bind to.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> with default settings.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>This overload accepts <see cref="SortAndBindOptions"/> to control reset threshold behavior.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> source,
        IList<TObject> targetList,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new BindVirtualized<TObject, TKey>(source, targetList, options).Run();

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <remarks>This overload uses <see cref="Comparer{T}.Default"/> for types implementing <see cref="IComparable{T}"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(targetList, DynamicDataOptions.SortAndBind);

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <remarks>This overload uses <see cref="Comparer{T}.Default"/> for types implementing <see cref="IComparable{T}"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        SortAndBindOptions options)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(targetList, Comparer<TObject>.Default, options);

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(targetList, comparer, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Sorts the source changeset using <paramref name="comparer"/> and applies incremental changes
    /// directly to <paramref name="targetList"/>, keeping it sorted in-place.
    /// Combines the behavior of Sort and Bind into a single optimized step.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset stream.</param>
    /// <param name="targetList">The list to bind to. Items are inserted, removed, and moved in-place to maintain sort order.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> controlling reset threshold and initial capacity.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>
    /// <para>
    /// This operator is the preferred replacement for the <c>.Sort().Bind()</c> chain.
    /// It applies sort logic and collection mutations in a single pass, avoiding intermediate allocations.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Item inserted at the correct sorted position in <paramref name="targetList"/>.</description></item>
    /// <item><term>Update</term><description>Old item removed and new item inserted at its sorted position.</description></item>
    /// <item><term>Remove</term><description>Item removed from <paramref name="targetList"/>.</description></item>
    /// <item><term>Refresh</term><description>Sort position is re-evaluated. If the position changed, the item is moved in-place.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// <item><term>OnCompleted</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Large batches may trigger a full list reset (clear + re-add) instead of incremental moves, controlled by <see cref="SortAndBindOptions.ResetThreshold"/>. This fires <c>CollectionChanged</c> with <c>Reset</c> action, which can be more efficient for UI virtualization but causes a visual flicker.</para>
    /// </remarks>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IObservable{IComparer{TObject}}, SortAndBindOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IComparer<TObject> comparer,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new SortAndBind<TObject, TKey>(source, comparer, options, targetList).Run();

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IObservable{IComparer{TObject}}, SortAndBindOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IObservable<IComparer<TObject>> comparerChanged)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(targetList, comparerChanged, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Sorts the source changeset and applies incremental changes directly to <paramref name="targetList"/>,
    /// re-sorting when the comparer observable emits a new comparer.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source changeset stream.</param>
    /// <param name="targetList">The list to bind to. Items are inserted, removed, and moved in-place to maintain sort order.</param>
    /// <param name="comparerChanged">An observable of comparers which enables the sort order to be changed.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> controlling reset threshold and initial capacity.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>
    /// <para>
    /// When <paramref name="comparerChanged"/> emits a new comparer, all items are re-sorted and the target list is updated.
    /// No data is emitted until the first comparer arrives.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Item inserted at the correct sorted position in <paramref name="targetList"/>.</description></item>
    /// <item><term>Update</term><description>Old item removed and new item inserted at its sorted position.</description></item>
    /// <item><term>Remove</term><description>Item removed from <paramref name="targetList"/>.</description></item>
    /// <item><term>Refresh</term><description>Sort position is re-evaluated. If the position changed, the item is moved in-place.</description></item>
    /// <item><term>Comparer changed</term><description>Full re-sort of all items. The target list is updated to reflect the new order.</description></item>
    /// <item><term>OnError</term><description>Forwarded to the downstream observer.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No data is emitted until the comparer observable produces its first value. Large batches or comparer changes may trigger a full list reset depending on <see cref="SortAndBindOptions.ResetThreshold"/>.</para>
    /// </remarks>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IObservable<IComparer<TObject>> comparerChanged,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new SortAndBind<TObject, TKey>(source, comparerChanged, options, targetList).Run();

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <remarks>This overload uses <see cref="Comparer{T}.Default"/> for types implementing <see cref="IComparable{T}"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, Comparer<TObject>.Default, DynamicDataOptions.SortAndBind);

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <remarks>This overload uses <see cref="Comparer{T}.Default"/> for types implementing <see cref="IComparable{T}"/>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        SortAndBindOptions options)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, Comparer<TObject>.Default, options);

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, comparer, DynamicDataOptions.SortAndBind);

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> controlling reset threshold and initial capacity.</param>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IComparer<TObject> comparer,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        // allow options to set initial capacity for efficiency
        var observableCollection = options.InitialCapacity > 0
            ? new ObservableCollectionExtended<TObject>(new List<TObject>(options.InitialCapacity))
            : new ObservableCollectionExtended<TObject>();

        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(observableCollection);

        return new SortAndBind<TObject, TKey>(source, comparer, options, observableCollection).Run();
    }

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, IObservable{IComparer{TObject}}, SortAndBindOptions)"/>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IObservable<IComparer<TObject>> comparerChanged)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, comparerChanged, DynamicDataOptions.SortAndBind);

    /// <inheritdoc cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IObservable{IComparer{TObject}}, SortAndBindOptions)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <param name="comparerChanged">An observable of comparers which enables the sort order to be changed.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> controlling reset threshold and initial capacity.</param>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IObservable<IComparer<TObject>> comparerChanged,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        // allow options to set initial capacity for efficiency
        var observableCollection = options.InitialCapacity > 0
            ? new ObservableCollectionExtended<TObject>(new List<TObject>(options.InitialCapacity))
            : new ObservableCollectionExtended<TObject>();

        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(observableCollection);

        return new SortAndBind<TObject, TKey>(source, comparerChanged, options, observableCollection).Run();
    }
}
