// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Internal;

namespace DynamicData.Cache.Internal;

internal sealed class TransformOnObservable<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform, bool transformOnRefresh = false)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() =>
        Observable.Create<IChangeSet<TDestination, TKey>>(observer => new Subscription(source, transform, observer, transformOnRefresh));

    // Maintains state for a single subscription
    private sealed class Subscription : CacheParentSubscription<TSource, TKey, TDestination, IChangeSet<TDestination, TKey>>
    {
        private readonly ChangeAwareCache<TDestination, TKey> _cache = new();
        private readonly Func<TSource, TKey, IObservable<TDestination>> _transform;
        private readonly bool _transformOnRefresh;

        public Subscription(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TKey, IObservable<TDestination>> transform, IObserver<IChangeSet<TDestination, TKey>> observer, bool transformOnRefresh)
            : base(observer)
        {
            _transform = transform;
            _transformOnRefresh = transformOnRefresh;
            CreateParentSubscription(source);
        }

        protected override void ParentOnNext(IChangeSet<TSource, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one that
                    // Will update the cache and emit the changes
                    case ChangeReason.Add or ChangeReason.Update:
                        AddTransformSubscription(change.Current, change.Key);
                        break;

                    // Shutdown the existing subscription and remove from the cache
                    case ChangeReason.Remove:
                        _cache.Remove(change.Key);
                        RemoveChildSubscription(change.Key);
                        break;

                    case ChangeReason.Refresh:
                        if (_transformOnRefresh)
                        {
                            AddTransformSubscription(change.Current, change.Key);
                        }
                        else
                        {
                            // Let the downstream decide what this means
                            _cache.Refresh(change.Key);
                        }
                        break;
                }
            }
        }

        protected override void ChildOnNext(TDestination child, TKey parentKey) =>
            _cache.AddOrUpdate(child, parentKey);

        protected override void EmitChanges(IObserver<IChangeSet<TDestination, TKey>> observer)
        {
            var changes = _cache.CaptureChanges();
            if (changes.Count > 0)
            {
                observer.OnNext(changes);
            }
        }

        private void AddTransformSubscription(TSource obj, TKey key) =>
            AddChildSubscription(MakeChildObservable(_transform(obj, key).DistinctUntilChanged()), key);
    }
}
