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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, PageContext{TObject}}}, IList{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, PageContext{TObject}}}, IList{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, PageContext{TObject}}}, IList{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, PageContext{TObject}}}, out ReadOnlyObservableCollection{TObject})"/>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, PageContext{TObject}}}, out ReadOnlyObservableCollection{TObject}, SortAndBindOptions)"/>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, PageContext{TObject}}}, IList{TObject})"/>
    /// <returns>An observable which will emit change sets.</returns>
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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, VirtualContext{TObject}}}, IList{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, VirtualContext{TObject}}}, IList{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, VirtualContext{TObject}}}, IList{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
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
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, VirtualContext{TObject}}}, out ReadOnlyObservableCollection{TObject})"/>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, VirtualContext{TObject}}}, out ReadOnlyObservableCollection{TObject}, SortAndBindOptions)"/>
    /// <seealso cref="Bind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey, VirtualContext{TObject}}}, IList{TObject})"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> source,
        IList<TObject> targetList,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new BindVirtualized<TObject, TKey>(source, targetList, options).Run();

    /// <summary>
    /// Bind sorted data to the specified collection, for an object which implements IComparable<typeparamref name="TObject"></typeparamref>>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(targetList, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Bind sorted data to the specified collection, for an object which implements IComparable<typeparamref name="TObject"></typeparamref>>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        SortAndBindOptions options)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(targetList, Comparer<TObject>.Default, options);

    /// <summary>
    /// Bind sorted data to the specified collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> comparer to order the resulting dataset.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(targetList, comparer, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Bind sorted data to the specified collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> comparer to order the resulting dataset.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject})"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, SortAndBindOptions)"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject})"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IObservable{IComparer{TObject}})"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IObservable{IComparer{TObject}}, SortAndBindOptions)"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject})"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, SortAndBindOptions)"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, IComparer{TObject})"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, IObservable{IComparer{TObject}})"/>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, out ReadOnlyObservableCollection{TObject}, IObservable{IComparer{TObject}}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IComparer<TObject> comparer,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new SortAndBind<TObject, TKey>(source, comparer, options, targetList).Run();

    /// <summary>
    /// Bind sorted data to the specified collection, using an observable of comparers to switch sort order.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <param name="comparerChanged">An <see cref="IObservable{T}"/> of <see cref="IComparer{T}"/> which enables the sort order to be changed.</param>>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IObservable<IComparer<TObject>> comparerChanged)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(targetList, comparerChanged, DynamicDataOptions.SortAndBind);

    /// <summary>
    /// Bind sorted data to the specified collection, using an observable of comparers to switch sort order.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="targetList">The <see cref="IList{T}"/> list to bind to.</param>
    /// <param name="comparerChanged">An <see cref="IObservable{T}"/> of <see cref="IComparer{T}"/> which enables the sort order to be changed.</param>>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IObservable<IComparer<TObject>> comparerChanged,
        SortAndBindOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new SortAndBind<TObject, TKey>(source, comparerChanged, options, targetList).Run();

    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection for an object which implements IComparable<typeparamref name="TObject"></typeparamref>>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, Comparer<TObject>.Default, DynamicDataOptions.SortAndBind);

    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection for an object which implements IComparable<typeparamref name="TObject"></typeparamref>>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        SortAndBindOptions options)
        where TObject : notnull, IComparable<TObject>
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, Comparer<TObject>.Default, options);

    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> comparer to order the resulting dataset.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, comparer, DynamicDataOptions.SortAndBind);

    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <param name="comparer">An <see cref="IComparer{T}"/> comparer to order the resulting dataset.</param>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
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

    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection, using an observable of comparers to switch sort order.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <param name="comparerChanged">An <see cref="IObservable{T}"/> of <see cref="IComparer{T}"/> which enables the sort order to be changed.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> SortAndBind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IObservable<IComparer<TObject>> comparerChanged)
        where TObject : notnull
        where TKey : notnull =>
        source.SortAndBind(out readOnlyObservableCollection, comparerChanged, DynamicDataOptions.SortAndBind);

    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection, using an observable of comparers to switch sort order.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{T}"/> of <see cref="IChangeSet{TObject, TKey}"/>.</param>
    /// <param name="readOnlyObservableCollection">The <see cref="ReadOnlyObservableCollection{T}"/> resulting read only observable collection.</param>
    /// <param name="comparerChanged">An <see cref="IObservable{T}"/> of <see cref="IComparer{T}"/> which enables the sort order to be changed.</param>>
    /// <param name="options">The <see cref="SortAndBindOptions"/> Bind and sort default options.</param>
    /// <seealso cref="SortAndBind{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, IList{TObject}, IComparer{TObject}, SortAndBindOptions)"/>
    /// <returns>An observable which will emit change sets.</returns>
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
