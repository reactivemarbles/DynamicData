using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Tests.Utilities;

public sealed class TestSourceList<T>
        : ISourceList<T>
    where T : notnull
{
    private readonly IObservable<int> _countChanged;
    private readonly BehaviorSubject<Exception?> _error;
    private readonly BehaviorSubject<bool> _hasCompleted;
    private readonly SourceList<T> _source;

    public TestSourceList()
    {
        _error = new(null);
        _hasCompleted = new(false);
        _source = new();

        _countChanged = WrapStream(_source.CountChanged);
    }

    public int Count
        => _source.Count;

    public IObservable<int> CountChanged
        => _countChanged;

    public IReadOnlyList<T> Items
        => _source.Items;

    public IObservable<IChangeSet<T>> Connect(Func<T, bool>? predicate = null)
        => WrapStream(_source.Connect(predicate));

    public void Complete()
    {
        AssertCanMutate();

        _hasCompleted.OnNext(true);
    }

    public void Dispose()
    {
        _source.Dispose();
        _error.Dispose();
        _hasCompleted.Dispose();
    }
    
    public void Edit(Action<IExtendedList<T>> updateAction)
    {
        AssertCanMutate();

        _source.Edit(updateAction);
    }
    
    public IObservable<IChangeSet<T>> Preview(Func<T, bool>? predicate = null)
        => WrapStream(_source.Preview(predicate));

    public void SetError(Exception error)
    {
        AssertCanMutate();

        _error.OnNext(error);
    }

    private void AssertCanMutate()
    {
        if (_error.Value is not null)
            throw new InvalidOperationException("The source collection is in an error state and cannot be mutated.");

        if (_hasCompleted.Value)
            throw new InvalidOperationException("The source collection is in a completed state and cannot be mutated.");
    }

    private IObservable<U> WrapStream<U>(IObservable<U> sourceStream)
        => Observable
            .Merge(
                _error
                    .Select(static error => (error is not null)
                        ? Observable.Throw<U>(error!)
                        : Observable.Empty<U>())
                    .Switch(),
                sourceStream)
            .TakeUntil(_hasCompleted
                .Where(static hasCompleted => hasCompleted));
}
