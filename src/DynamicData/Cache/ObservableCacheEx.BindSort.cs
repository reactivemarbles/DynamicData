using System.Collections.ObjectModel;
using DynamicData.Binding;

namespace DynamicData;

public static partial class ObservableCacheEx
{

    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="targetList">The list to bind to</param>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> BindAndSort<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IComparer<TObject> comparer)
        where TObject : notnull
        where TKey : notnull =>
        new BindAndSort<TObject, TKey>(source, comparer, BindAndSortOptions.Default, targetList).Run();

    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="options">Bind and sort default options.</param>
    /// <param name="targetList">The list to bind to</param>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> BindAndSort<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        IList<TObject> targetList,
        IComparer<TObject> comparer,
        BindAndSortOptions options)
        where TObject : notnull
        where TKey : notnull =>
        new BindAndSort<TObject, TKey>(source, comparer, options, targetList).Run();


    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> BindAndSort<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IComparer<TObject> comparer
    )
        where TObject : notnull
        where TKey : notnull =>
        source.BindAndSort(out readOnlyObservableCollection, comparer, BindAndSortOptions.Default);


    /// <summary>
    ///  Bind sorted data to the specified readonly observable collection.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="comparer">The comparer to order the resulting dataset.</param>
    /// <param name="options">Bind and sort default options.</param>
    /// <param name="readOnlyObservableCollection">The resulting read only observable collection.</param>
    /// <returns>An observable which will emit change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> BindAndSort<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> readOnlyObservableCollection,
        IComparer<TObject> comparer,
        BindAndSortOptions options
    )
        where TObject : notnull
        where TKey : notnull
    {
        // allow options to set initial capacity for efficiency
        var observableCollection = options.InitialCapacity > 0
            ? new ObservableCollectionExtended<TObject>(new List<TObject>(options.InitialCapacity))
            : new ObservableCollectionExtended<TObject>();

        readOnlyObservableCollection = new ReadOnlyObservableCollection<TObject>(observableCollection);

        return new BindAndSort<TObject, TKey>(source, comparer, options, observableCollection).Run();
    }


}
