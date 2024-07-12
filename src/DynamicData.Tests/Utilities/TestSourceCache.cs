using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;

using DynamicData.Kernel;

namespace DynamicData.Tests.Utilities;

public sealed class TestSourceCache<TObject, TKey>
        : ISourceCache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<int> _countChanged;
    private readonly BehaviorSubject<Exception?> _error;
    private readonly BehaviorSubject<bool> _hasCompleted;
    private readonly SourceCache<TObject, TKey> _source;

    public TestSourceCache(Func<TObject, TKey> keySelector)
    {
        _error = new(null);
        _hasCompleted = new(false);
        _source = new(keySelector);

        _countChanged = WrapStream(_source.CountChanged);
    }

    public int Count
        => _source.Count;

    public IObservable<int> CountChanged
        => _countChanged;

    public IReadOnlyList<TObject> Items
        => _source.Items;
    
    public IReadOnlyList<TKey> Keys
        => _source.Keys;

    public Func<TObject, TKey> KeySelector
        => _source.KeySelector;

    public IReadOnlyDictionary<TKey, TObject> KeyValues
        => _source.KeyValues;

    public void Complete()
    {
        AssertCanMutate();

        _hasCompleted.OnNext(true);
    }

    public IObservable<IChangeSet<TObject, TKey>> Connect(
            Func<TObject, bool>? predicate = null,
            bool suppressEmptyChangeSets = true)
        => WrapStream(_source.Connect(predicate, suppressEmptyChangeSets));

    public void Dispose()
    {
        _source.Dispose();
        _error.Dispose();
        _hasCompleted.Dispose();
    }

    public void Edit(Action<ISourceUpdater<TObject, TKey>> updateAction)
    {
        AssertCanMutate();

        _source.Edit(updateAction);
    }

    public Optional<TObject> Lookup(TKey key)
        => _source.Lookup(key);

    public IObservable<IChangeSet<TObject, TKey>> Preview(Func<TObject, bool>? predicate = null)
        => WrapStream(_source.Preview(predicate));

    public void SetError(Exception error)
    {
        AssertCanMutate();

        _error.OnNext(error);
    }

    public IObservable<Change<TObject, TKey>> Watch(TKey key)
        => WrapStream(_source.Watch(key));

    private void AssertCanMutate()
    {
        if (_error.Value is not null)
            throw new InvalidOperationException("The source collection is in an error state and cannot be mutated.");

        if (_hasCompleted.Value)
            throw new InvalidOperationException("The source collection is in a completed state and cannot be mutated.");
    }

    private IObservable<T> WrapStream<T>(IObservable<T> sourceStream)
        => Observable
            .Merge(
                _error
                    .Select(static error => (error is not null)
                        ? Observable.Throw<T>(error!)
                        : Observable.Empty<T>())
                    .Switch(),
                sourceStream)
            .TakeUntil(_hasCompleted
                .Where(static hasCompleted => hasCompleted));
}
