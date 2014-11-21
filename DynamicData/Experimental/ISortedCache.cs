using DynamicData.Kernel;

namespace DynamicData.Experimental
{
    internal interface ISortedCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        IKeyValueCollection<TObject, TKey> SortedItems { get; }
    }


    ///// <summary>
    ///// A cache with a sorted list
    ///// </summary>
    ///// <typeparam name="TObject">The type of the object.</typeparam>
    ///// <typeparam name="TKey">The type of the key.</typeparam>
    //public class SortedObservableCache<TObject, TKey>
    //{
    //    private readonly SortController<TObject> _sortController;
    //    private IObservableCache<TObject, TKey> _innerCache;
        
    //    private IComparer<KeyValuePair<TKey,TObject>> _comparer;
    //    private readonly List<KeyValuePair<TKey,TObject>> _list = new List<KeyValuePair<TKey,TObject>>();


    //    public SortedObservableCache(IObservable<IChangeSet<TObject, TKey>> source, 
    //                SortController<TObject> sortController)
    //    {
    //        _sortController = sortController;
    //        _innerCache = source.AsObservableCache();
           
    //     //   _comparer = new KeyValueComparer<TObject, TKey>(comparer);

    //    //    _innerCache.
    //    }

    //    /// <summary>
    //    /// Changes the comparer.
    //    /// </summary>
    //    /// <param name="comparer">The comparer.</param>
    //    /// <returns></returns>
    //    public IChangeSet<TObject, TKey> ChangeComparer(IComparer<KeyValuePair<TKey,TObject>> comparer)
    //    {
            
    //    }

    //    public IComparer<KeyValuePair<TKey,TObject>> Comparer
    //    {
    //        get
    //        {
    //            return _comparer;
    //        }
    //    }

    //    public List<KeyValuePair<TKey,TObject>> List
    //    {
    //        get
    //        {
    //            return _list;
    //        }
    //    }

    //}
}