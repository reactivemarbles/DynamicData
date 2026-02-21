using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
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
    private readonly Subject<IChangeSet<T>> _refreshRequested;
    private readonly Subject<IChangeSet<T>> _refreshRequestedPreview;
    private readonly SourceList<T> _source;

    public TestSourceList()
    {
        _error = new(null);
        _hasCompleted = new(false);
        _refreshRequested = new();
        _refreshRequestedPreview = new();
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
        => WrapStream(Observable.Merge(
            _source.Connect(predicate),
            _refreshRequested));

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
        _refreshRequested.Dispose();
        _refreshRequestedPreview.Dispose();
    }
    
    public void Edit(Action<IExtendedList<T>> updateAction)
    {
        AssertCanMutate();

        _source.Edit(updateAction);
    }
    
    public IObservable<IChangeSet<T>> Preview(Func<T, bool>? predicate = null)
        => WrapStream(Observable.Merge(
            _source.Preview(predicate),
            _refreshRequestedPreview));

    // TODO: Formally add this to ISourceList
    public void Refresh()
    {
        var changeSet = new ChangeSet<T>(_source.Items
            .Select((item, index) => new Change<T>(
                reason:     ListChangeReason.Refresh,
                current:    item,
                index:      index)));

        _refreshRequestedPreview.OnNext(changeSet);
        _refreshRequested.OnNext(changeSet);
    }

    // TODO: Formally add this to ISourceList
    public void Refresh(int index)
    {
        var changeSet = new ChangeSet<T>(capacity: 1)
        {
            new Change<T>(
                reason:     ListChangeReason.Refresh,
                current:    _source.Items.ElementAt(index),
                index:      index)
        };

        _refreshRequestedPreview.OnNext(changeSet);
        _refreshRequested.OnNext(changeSet);
    }

    // TODO: Formally add this to ISourceList
    public void Refresh(IEnumerable<int> indexes)
    {
        var changeSet = new ChangeSet<T>(indexes
            .Select(index => new Change<T>(
                reason:     ListChangeReason.Refresh,
                current:    _source.Items.ElementAt(index),
                index:      index)));

        _refreshRequestedPreview.OnNext(changeSet);
        _refreshRequested.OnNext(changeSet);
    }

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
        => Observable.Create<U>(downstreamObserver =>
        {
            var hasCompleted = _hasCompleted
                .Publish();
            
            var subscription = Observable
                .Merge(
                    _error
                        .Select(static error => (error is not null)
                            ? Observable.Throw<U>(error!)
                            : Observable.Empty<U>())
                        .Switch(),
                    sourceStream)
                .TakeUntil(hasCompleted
                    .Where(static hasCompleted => hasCompleted))
                .SubscribeSafe(downstreamObserver);
            
            // Make sure that an initial changeset gets published, before immediate completion.
            var connection = hasCompleted.Connect();
            
            return Disposable.Create(() =>
            {
                connection.Dispose();
                subscription.Dispose();
            });
        });
}
