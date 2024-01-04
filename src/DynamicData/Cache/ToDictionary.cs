using System.Reactive.Linq;
using DynamicData.Cache.Internal;

namespace DynamicData.Cache;

internal static class ToDictionaryEx
{
    public static IObservable<Dictionary<TKey, TObject>> ToDictionary<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        return new ToDictionary<TObject, TKey>(source).Run();
    }

}

internal class ToDictionary<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{

    public IObservable<Dictionary<TKey, TObject>> Run() =>
        Observable.Defer(() =>
            {
                // maintain the dictionary
                return source.Scan(
                    (Dictionary<TKey, TObject>?)null,
                    (cache, changes) =>
                    {
                        cache ??= new Dictionary<TKey, TObject>(changes.Count);

                        cache.Clone(changes);
                        return cache;
                    });
            })
            // clone so the dictionary is thread safe-
            .Select(cache => new Dictionary<TKey, TObject>(cache!));
}
