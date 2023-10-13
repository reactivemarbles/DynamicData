// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class AsObservableChangeSet<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<IEnumerable<TObject>> _source;
    private readonly IEqualityComparer<TObject> _equalityComparer;
    private readonly Func<TObject, TKey> _keySelector;

    public AsObservableChangeSet(IObservable<IEnumerable<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var cache = new SourceCache<TObject, TKey>(_keySelector);

                var subscription = _source.Subscribe(coll => cache.EditDiff(coll, _equalityComparer), observer.OnError, observer.OnCompleted);

                return new CompositeDisposable(subscription, cache.Connect().Subscribe(observer.OnNext, _ => { }, () => { }), cache);
            });
    }
}
