// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Bind paged data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The output <c>ReadOnlyObservableCollection&lt;TObject&gt;</c> that will be populated with the sorted results.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>Creates a <c>ReadOnlyObservableCollection&lt;T&gt;</c> and delegates to <c>Bind&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey, PageContext&lt;TObject&gt;&gt;&gt;, IList&lt;TObject&gt;)</c>.</remarks>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The output <c>ReadOnlyObservableCollection&lt;TObject&gt;</c> that will be populated with the sorted results.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> with default settings.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>Creates a <c>ReadOnlyObservableCollection&lt;T&gt;</c> and delegates to <c>Bind&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey, PageContext&lt;TObject&gt;&gt;&gt;, IList&lt;TObject&gt;, SortAndBindOptions)</c>.</remarks>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="targetList">The <c>IList&lt;TObject&gt;</c> to bind sorted results to.</param>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="targetList">The <c>IList&lt;TObject&gt;</c> to bind sorted results to.</param>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The output <c>ReadOnlyObservableCollection&lt;TObject&gt;</c> that will be populated with the sorted results.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>Creates a <c>ReadOnlyObservableCollection&lt;T&gt;</c> and delegates to <c>Bind&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey, VirtualContext&lt;TObject&gt;&gt;&gt;, IList&lt;TObject&gt;)</c>.</remarks>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The output <c>ReadOnlyObservableCollection&lt;TObject&gt;</c> that will be populated with the sorted results.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> with default settings.</param>
    /// <returns>An observable which will emit change sets.</returns>
    /// <remarks>Creates a <c>ReadOnlyObservableCollection&lt;T&gt;</c> and delegates to <c>Bind&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey, VirtualContext&lt;TObject&gt;&gt;&gt;, IList&lt;TObject&gt;, SortAndBindOptions)</c>.</remarks>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="targetList">The <c>IList&lt;TObject&gt;</c> to bind sorted results to.</param>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="targetList">The <c>IList&lt;TObject&gt;</c> to bind sorted results to.</param>
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

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="targetList">The targetList value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload uses <c>Comparer&lt;T&gt;.Default</c> for types implementing <c>IComparable&lt;T&gt;</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(targetList, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="targetList">The targetList value.</param>
    /// <param name="options">The options value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload uses <c>Comparer&lt;T&gt;.Default</c> for types implementing <c>IComparable&lt;T&gt;</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        SortAndBindOptions options)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(targetList, Comparer<TObject>.Default, options);

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="targetList">The targetList value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="targetList">The <c>IList&lt;TObject&gt;</c> to bind sorted results to. Items are inserted, removed, and moved in-place to maintain sort order.</param>
    /// <param name="comparer">The <c>IComparer&lt;TObject&gt;</c> that determines sort order.</param>
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
    /// <seealso><c>SortAndBind&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IList&lt;TObject&gt;, IObservable&lt;IComparer&lt;TObject&gt;&gt;, SortAndBindOptions)</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IComparer<TObject> comparer,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new SortAndBind<TObject, TKey>(source, comparer, options, targetList).Run();
    }

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="targetList">The targetList value.</param>
    /// <param name="comparerChanged">The comparerChanged value.</param>
    /// <returns>The resulting observable sequence.</returns>
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="targetList">The <c>IList&lt;TObject&gt;</c> to bind sorted results to. Items are inserted, removed, and moved in-place to maintain sort order.</param>
    /// <param name="comparerChanged">An <c>IObservable&lt;IComparer&lt;TObject&gt;&gt;</c> that emits new comparers to re-sort with.</param>
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
    /// <seealso><c>SortAndBind&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, IList&lt;TObject&gt;, IComparer&lt;TObject&gt;, SortAndBindOptions)</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IObservable<IComparer<TObject>> comparerChanged,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new SortAndBind<TObject, TKey>(source, comparerChanged, options, targetList).Run();

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="readOnlyObservableCollection">The readOnlyObservableCollection value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload uses <c>Comparer&lt;T&gt;.Default</c> for types implementing <c>IComparable&lt;T&gt;</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, Comparer<TObject>.Default, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="readOnlyObservableCollection">The readOnlyObservableCollection value.</param>
    /// <param name="options">The options value.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload uses <c>Comparer&lt;T&gt;.Default</c> for types implementing <c>IComparable&lt;T&gt;</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        SortAndBindOptions options)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, Comparer<TObject>.Default, options);

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="readOnlyObservableCollection">The readOnlyObservableCollection value.</param>
    /// <param name="comparer">The comparer value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, comparer, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The output <c>ReadOnlyObservableCollection&lt;TObject&gt;</c> that will be populated with the sorted results.</param>
    /// <param name="comparer">The <c>IComparer&lt;TObject&gt;</c> that determines sort order.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> controlling reset threshold and initial capacity.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IComparer<TObject> comparer,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        // allow options to set initial capacity for efficiency
        var observableCollection = options.InitialCapacity > 0
            ? new ObservableCollectionExtended<TObject>(new List<TObject>(options.InitialCapacity))
            : new ObservableCollectionExtended<TObject>();

        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(observableCollection);

        return new SortAndBind<TObject, TKey>(source, comparer, options, observableCollection).Run();
    }

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="readOnlyObservableCollection">The readOnlyObservableCollection value.</param>
    /// <param name="comparerChanged">The comparerChanged value.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IObservable<IComparer<TObject>> comparerChanged)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, comparerChanged, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Provides an overload of <c>SortAndBind</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to sort and bind.</param>
    /// <param name="readOnlyObservableCollection">The output <c>ReadOnlyObservableCollection&lt;TObject&gt;</c> that will be populated with the sorted results.</param>
    /// <param name="comparerChanged">An <c>IObservable&lt;IComparer&lt;TObject&gt;&gt;</c> that emits new comparers to re-sort with.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> controlling reset threshold and initial capacity.</param>
    /// <returns>The resulting observable sequence.</returns>
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
