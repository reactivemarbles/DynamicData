// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;
using DynamicData.List.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class TransformWithInlineUpdate<TDestination, TSource, TKey>
    where TDestination : class
    where TSource : notnull
    where TKey : notnull
{
    private readonly Action<Error<TSource, TKey>>? _exceptionCallback;

    private readonly IObservable<IChangeSet<TSource, TKey>> _source;

    private readonly Func<TSource, TDestination> _transformFactory;

    private readonly Action<TDestination, TSource> _updateAction;

    public TransformWithInlineUpdate(IObservable<IChangeSet<TSource, TKey>> source,
                                     Func<TSource, TDestination> transformFactory,
                                     Action<TDestination, TSource> updateAction,
                                     Action<Error<TSource, TKey>>? exceptionCallback = null)
    {
        _source = source;
        _exceptionCallback = exceptionCallback;
        _updateAction = updateAction;
        _transformFactory = transformFactory;
    }

    public IObservable<IChangeSet<TDestination, TKey>> Run() => Observable.Defer(RunImpl);

    private IObservable<IChangeSet<TDestination, TKey>> RunImpl()
    {
        return _source.Scan(
                (ChangeAwareCache<TDestination, TKey>?)null,
                (cache, changes) =>
                {
                    cache ??= new ChangeAwareCache<TDestination, TKey>(changes.Count);

                    foreach (var change in changes.ToConcreteType())
                    {
                        switch (change.Reason)
                        {
                            case ChangeReason.Add:
                                Transform(cache, change);
                                break;

                            case ChangeReason.Update:
                                InlineUpdate(cache, change);
                                break;

                            case ChangeReason.Remove:
                                cache.Remove(change.Key);
                                break;

                            case ChangeReason.Refresh:
                                cache.Refresh(change.Key);
                                break;

                            case ChangeReason.Moved:
                                // Do nothing !
                                break;
                        }
                    }

                    return cache;
                })
            .Where(x => x is not null)
            .Select(cache => cache!.CaptureChanges());
    }

    private void Transform(ChangeAwareCache<TDestination, TKey> cache, Change<TSource, TKey> change)
    {
        TDestination transformed;
        if (_exceptionCallback is not null)
        {
            try
            {
                transformed = _transformFactory(change.Current);
                cache.AddOrUpdate(transformed, change.Key);
            }
            catch (Exception ex)
            {
                _exceptionCallback(new Error<TSource, TKey>(ex, change.Current, change.Key));
            }
        }
        else
        {
            transformed = _transformFactory(change.Current);
            cache.AddOrUpdate(transformed, change.Key);
        }
    }

    private void InlineUpdate(ChangeAwareCache<TDestination, TKey> cache, Change<TSource, TKey> change)
    {
        var previous = cache.Lookup(change.Key)
                                .ValueOrThrow(() => new MissingKeyException($"{change.Key} is not found."));
        if (_exceptionCallback is not null)
        {
            try
            {
                _updateAction(previous, change.Current);
            }
            catch (Exception ex)
            {
                _exceptionCallback(new Error<TSource, TKey>(ex, change.Current, change.Key));
            }
        }
        else
        {
            _updateAction(previous, change.Current);
        }

        cache.Refresh(change.Key);
    }
}
