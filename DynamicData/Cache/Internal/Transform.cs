using System;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class Transform<TDestination, TSource, TKey>
    {
        private readonly IObservable<IChangeSet<TSource, TKey>> _source;
        private readonly Func<TSource, Optional<TSource>, TKey, TDestination> _transformFactory;
        private readonly Action<Error<TSource, TKey>> _exceptionCallback;
        private readonly bool _transformOnRefresh;

        public Transform(IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, Optional<TSource>, TKey, TDestination> transformFactory,
            Action<Error<TSource, TKey>> exceptionCallback = null,
            bool transformOnRefresh = false)
        {
            _source = source;
            _exceptionCallback = exceptionCallback;
            _transformOnRefresh = transformOnRefresh;
            _transformFactory = transformFactory;
        }

        public IObservable<IChangeSet<TDestination, TKey>> Run()
        {
            return _source.Scan(new ChangeAwareCache<TDestination, TKey>(), (cache, changes) =>
                {
                    foreach (var change in changes)
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                            case ChangeReason.Update:
                            {
                                TDestination transformed;
                                if (_exceptionCallback != null)
                                {
                                    try
                                    {
                                        transformed = _transformFactory(change.Current, change.Previous, change.Key);
                                        cache.AddOrUpdate(transformed, change.Key);
                                    }
                                    catch (Exception ex)
                                    {
                                        _exceptionCallback(new Error<TSource, TKey>(ex, change.Current, change.Key));
                                    }
                                }
                                else
                                {
                                    transformed = _transformFactory(change.Current, change.Previous, change.Key);
                                    cache.AddOrUpdate(transformed, change.Key);
                                }
                            }
                                break;
                            case ChangeReason.Remove:
                                cache.Remove(change.Key);
                                break;
                            case ChangeReason.Refresh:
                            {
                                if (_transformOnRefresh)
                                {
                                    var transformed = _transformFactory(change.Current, change.Previous, change.Key);
                                    cache.AddOrUpdate(transformed, change.Key);
                                }
                                else
                                {
                                    cache.Refresh(change.Key);
                                }
                            }

                                break;
                            case ChangeReason.Moved:
                                //Do nothing !
                                break;
                        }
                    }
                    return cache;
                })
                .Select(cache => cache.CaptureChanges())
                .NotEmpty();
        }
    }
}