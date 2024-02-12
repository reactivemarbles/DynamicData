// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;

using DynamicData.Diagnostics;

namespace DynamicData.Tests;

/// <summary>
/// Aggregates all events and statistics for a group change set to help assertions when testing.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <typeparam name="TGroupKey">The type of the grouping key.</typeparam>
public class GroupChangeSetAggregator<TObject, TKey, TGroupKey> : IDisposable
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    private readonly CompositeDisposable _compositeDisposable;
    private readonly List<IGroupChangeSet<TObject, TKey, TGroupKey>> _messages = [];
    private bool _disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroupChangeSetAggregator{TObject, TKey, TGroupKey}"/> class.
    /// </summary>
    /// <param name="source">The source.</param>
    public GroupChangeSetAggregator(IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> source)
    {
        var published = source.Publish();

        Data = published.AsObservableCache();
        Groups = published.Transform(grp => grp.Cache.Connect().AsAggregator()).DisposeMany().AsObservableCache();
        var results = published.Subscribe(_messages.Add, ex => Error = ex, () => IsCompleted = true);
        var summariser = published.CollectUpdateStats().Subscribe(summary => Summary = summary, static _ => { });

        _compositeDisposable = new(Data, Groups, results, summariser, published.Connect());
    }

    /// <summary>
    /// Gets the data.
    /// </summary>
    /// <value>
    /// The data.
    /// </value>
    public IObservableCache<IGroup<TObject, TKey, TGroupKey>, TGroupKey> Data { get; }

    /// <summary>Gets a cache containing the aggregated results of each individual group.</summary>
    public IObservableCache<ChangeSetAggregator<TObject, TKey>, TGroupKey> Groups { get; }

    /// <summary>
    /// Gets the error.
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    public Exception? Error { get; private set; }

    /// <summary>
    /// Gets a value indicating whether or not the ChangeSet fired OnCompleted.
    /// </summary>
    /// <value>
    /// Boolean Value.
    /// </value>
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Gets a list of the messages that were received.
    /// </summary>
    public IReadOnlyList<IGroupChangeSet<TObject, TKey, TGroupKey>> Messages => _messages;

    /// <summary>
    /// Gets the summary.
    /// </summary>
    /// <value>
    /// The summary.
    /// </value>
    public ChangeSummary Summary { get; private set; } = ChangeSummary.Empty;

    /// <inheritdoc/>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed and unmanaged responses.
    /// </summary>
    /// <param name="disposing">If being called by the Dispose method.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            _ = disposing;
            _compositeDisposable.Dispose();
            _disposedValue = true;
        }
    }
}
