using System.Collections.ObjectModel;

namespace DynamicData.Binding;

internal record struct BindSortOptions(
    int ResetThreshold,
    bool UseReplaceForUpdates,
    bool ResetOnFirstTimeLoad,
    bool UseBinarySearch);



public static class BindSortEx
{
    public static IObservable<IChangeSet<TObject, TKey>> BindSort<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        ICollection<TObject> col,
        IComparer<TObject> comparer,
        BindingOptions? options = null)
        where TObject : notnull
        where TKey : notnull
    {
        throw new NotImplementedException();
    }

    public static IObservable<IChangeSet<TObject, TKey>> BindSort<TObject, TKey>(
        this IObservable<IChangeSet<TObject, TKey>> source,
        out ReadOnlyObservableCollection<TObject> col,
        IComparer<TObject> comparer,
        BindingOptions? options = null)
        where TObject : notnull
        where TKey : notnull
    {
        throw new NotImplementedException();
    }


}
